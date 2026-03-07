using AirGuard.WPF.Models;
using OxyPlot;
using AirGuard.WPF.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AirGuard.WPF.Map;

namespace AirGuard.WPF.ViewModels
{
    public class MainViewModel : BaseViewModel, IDisposable
    {
        private readonly TcpClientService _tcpService = new();
        private readonly DatabaseService _db = App.Database;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _uiTimer;
        private readonly DispatcherTimer _flashTimer;

        // ===== 백그라운드 버퍼 =====
        private readonly ConcurrentDictionary<string, VehicleData> _latestData = new();
        private readonly ConcurrentQueue<(string type, string title, string msg, string unitId)> _pendingAlerts = new();
        private readonly ConcurrentQueue<(string level, string msg)> _pendingLogs = new();

        // ===== 연결 =====
        private string _serverAddress = "127.0.0.1";
        private string _serverPort = "9000";
        private string _statusText = "OFFLINE";
        private bool _isConnected;
        private bool _isDisconnecting;

        // ===== 지도 =====
        private double _mapOffsetX;
        private double _mapOffsetY;
        private double _mapScale = 1.0;
        private double _mapCursorLat;
        private double _mapCursorLon;
        private AirGuard.WPF.Map.MapRenderer? _mapRenderer;
        public void SetMapRenderer(MapRenderer r)
        {
            _mapRenderer = r;
            Playback.SetMapRenderer(r);
        }
        // ===== 통계 =====
        private int _totalReceived;
        private int _receivedThisSecond;
        private int _uiTickCount;
        private string _dataRate = "0 msg/s";

        // ===== 필터 =====
        private string _searchQuery = "";
        private string _selectedFilter = "ALL";

        // ===== 선택 =====
        private VehicleViewModel? _selectedVehicle;

        // ===== 시계 =====
        private string _currentTime = "";
        private string _currentDate = "";

        // ===== 플래시 =====
        private bool _isFlashing;
        private int _flashCount;
        private Brush _flashBrush = new SolidColorBrush(Colors.Transparent);

        // ===== 세션 =====
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _vehicleSessions = new();

        // ===== CSV =====
        private StreamWriter? _csvWriter;
        private string _csvPath = "";
        private bool _isCsvLogging;
        private string _csvStatus = "CSV LOG: OFF";

        // ===== 그래프 =====
        public TelemetryGraphViewModel TelemetryGraph { get; } = new();

        // ===== 플레이백 =====
        public PlaybackViewModel Playback { get; }

        // ===== 세션 =====
        private DateTime _sessionStart = DateTime.Now;

