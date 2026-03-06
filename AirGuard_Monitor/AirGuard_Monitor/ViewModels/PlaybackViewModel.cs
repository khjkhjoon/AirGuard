using AirGuard.WPF.Map;
using AirGuard.WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AirGuard.WPF.ViewModels
{
    public class PlaybackViewModel : BaseViewModel, IDisposable
    {
        private readonly DatabaseService _db;
        private MapRenderer? _mapRenderer;

        // ===== 날짜 선택 =====
        public ObservableCollection<string> AvailableDates { get; } = new();
        private string _selectedDate = "";
        public string SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value) && !string.IsNullOrEmpty(value))
                    _ = LoadRecordsByDateAsync(value);
            }
        }
        public Visibility DateComboVisibility => AvailableDates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // ===== 세션 선택 =====
        private List<SessionRecord> _sessionList = new();
        public ObservableCollection<string> AvailableSessions { get; } = new();
        private string _selectedSession = "";
        public string SelectedSession
        {
            get => _selectedSession;
            set
            {
                if (SetProperty(ref _selectedSession, value) && !string.IsNullOrEmpty(value))
                {
                    var idx = AvailableSessions.IndexOf(value);
                    if (idx >= 0 && idx < _sessionList.Count)
                        _ = LoadRecordsBySessionAsync(_sessionList[idx].Id);
                }
            }
        }
        public Visibility SessionComboVisibility => AvailableSessions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // ===== 플레이백 상태 =====
        private List<FlightLogRecord> _records = new();
        private int _currentIndex;
        private bool _isPlaying;
        private bool _isPaused;
        private double _playbackSpeed = 1.0;
        private string _currentVehicleId = "";
        private string _currentVehicleName = "";

        // ===== 타이머 =====
        private CancellationTokenSource? _cts;

        // ===== 지도 마커 =====
        private double _markerX;
        private double _markerY;
        private double _markerAlt;
        private double _markerSpeed;
        private double _markerBattery;
        private string _markerStatus = "";
        private string _markerTime = "";

        public void SetMapRenderer(MapRenderer r) => _mapRenderer = r;

        // ===== 프로퍼티 =====
        public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }
        public bool IsPaused { get => _isPaused; private set => SetProperty(ref _isPaused, value); }
        public bool HasRecords => _records.Count > 0;

        public double MarkerX { get => _markerX; private set => SetProperty(ref _markerX, value); }
        public double MarkerY { get => _markerY; private set => SetProperty(ref _markerY, value); }
        public double MarkerAlt { get => _markerAlt; private set => SetProperty(ref _markerAlt, value); }
        public double MarkerSpeed { get => _markerSpeed; private set => SetProperty(ref _markerSpeed, value); }
        public double MarkerBattery { get => _markerBattery; private set => SetProperty(ref _markerBattery, value); }
        public string MarkerStatus { get => _markerStatus; private set => SetProperty(ref _markerStatus, value); }
        public string MarkerTime { get => _markerTime; private set => SetProperty(ref _markerTime, value); }

        public string VehicleLabel => string.IsNullOrEmpty(_currentVehicleName)
            ? "— 드론을 선택하세요 —"
            : $"{_currentVehicleName}  ({_records.Count}개 기록)";

        public int TotalFrames => _records.Count;
        public int CurrentFrame
        {
            get => _currentIndex;
            set
            {
                if (_records.Count == 0) return;
                _currentIndex = Math.Clamp(value, 0, _records.Count - 1);
                OnPropertyChanged(nameof(CurrentFrame));
                OnPropertyChanged(nameof(ProgressText));
                ApplyFrame(_currentIndex);
            }
        }

        public string ProgressText => _records.Count == 0 ? "0 / 0"
            : $"{_currentIndex + 1} / {_records.Count}  {_records[_currentIndex].RecordedAt:HH:mm:ss}";

        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set { SetProperty(ref _playbackSpeed, Math.Clamp(value, 0.25, 10.0)); }
        }

        public ObservableCollection<string> SpeedOptions { get; } =
            new() { "0.25x", "0.5x", "1x", "2x", "5x", "10x" };

        private string _selectedSpeed = "1x";
        public string SelectedSpeed
        {
            get => _selectedSpeed;
            set
            {
                SetProperty(ref _selectedSpeed, value);
                PlaybackSpeed = value switch
                {
                    "0.25x" => 0.25,
                    "0.5x" => 0.5,
                    "2x" => 2.0,
                    "5x" => 5.0,
                    "10x" => 10.0,
                    _ => 1.0
                };
            }
        }

        // 경로 포인트 (지도에 표시)
        public string PathPoints { get => _pathPoints; private set => SetProperty(ref _pathPoints, value); }
        private string _pathPoints = "";

        public Visibility HasRecordsVisibility => HasRecords ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NoRecordsVisibility => HasRecords ? Visibility.Collapsed : Visibility.Visible;
        public Visibility MarkerVisibility => HasRecords ? Visibility.Visible : Visibility.Collapsed;

        public Brush MarkerColor => _markerStatus switch
        {
            "Active" => new SolidColorBrush(Color.FromRgb(0, 255, 136)),
            "Emergency" => new SolidColorBrush(Color.FromRgb(255, 59, 59)),
            _ => new SolidColorBrush(Color.FromRgb(255, 140, 0))
        };

        public string PlayButtonText => IsPlaying && !IsPaused ? "⏸" : "▶";

        // ===== 커맨드 =====
        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand StepBackCommand { get; }
        public ICommand StepFwdCommand { get; }

        public PlaybackViewModel(DatabaseService db)
        {
            _db = db;

            PlayPauseCommand = new RelayCommand(_ =>
            {
                if (!HasRecords) return Task.CompletedTask;
                if (IsPlaying && !IsPaused) PausePlayback();
                else StartPlayback();
                return Task.CompletedTask;
            });
            StopCommand = new RelayCommand(_ =>
            {
                StopPlayback();
                return Task.CompletedTask;
            });
            StepBackCommand = new RelayCommand(_ =>
            {
                if (_currentIndex > 0) CurrentFrame--;
                return Task.CompletedTask;
            });
            StepFwdCommand = new RelayCommand(_ =>
            {
                if (_currentIndex < _records.Count - 1) CurrentFrame++;
                return Task.CompletedTask;
            });
        }

        // ===== 드론 선택 시 날짜 목록 로드 =====
        public async Task LoadRecordsAsync(string vehicleId, string vehicleName)
        {
            StopPlayback();
            _currentVehicleId = vehicleId;
            _currentVehicleName = vehicleName;

            // 날짜 목록 로드
            var dates = await Task.Run(() => _db.GetFlightDates(vehicleId));
            AvailableDates.Clear();
            foreach (var d in dates)
                AvailableDates.Add(d.ToString("yyyy-MM-dd"));

            OnPropertyChanged(nameof(DateComboVisibility));
            OnPropertyChanged(nameof(VehicleLabel));

            // 가장 최근 날짜 자동 선택
            if (AvailableDates.Count > 0)
            {
                _selectedDate = AvailableDates[0];
                OnPropertyChanged(nameof(SelectedDate));
                await LoadRecordsByDateAsync(AvailableDates[0]);
            }
            else
            {
                _records.Clear();
                RefreshRecordProps();
            }
        }

        private async Task LoadRecordsByDateAsync(string dateStr)
        {
            StopPlayback();
            if (!DateTime.TryParse(dateStr, out var date)) return;

            // 해당 날짜의 세션 목록 로드
            _sessionList = await Task.Run(() => _db.GetSessionsByDate(_currentVehicleId, date));
            AvailableSessions.Clear();
            foreach (var s in _sessionList)
                AvailableSessions.Add(s.Label);

            OnPropertyChanged(nameof(SessionComboVisibility));

            // 가장 첫 세션 자동 선택
            if (_sessionList.Count > 0)
            {
                _selectedSession = AvailableSessions[0];
                OnPropertyChanged(nameof(SelectedSession));
                await LoadRecordsBySessionAsync(_sessionList[0].Id);
            }
            else
            {
                // 세션 없으면 날짜 전체 기록으로 폴백
                _records = await Task.Run(() => _db.GetFlightLogsByDate(_currentVehicleId, date));
                _currentIndex = 0;
                BuildPathPoints();
                RefreshRecordProps();
                if (_records.Count > 0) ApplyFrame(0);
            }
        }

        private async Task LoadRecordsBySessionAsync(int sessionId)
        {
            StopPlayback();
            _records = await Task.Run(() => _db.GetFlightLogsBySession(sessionId));
            _currentIndex = 0;
            BuildPathPoints();
            RefreshRecordProps();
            if (_records.Count > 0) ApplyFrame(0);
        }

        private void RefreshRecordProps()
        {
            OnPropertyChanged(nameof(HasRecords));
            OnPropertyChanged(nameof(TotalFrames));
            OnPropertyChanged(nameof(VehicleLabel));
            OnPropertyChanged(nameof(HasRecordsVisibility));
            OnPropertyChanged(nameof(NoRecordsVisibility));
            OnPropertyChanged(nameof(MarkerVisibility));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(CurrentFrame));
        }

        // ===== 재생 =====
        private void StartPlayback()
        {
            if (!HasRecords) return;
            if (_currentIndex >= _records.Count - 1) _currentIndex = 0;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            IsPlaying = true;
            IsPaused = false;
            OnPropertyChanged(nameof(PlayButtonText));

            var token = _cts.Token;
            _ = Task.Run(async () =>
            {
                while (_currentIndex < _records.Count && !token.IsCancellationRequested)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ApplyFrame(_currentIndex);
                        OnPropertyChanged(nameof(CurrentFrame));
                        OnPropertyChanged(nameof(ProgressText));
                    });

                    _currentIndex++;
                    int delayMs = (int)(200 / _playbackSpeed); // 200ms 기준
                    await Task.Delay(Math.Max(10, delayMs), token).ContinueWith(_ => { });
                }

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsPlaying = false;
                    IsPaused = false;
                    OnPropertyChanged(nameof(PlayButtonText));
                });
            }, token);
        }

        private void PausePlayback()
        {
            _cts?.Cancel();
            IsPaused = true;
            IsPlaying = true;
            OnPropertyChanged(nameof(PlayButtonText));
        }

        public void StopPlayback()
        {
            _cts?.Cancel();
            IsPlaying = false;
            IsPaused = false;
            _currentIndex = 0;
            OnPropertyChanged(nameof(CurrentFrame));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(PlayButtonText));
        }

        // ===== 프레임 적용 =====
        private void ApplyFrame(int index)
        {
            if (index < 0 || index >= _records.Count) return;
            var r = _records[index];

            MarkerAlt = r.Altitude;
            MarkerSpeed = r.Speed;
            MarkerBattery = r.Battery;
            MarkerStatus = r.Status;
            MarkerTime = r.RecordedAt.ToString("HH:mm:ss");

            if (_mapRenderer != null && _mapRenderer.IsMapLoaded)
            {
                var (cx, cy) = _mapRenderer.WorldToCanvas(r.Longitude, r.Latitude);
                MarkerX = cx;
                MarkerY = cy;
            }
        }

        // ===== 경로 포인트 생성 =====
        private void BuildPathPoints()
        {
            if (_mapRenderer == null || !_mapRenderer.IsMapLoaded || _records.Count == 0)
            {
                PathPoints = "";
                return;
            }
            var sb = new StringBuilder();
            foreach (var r in _records)
            {
                var (cx, cy) = _mapRenderer.WorldToCanvas(r.Longitude, r.Latitude);
                sb.Append($"{cx:F1},{cy:F1} ");
            }
            PathPoints = sb.ToString().TrimEnd();
        }

        public void Dispose() => _cts?.Cancel();
    }
}