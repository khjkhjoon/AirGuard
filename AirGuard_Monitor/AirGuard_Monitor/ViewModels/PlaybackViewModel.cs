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
    /// <summary>
    /// 플레이백 뷰모델 - 비행 기록 재생, 날짜/세션 선택, 지도 마커 표시 관리
    /// </summary>
    public class PlaybackViewModel : BaseViewModel, IDisposable
    {
        // 데이터베이스 서비스
        private readonly DatabaseService _db;
        // 맵 렌더러 참조 (좌표 변환용)
        private MapRenderer? _mapRenderer;

        // 선택 가능한 날짜 목록
        public ObservableCollection<string> AvailableDates { get; } = new();
        // 선택된 날짜
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
        // 날짜 콤보박스 표시 여부
        public Visibility DateComboVisibility => AvailableDates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // 세션 데이터 목록 (DB 조회 결과)
        private List<SessionRecord> _sessionList = new();
        // 선택 가능한 세션 레이블 목록
        public ObservableCollection<string> AvailableSessions { get; } = new();
        // 선택된 세션 레이블
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
        // 세션 콤보박스 표시 여부
        public Visibility SessionComboVisibility => AvailableSessions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // 현재 로드된 비행 기록 목록
        private List<FlightLogRecord> _records = new();
        // 현재 재생 중인 프레임 인덱스
        private int _currentIndex;
        // 재생 중 여부
        private bool _isPlaying;
        // 일시정지 여부
        private bool _isPaused;
        // 재생 속도 배율
        private double _playbackSpeed = 1.0;
        // 현재 드론 ID
        private string _currentVehicleId = "";
        // 현재 드론 이름
        private string _currentVehicleName = "";

        // 재생 취소 토큰
        private CancellationTokenSource? _cts;

        // 지도 마커 X 좌표 (캔버스 픽셀)
        private double _markerX;
        // 지도 마커 Y 좌표 (캔버스 픽셀)
        private double _markerY;
        // 마커 고도
        private double _markerAlt;
        // 마커 속도
        private double _markerSpeed;
        // 마커 배터리
        private double _markerBattery;
        // 마커 상태 문자열
        private string _markerStatus = "";
        // 마커 기록 시각 문자열
        private string _markerTime = "";

        // 맵 렌더러 설정
        public void SetMapRenderer(MapRenderer r) => _mapRenderer = r;

        // 재생 중 여부 프로퍼티
        public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }
        // 일시정지 여부 프로퍼티
        public bool IsPaused { get => _isPaused; private set => SetProperty(ref _isPaused, value); }
        // 기록 존재 여부
        public bool HasRecords => _records.Count > 0;

        // 마커 캔버스 X 좌표
        public double MarkerX { get => _markerX; private set => SetProperty(ref _markerX, value); }
        // 마커 캔버스 Y 좌표
        public double MarkerY { get => _markerY; private set => SetProperty(ref _markerY, value); }
        // 마커 고도
        public double MarkerAlt { get => _markerAlt; private set => SetProperty(ref _markerAlt, value); }
        // 마커 속도
        public double MarkerSpeed { get => _markerSpeed; private set => SetProperty(ref _markerSpeed, value); }
        // 마커 배터리
        public double MarkerBattery { get => _markerBattery; private set => SetProperty(ref _markerBattery, value); }
        // 마커 상태
        public string MarkerStatus { get => _markerStatus; private set => SetProperty(ref _markerStatus, value); }
        // 마커 시각
        public string MarkerTime { get => _markerTime; private set => SetProperty(ref _markerTime, value); }

        // 드론 레이블 텍스트 (이름 + 기록 수)
        public string VehicleLabel => string.IsNullOrEmpty(_currentVehicleName)
            ? "— 드론을 선택하세요 —"
            : $"{_currentVehicleName}  ({_records.Count}개 기록)";

        // 총 프레임 수
        public int TotalFrames => _records.Count;

        // 현재 프레임 인덱스 - 설정 시 해당 프레임 즉시 적용
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

        // 재생 진행 텍스트 (현재 프레임 / 전체 + 시각)
        public string ProgressText => _records.Count == 0 ? "0 / 0"
            : $"{_currentIndex + 1} / {_records.Count}  {_records[_currentIndex].RecordedAt:HH:mm:ss}";

        // 재생 속도 배율 (0.25 ~ 10.0)
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set { SetProperty(ref _playbackSpeed, Math.Clamp(value, 0.25, 10.0)); }
        }

        // 속도 선택 옵션 목록
        public ObservableCollection<string> SpeedOptions { get; } =
            new() { "0.25x", "0.5x", "1x", "2x", "5x", "10x" };

        // 선택된 속도 문자열
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

        // 지도에 표시할 경로 포인트 문자열 ("x1,y1 x2,y2 ...")
        public string PathPoints { get => _pathPoints; private set => SetProperty(ref _pathPoints, value); }
        private string _pathPoints = "";

        // 기록 존재 시 표시
        public Visibility HasRecordsVisibility => HasRecords ? Visibility.Visible : Visibility.Collapsed;
        // 기록 없을 시 안내문 표시
        public Visibility NoRecordsVisibility => HasRecords ? Visibility.Collapsed : Visibility.Visible;
        // 마커 표시 여부
        public Visibility MarkerVisibility => HasRecords ? Visibility.Visible : Visibility.Collapsed;

        // 마커 색상 (상태별)
        public Brush MarkerColor => _markerStatus switch
        {
            "Active" => new SolidColorBrush(Color.FromRgb(0, 255, 136)),
            "Emergency" => new SolidColorBrush(Color.FromRgb(255, 59, 59)),
            _ => new SolidColorBrush(Color.FromRgb(255, 140, 0))
        };

        // 재생/일시정지 버튼 텍스트
        public string PlayButtonText => IsPlaying && !IsPaused ? "⏸" : "▶";

        // 재생/일시정지 토글 커맨드
        public ICommand PlayPauseCommand { get; }
        // 정지 커맨드
        public ICommand StopCommand { get; }
        // 한 프레임 뒤로 커맨드
        public ICommand StepBackCommand { get; }
        // 한 프레임 앞으로 커맨드
        public ICommand StepFwdCommand { get; }

        /// <summary>
        /// 생성자 - DB 서비스 주입 및 커맨드 초기화
        /// </summary>
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

        /// <summary>
        /// 드론 선택 시 호출 - 날짜 목록 로드 후 가장 최근 날짜 자동 선택
        /// </summary>
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

        /// <summary>
        /// 날짜 선택 시 해당 날짜의 세션 목록 로드 후 첫 세션 자동 선택
        /// </summary>
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

            if (_sessionList.Count > 0)
            {
                // 첫 세션 자동 선택
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

        /// <summary>
        /// 세션 ID로 비행 기록 로드
        /// </summary>
        private async Task LoadRecordsBySessionAsync(int sessionId)
        {
            StopPlayback();
            _records = await Task.Run(() => _db.GetFlightLogsBySession(sessionId));
            _currentIndex = 0;
            BuildPathPoints();
            RefreshRecordProps();
            if (_records.Count > 0) ApplyFrame(0);
        }

        // 기록 관련 프로퍼티 일괄 갱신
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

        //재생 시작 - 백그라운드 태스크에서 200ms 간격으로 프레임 진행
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
                    // 속도 배율에 따라 딜레이 조정 (기준 200ms)
                    int delayMs = (int)(200 / _playbackSpeed);
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

        // 일시정지 - 태스크 취소 후 일시정지 상태로 전환
        private void PausePlayback()
        {
            _cts?.Cancel();
            IsPaused = true;
            IsPlaying = true;
            OnPropertyChanged(nameof(PlayButtonText));
        }

        // 정지 - 태스크 취소 후 첫 프레임으로 리셋
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

        // 지정 인덱스 프레임을 마커에 적용 - 맵 좌표 변환 포함
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

        // 경로 포인트 문자열 생성 - 맵 패닝/줌 후 재계산 필요
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

        // 패닝/줌 후 마커 위치 및 경로 재계산 (MainWindow에서 호출)
        public void RefreshMarkerPosition()
        {
            if (_mapRenderer == null || !_mapRenderer.IsMapLoaded) return;

            // 현재 프레임 마커 위치 갱신
            if (_records.Count > 0 && _currentIndex >= 0 && _currentIndex < _records.Count)
            {
                var r = _records[_currentIndex];
                var (cx, cy) = _mapRenderer.WorldToCanvas(r.Longitude, r.Latitude);
                MarkerX = cx;
                MarkerY = cy;
            }

            // 경로 전체 재계산
            BuildPathPoints();
        }

        // 취소 토큰 해제
        public void Dispose() => _cts?.Cancel();
    }
}