        // ===== 컬렉션 =====
        public ObservableCollection<VehicleViewModel> Vehicles { get; } = new();
        public ObservableCollection<AlertEntry> Alerts { get; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public ObservableCollection<QuickStat> QuickStats { get; } = new();
        public ObservableCollection<string> FilterOptions { get; } = new()
            { "ALL", "ACTIVE", "IDLE", "EMERGENCY" };

        private readonly Dictionary<string, VehicleViewModel> _vehicleMap = new();

        // ===== 이벤트 ====="

        public event Action<string>? MapDataReceived;

        // ===== 프로퍼티 =====
        public string ServerAddress { get => _serverAddress; set => SetProperty(ref _serverAddress, value); }
        public string ServerPort { get => _serverPort; set => SetProperty(ref _serverPort, value); }
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectButtonText));
                OnPropertyChanged(nameof(ConnectButtonColor));
                OnPropertyChanged(nameof(ConnectionIndicatorColor));
            }
        }

        public string ConnectButtonText => IsConnected ? "DISCONNECT" : "CONNECT";
        public Brush ConnectButtonColor => IsConnected
            ? new SolidColorBrush(Color.FromRgb(180, 30, 30))
            : new SolidColorBrush(Color.FromRgb(13, 127, 255));
        public Brush ConnectionIndicatorColor => IsConnected
            ? new SolidColorBrush(Color.FromRgb(0, 255, 136))
            : new SolidColorBrush(Color.FromRgb(80, 80, 80));

        public string CurrentTime { get => _currentTime; private set => SetProperty(ref _currentTime, value); }
        public string CurrentDate { get => _currentDate; private set => SetProperty(ref _currentDate, value); }

        // 로그인 사용자 정보
        public string CurrentUserText =>
            $"{App.CurrentUser?.Name}  [{App.CurrentUser?.Role?.ToUpper()}]";
        public bool CanEdit => App.CurrentUser?.IsOperator == true;

        public string SearchQuery
        {
            get => _searchQuery;
            set { SetProperty(ref _searchQuery, value); ApplyFilter(); }
        }
        public string SelectedFilter
        {
            get => _selectedFilter;
            set { SetProperty(ref _selectedFilter, value); ApplyFilter(); }
        }

        public VehicleViewModel? SelectedVehicle
        {
            get => _selectedVehicle;
            set
            {
                if (_selectedVehicle != null) _selectedVehicle.IsSelected = false;
                SetProperty(ref _selectedVehicle, value);
                if (_selectedVehicle != null) _selectedVehicle.IsSelected = true;
                OnPropertyChanged(nameof(HasSelectedVehicle));
                OnPropertyChanged(nameof(SelectedVehicleVisibility));
                OnPropertyChanged(nameof(NoSelectionVisibility));
                // 그래프 소스 교체
                if (_selectedVehicle != null)
                {
                    TelemetryGraph.UpdateData(_selectedVehicle.TelemetryHistory);
                    _ = Playback.LoadRecordsAsync(_selectedVehicle.VehicleId, _selectedVehicle.Name);
                }
                else
                {
                    TelemetryGraph.Clear();
                }
            }
        }
        public bool HasSelectedVehicle => _selectedVehicle != null;
        public Visibility SelectedVehicleVisibility => HasSelectedVehicle ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NoSelectionVisibility => HasSelectedVehicle ? Visibility.Collapsed : Visibility.Visible;

        public Brush FlashOverlayBrush { get => _flashBrush; private set => SetProperty(ref _flashBrush, value); }
        public Visibility FlashOverlayVisibility => _isFlashing ? Visibility.Visible : Visibility.Collapsed;

        public string CsvStatus { get => _csvStatus; private set => SetProperty(ref _csvStatus, value); }
        public bool IsCsvLogging { get => _isCsvLogging; private set { SetProperty(ref _isCsvLogging, value); OnPropertyChanged(nameof(CsvButtonText)); OnPropertyChanged(nameof(CsvButtonColor)); } }
        public string CsvButtonText => IsCsvLogging ? "STOP LOG" : "START LOG";
        public Brush CsvButtonColor => IsCsvLogging
            ? new SolidColorBrush(Color.FromRgb(255, 140, 0))
            : new SolidColorBrush(Color.FromRgb(30, 80, 40));

        public string SessionDuration => $"SESSION: {(DateTime.Now - _sessionStart):hh\\:mm\\:ss}";
        public string MapCoordinateText => $"LAT {_mapCursorLat:F4}  LON {_mapCursorLon:F4}";
        public string MapZoomText => $"ZOOM {_mapScale:F1}x";
        public string VehicleCountText => $"{Vehicles.Count} UNITS TRACKED";
        public string LastUpdateText => $"LAST UPDATE: {DateTime.Now:HH:mm:ss}";
        public string TotalReceived => $"PACKETS: {_totalReceived:N0}";
        public string DataRate => _dataRate;

        public bool HasAlerts => Alerts.Count > 0;
        public int AlertCount => Alerts.Count;
        public Visibility AlertBadgeVisibility => HasAlerts ? Visibility.Visible : Visibility.Collapsed;

        // ===== 커맨드 =====
        public ICommand ToggleConnectCommand { get; }
        public ICommand SelectVehicleCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ToggleCsvCommand { get; }
        public ICommand ClearVehiclesCommand { get; }
        public ICommand ExportAlertsCommand { get; }

        // ===== 생성자 =====
        public MainViewModel()
        {
            Playback = new PlaybackViewModel(_db);

            _clockTimer = new DispatcherTimer(DispatcherPriority.Background);
            _flashTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _uiTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _uiTimer.Tick += OnUiTick;

            ToggleConnectCommand = new RelayCommand(async _ =>
            {
                if (IsConnected) DoDisconnect();
                else await DoConnectAsync();
            });
            SelectVehicleCommand = new RelayCommand(_ =>
            {
                if (_ is VehicleViewModel vm) SelectedVehicle = vm;
                return Task.CompletedTask;
            });
            ClearLogCommand = new RelayCommand(_ =>
            {
                LogEntries.Clear();
                return Task.CompletedTask;
            });
            ToggleCsvCommand = new RelayCommand(_ =>
            {
                if (IsCsvLogging) StopCsvLogging();
                else StartCsvLogging();
                return Task.CompletedTask;
            });
            ClearVehiclesCommand = new RelayCommand(_ =>
            {
                Vehicles.Clear();
                _vehicleMap.Clear();
                _latestData.Clear();
                OnPropertyChanged(nameof(VehicleCountText));
                _pendingLogs.Enqueue(("INFO", "유닛 목록 초기화됨"));
                return Task.CompletedTask;
            });
            ExportAlertsCommand = new RelayCommand(_ =>
            {
                ExportAlertsToCsv();
                return Task.CompletedTask;
            });

            _tcpService.MessageReceived += OnMessageReceived;
            _tcpService.Disconnected += OnDisconnected;
            _tcpService.ErrorOccurred += OnErrorOccurred;

            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, _) =>
            {
                CurrentTime = DateTime.Now.ToString("HH:mm:ss");
                CurrentDate = DateTime.Now.ToString("yyyy.MM.dd ddd").ToUpper();
                OnPropertyChanged(nameof(SessionDuration));
            };
            _clockTimer.Start();
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            CurrentDate = DateTime.Now.ToString("yyyy.MM.dd ddd").ToUpper();

            _flashTimer.Interval = TimeSpan.FromMilliseconds(300);
            _flashTimer.Tick += OnFlashTick;

            InitQuickStats();
            _uiTimer.Start();
            _pendingLogs.Enqueue(("INFO", "AirGuard 관제 시스템 시작됨"));
            _pendingLogs.Enqueue(("OK", $"로그인: {App.CurrentUser?.Name} ({App.CurrentUser?.Role})"));
        }

        // ===== UI 타이머 (200ms) =====
        private void OnUiTick(object? sender, EventArgs e)
        {
            _uiTickCount++;
            bool needStats = false;

            // 1) 드론 데이터 반영
            foreach (var key in _latestData.Keys.ToList())
            {
                if (!_latestData.TryRemove(key, out var data)) continue;

                if (_vehicleMap.TryGetValue(data.VehicleId, out var vm))
                {
                    vm.UpdateFrom(data);
                    UpdateDroneMapPosition(vm);
                    if (_selectedVehicle?.VehicleId == data.VehicleId)
                    {
                        vm.UpdateTelemetry(data);
                        TelemetryGraph.UpdateData(vm.TelemetryHistory);
                    }
                }
                else
                {
                    vm = new VehicleViewModel(data);
                    _vehicleMap[data.VehicleId] = vm;
                    Vehicles.Add(vm);
                    UpdateDroneMapPosition(vm);
                    ApplyFilter();
                    // 새 드론 등록 시 세션 시작
                    int sid = _db.StartSession(data.VehicleId);
                    _vehicleSessions[data.VehicleId] = sid;
                }
                needStats = true;
            }

            // 2) 알림 반영
            while (_pendingAlerts.TryDequeue(out var alert))
            {
                var severity = alert.type == "CRITICAL" ? AlertSeverity.Critical
                             : alert.type == "WARN" ? AlertSeverity.Warning
                             : AlertSeverity.Info;
                AddAlertInternal(alert.title, alert.msg, alert.unitId, severity);
                if (alert.type == "CRITICAL") { TriggerEmergencyFlash(); PlayAlertSound(); }
                else if (alert.type == "WARN") PlayAlertSound();
            }

            // 3) 로그 반영
            while (_pendingLogs.TryDequeue(out var log))
                AddLogInternal(log.level, log.msg);

            // 4) 1초마다 (5틱) — 숫자 UI 갱신
            if (_uiTickCount % 5 == 0)
            {
                _dataRate = $"{_receivedThisSecond} msg/s";
                _receivedThisSecond = 0;
                OnPropertyChanged(nameof(DataRate));
                OnPropertyChanged(nameof(TotalReceived));
                OnPropertyChanged(nameof(LastUpdateText));
                OnPropertyChanged(nameof(VehicleCountText));
            }

            // 5초마다 (25틱) — DB 비행 로그 저장
            if (_uiTickCount % 25 == 0)
            {
                var snapshot = Vehicles.ToList();
                _ = Task.Run(() =>
                {
                    foreach (var v in snapshot)
                    {
                        _vehicleSessions.TryGetValue(v.VehicleId, out int sid);
                        _db.SaveFlightLog(v.VehicleId, v.Name,
                            v.Latitude, v.Longitude, v.Altitude,
                            v.Speed, v.Battery, v.Status, v.Heading, sid);
                    }
                });
            }

            // 1시간마다 (18000틱) — 오래된 로그 정리
            if (_uiTickCount % 18000 == 0)
            {
                _ = Task.Run(() => _db.CleanupOldLogs(30));
            }

            if (needStats) UpdateQuickStats();
        }

        // ===== 수신 =====
        private void OnMessageReceived(string json)
        {
            try
            {
                if (json.Contains("\"type\":\"map\"") ||
                   (json.Contains("originX") && json.Contains("objects")))
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                        MapDataReceived?.Invoke(json));
                    return;
                }

                var data = JsonSerializer.Deserialize<VehicleData>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data == null) return;

                System.Threading.Interlocked.Increment(ref _totalReceived);
                System.Threading.Interlocked.Increment(ref _receivedThisSecond);

                if (_latestData.TryGetValue(data.VehicleId, out var prev))
                {
                    if (!prev.Status.Equals(data.Status, StringComparison.OrdinalIgnoreCase))
                    {
                        if (data.Status.Equals("Emergency", StringComparison.OrdinalIgnoreCase))
                            _pendingAlerts.Enqueue(("CRITICAL", "⚠ EMERGENCY", $"{data.Name} 비상 상태!", data.VehicleId));
                        else
                            _pendingAlerts.Enqueue(("INFO", "STATUS", $"{data.Name}: {prev.Status} → {data.Status}", data.VehicleId));
                    }
                    if (prev.Battery > 20 && data.Battery <= 20)
                        _pendingAlerts.Enqueue(("WARN", "⚡ LOW BATTERY", $"{data.Name} 배터리 부족 ({data.Battery:F0}%)", data.VehicleId));
                    else if (prev.Battery > 10 && data.Battery <= 10)
                        _pendingAlerts.Enqueue(("CRITICAL", "⚡ CRITICAL BATT", $"{data.Name} 배터리 위험 ({data.Battery:F0}%)", data.VehicleId));
                }
                else
                {
                    _pendingAlerts.Enqueue(("INFO", "NEW UNIT", $"{data.Name} 관제 구역 진입", data.VehicleId));
                }

                _latestData[data.VehicleId] = data;

                if (_isCsvLogging && _csvWriter != null)
                {
                    try
                    {
                        _csvWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}," +
                            $"{data.VehicleId},{data.Name}," +
                            $"{data.Latitude:F6},{data.Longitude:F6}," +
                            $"{data.Altitude:F2},{data.Speed:F2}," +
                            $"{data.Battery:F2},{data.Status},{data.Heading:F1}");
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ===== 연결 =====
        private async Task DoConnectAsync()
        {
            try
            {
                AddLogInternal("INFO", $"연결 시도... {ServerAddress}:{ServerPort}");
                await _tcpService.ConnectAsync(ServerAddress, int.Parse(ServerPort));
                IsConnected = true;
                _isDisconnecting = false;
                StatusText = $"ONLINE  {ServerAddress}:{ServerPort}";
                AddLogInternal("OK", "연결 성공");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                StatusText = "CONNECTION FAILED";
                AddLogInternal("ERR", $"연결 실패: {ex.Message}");
                MessageBox.Show($"연결 실패: {ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DoDisconnect()
        {
            if (_isDisconnecting) return;
            _isDisconnecting = true;
            _tcpService.Disconnect();
            IsConnected = false;
            StatusText = "OFFLINE";
            AddLogInternal("WARN", "연결 해제됨");
            EndAllSessions();
            _isDisconnecting = false;
        }

        private void OnDisconnected()
        {
            if (_isDisconnecting) return;
            EndAllSessions();
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                IsConnected = false;
                StatusText = "DISCONNECTED";
            });
            _pendingLogs.Enqueue(("WARN", "서버 연결 끊김"));
            _pendingAlerts.Enqueue(("WARN", "DISCONNECTED", "서버 연결이 끊어졌습니다", "SYSTEM"));
        }

        private void EndAllSessions()
        {
            foreach (var kv in _vehicleSessions)
                _ = Task.Run(() => _db.EndSession(kv.Value));
            _vehicleSessions.Clear();
        }

        private void OnErrorOccurred(string message)
            => _pendingLogs.Enqueue(("ERR", $"수신 오류: {message}"));

        // ===== 지도 =====
        private void UpdateDroneMapPosition(VehicleViewModel vm)
        {
            if (_mapRenderer != null && _mapRenderer.IsMapLoaded)
            {
                var (cx, cy) = _mapRenderer.WorldToCanvas(vm.Longitude, vm.Latitude);
                vm.UpdateMapXY(cx, cy);
                return;
            }
            if (Vehicles.Count == 0) return;
            var first = Vehicles[0];
            double scale = 5000.0 * _mapScale;
            vm.UpdateMapXY(
                400 + (vm.Longitude - first.Longitude) * scale + _mapOffsetX,
                250 - (vm.Latitude - first.Latitude) * scale + _mapOffsetY);
        }

        public void UpdateMapCoordinate(System.Windows.Point pos)
        {
            if (Vehicles.Count == 0) return;
            var first = Vehicles[0];
            double scale = 5000.0 * _mapScale;
            _mapCursorLon = first.Longitude + (pos.X - 400) / scale;
            _mapCursorLat = first.Latitude - (pos.Y - 250) / scale;
            OnPropertyChanged(nameof(MapCoordinateText));
        }

        public void PanMap(double dx, double dy)
        {
            _mapOffsetX += dx; _mapOffsetY += dy;
            foreach (var v in Vehicles) UpdateDroneMapPosition(v);
        }

        public void ZoomMap(double factor)
        {
            _mapScale = Math.Max(0.1, Math.Min(50.0, _mapScale * factor));
            OnPropertyChanged(nameof(MapZoomText));
            foreach (var v in Vehicles) UpdateDroneMapPosition(v);
        }

        // ===== 필터 =====
        private void ApplyFilter()
        {
            var query = _searchQuery?.ToLower().Trim() ?? "";
            var filter = _selectedFilter ?? "ALL";
            foreach (var v in Vehicles)
            {
                v.IsVisible =
                    (filter == "ALL" || v.Status.Equals(filter, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(query) ||
                     v.Name.ToLower().Contains(query) ||
                     v.VehicleId.ToLower().Contains(query));
            }
        }

        // ===== 플래시 =====
        private void TriggerEmergencyFlash()
        {
            _flashCount = 0; _isFlashing = true;
            OnPropertyChanged(nameof(FlashOverlayVisibility));
            _flashTimer.Start();
        }

        private void OnFlashTick(object? sender, EventArgs e)
        {
            _flashCount++;
            FlashOverlayBrush = (_flashCount % 2 == 1)
                ? new SolidColorBrush(Color.FromArgb(80, 255, 30, 30))
                : new SolidColorBrush(Colors.Transparent);
            OnPropertyChanged(nameof(FlashOverlayVisibility));
            if (_flashCount >= 10)
            {
                _flashTimer.Stop(); _isFlashing = false;
                FlashOverlayBrush = new SolidColorBrush(Colors.Transparent);
                OnPropertyChanged(nameof(FlashOverlayVisibility));
            }
        }

        private static void PlayAlertSound() { try { SystemSounds.Beep.Play(); } catch { } }

        // ===== CSV =====
        private void StartCsvLogging()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                _csvPath = Path.Combine(desktop, $"airguard_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                _csvWriter = new StreamWriter(_csvPath, false, Encoding.UTF8) { AutoFlush = true };
                _csvWriter.WriteLine("Timestamp,VehicleId,Name,Latitude,Longitude,Altitude,Speed,Battery,Status,Heading");
                IsCsvLogging = true;
                CsvStatus = $"LOG ON: {System.IO.Path.GetFileName(_csvPath)}";
                AddLogInternal("OK", $"CSV 로그 시작: {_csvPath}");
            }
            catch (Exception ex) { AddLogInternal("ERR", $"CSV 실패: {ex.Message}"); }
        }

        private void StopCsvLogging()
        {
            try
            {
                _csvWriter?.Flush(); _csvWriter?.Close(); _csvWriter = null;
                IsCsvLogging = false; CsvStatus = "CSV LOG: OFF (저장됨)";
                AddLogInternal("WARN", $"CSV 저장: {_csvPath}");
            }
            catch (Exception ex) { AddLogInternal("ERR", $"CSV 종료 실패: {ex.Message}"); }
        }

        private void ExportAlertsToCsv()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"airguard_alerts_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                using var w = new StreamWriter(path, false, Encoding.UTF8);
                w.WriteLine("Time,Title,Message,UnitId");
                foreach (var a in Alerts)
                    w.WriteLine($"{a.Time},{a.Title},{a.Message},{a.UnitId}");
                AddLogInternal("OK", $"알림 내보내기: {path}");
                MessageBox.Show($"저장됨:\n{path}", "알림 내보내기",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { AddLogInternal("ERR", $"내보내기 실패: {ex.Message}"); }
        }

        // ===== 알림/로그 =====
        private void AddAlertInternal(string title, string msg, string unitId, AlertSeverity severity)
        {
            var color = severity switch
            {
                AlertSeverity.Critical => new SolidColorBrush(Color.FromRgb(255, 59, 59)),
                AlertSeverity.Warning => new SolidColorBrush(Color.FromRgb(255, 140, 0)),
                _ => new SolidColorBrush(Color.FromRgb(13, 127, 255))
            };
            Alerts.Insert(0, new AlertEntry
            {
                Title = title,
                Message = msg,
                UnitId = unitId,
                Time = DateTime.Now.ToString("HH:mm:ss"),
                AlertColor = color
            });
            while (Alerts.Count > 100) Alerts.RemoveAt(Alerts.Count - 1);
            OnPropertyChanged(nameof(HasAlerts));
            OnPropertyChanged(nameof(AlertCount));
            OnPropertyChanged(nameof(AlertBadgeVisibility));

            // DB 저장
            _db.SaveAlert(title, msg, unitId, severity.ToString());
        }

        private void AddLogInternal(string level, string message)
        {
            var color = level switch
            {
                "OK" => new SolidColorBrush(Color.FromRgb(0, 255, 136)),
                "WARN" => new SolidColorBrush(Color.FromRgb(255, 140, 0)),
                "ERR" => new SolidColorBrush(Color.FromRgb(255, 59, 59)),
                _ => new SolidColorBrush(Color.FromRgb(122, 155, 181))
            };
            void Insert()
            {
                LogEntries.Insert(0, new LogEntry
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    Level = $"[{level}]",
                    Message = message,
                    LevelColor = color
                });
                while (LogEntries.Count > 200) LogEntries.RemoveAt(LogEntries.Count - 1);
            }
            if (Application.Current?.Dispatcher.CheckAccess() == true) Insert();
            else Application.Current?.Dispatcher.BeginInvoke(Insert);
        }

        // ===== 통계 =====
        private void InitQuickStats()
        {
            QuickStats.Add(new QuickStat { Label = "ACTIVE", Value = "0", Color = new SolidColorBrush(Color.FromRgb(0, 255, 136)) });
            QuickStats.Add(new QuickStat { Label = "IDLE", Value = "0", Color = new SolidColorBrush(Color.FromRgb(122, 155, 181)) });
            QuickStats.Add(new QuickStat { Label = "EMERGENCY", Value = "0", Color = new SolidColorBrush(Color.FromRgb(255, 59, 59)) });
            QuickStats.Add(new QuickStat { Label = "LOW BATT", Value = "0", Color = new SolidColorBrush(Color.FromRgb(255, 140, 0)) });
        }

        private void UpdateQuickStats()
        {
            if (QuickStats.Count < 4) return;
            QuickStats[0].Value = Vehicles.Count(v => v.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)).ToString();
            QuickStats[1].Value = Vehicles.Count(v => v.Status.Equals("Idle", StringComparison.OrdinalIgnoreCase)).ToString();
            QuickStats[2].Value = Vehicles.Count(v => v.Status.Equals("Emergency", StringComparison.OrdinalIgnoreCase)).ToString();
            QuickStats[3].Value = Vehicles.Count(v => v.Battery <= 20).ToString();
        }

        public void Dispose()
        {
            _clockTimer.Stop();
            _uiTimer.Stop();
            _flashTimer.Stop();
            StopCsvLogging();
            _tcpService.Dispose();
            Playback.Dispose();
        }
    }

    public enum AlertSeverity { Info, Warning, Critical }
}