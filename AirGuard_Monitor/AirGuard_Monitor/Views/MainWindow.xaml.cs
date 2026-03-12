using AirGuard.WPF.Map;
using AirGuard.WPF.ViewModels;
using HelixToolkit.Wpf;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AirGuard.WPF.Views
{
    /// <summary>
    /// 메인 윈도우 코드비하인드 - 지도 인터랙션, 웨이포인트 오버레이, 2D/3D 전환 처리
    /// </summary>
    public partial class MainWindow : Window
    {
        // 메인 뷰모델 참조
        private MainViewModel _vm => (MainViewModel)DataContext;
        // 마우스 드래그 여부
        private bool _isDragging;
        // 마지막 마우스 위치 (패닝 계산용)
        private System.Windows.Point _lastMousePos;
        // 2D 맵 렌더러
        private MapRenderer? _mapRenderer;
        // 3D 드론 뷰
        private DroneView3D? _droneView3D;
        // 현재 3D 모드 여부
        private bool _is3DMode = false;
        // 웨이포인트 편집 모드 여부
        private bool _isWaypointMode = false;
        // 웨이포인트 캔버스 오버레이 목록 (마커/선)
        private readonly List<UIElement> _wpOverlays = new();

        // 생성자 - 뷰모델/맵/3D 초기화 및 이벤트 구독
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Loaded += (_, _) =>
            {
                // 2D 맵 렌더러 초기화
                _mapRenderer = new MapRenderer(MapCanvas);
                _vm.SetMapRenderer(_mapRenderer);
                _vm.MapDataReceived += json =>
                {
                    var mapData = _mapRenderer.LoadFromJson(json);
                    if (mapData != null)
                        Dispatcher.BeginInvoke(() => _droneView3D?.LoadMap(mapData));
                };

                // 3D 뷰 초기화
                _droneView3D = new DroneView3D(Viewport3D);

                // 3D 드론 위치 업데이트 구독
                _vm.DronePositionUpdated += (id, name, lat, lon, alt, status) =>
                {
                    if (_is3DMode)
                        Dispatcher.BeginInvoke(() =>
                            _droneView3D?.UpdateDrone(id, name, lat, lon, alt, status));
                };

                // 미션 진행상황 수신 - 웨이포인트 상태 갱신 및 완료 처리
                _vm.MissionProgressReceived += (vehicleId, wpIndex) =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        int total = _vm.Mission.WaypointCount;
                        _vm.Mission.SetCurrentWaypoint(wpIndex);
                        UpdateWpOverlays();

                        // wpIndex > total 이면 미션 완료 → 500ms 후 오버레이 초기화
                        if (wpIndex > total)
                        {
                            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                                Dispatcher.BeginInvoke(() =>
                                {
                                    _vm.Mission.ClearWaypoints();
                                    foreach (var o in _wpOverlays) MapCanvas.Children.Remove(o);
                                    _wpOverlays.Clear();
                                    TxtWpCount.Text = "0 WP";
                                    UpdateWpOverlays();
                                }));
                        }
                    });
                };

                _vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.IsConnected) && !_vm.IsConnected)
                    {
                        foreach (var o in _wpOverlays) MapCanvas.Children.Remove(o);
                        _wpOverlays.Clear();
                        TxtWpCount.Text = "0 WP";
                        _droneView3D?.Clear();
                    }
                };
            };
        }

        // 윈도우 닫힐 때 뷰모델 리소스 해제
        protected override void OnClosing(CancelEventArgs e)
        {
            _vm.Dispose();
            base.OnClosing(e);
            Application.Current.Shutdown();
        }

        // 2D/3D 뷰 전환 토글
        private void Toggle3DView(object sender, RoutedEventArgs e)
        {
            _is3DMode = !_is3DMode;
            MapContainer.Visibility = _is3DMode ? Visibility.Collapsed : Visibility.Visible;
            View3DContainer.Visibility = _is3DMode ? Visibility.Visible : Visibility.Collapsed;
            Btn3DToggle.Content = _is3DMode ? "2D MAP" : "3D VIEW";
            BtnTrack.Visibility = _is3DMode ? Visibility.Visible : Visibility.Collapsed;
            if (!_is3DMode) _droneView3D?.Clear();
        }

        // 3D 카메라 추적 모드 토글
        private void ToggleTracking(object sender, RoutedEventArgs e)
        {
            if (_droneView3D == null) return;
            bool isNowTracking = _droneView3D.ToggleTracking();
            BtnTrack.Content = isNowTracking ? "📍 TRACK: ON" : "📍 TRACK: OFF";
            BtnTrack.Foreground = isNowTracking
                ? new SolidColorBrush(Color.FromRgb(0, 255, 136))
                : new SolidColorBrush(Color.FromRgb(122, 155, 181));
        }

        // 선택된 드론을 지도 중앙으로 이동
        private void CenterOnDrone_Click(object sender, RoutedEventArgs e)
        {
            var pos = _vm.GetSelectedDroneCanvasPos();
            if (pos == null) return;
            double dx = MapCanvas.ActualWidth / 2 - pos.Value.X;
            double dy = MapCanvas.ActualHeight / 2 - pos.Value.Y;
            _vm.PanMap(dx, dy);
        }

        // 마우스 이동 - 좌표 갱신 및 드래그 패닝 처리
        private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(MapCanvas);
            _vm.UpdateMapCoordinate(pos);
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var delta = pos - _lastMousePos;
                _vm.PanMap(delta.X, delta.Y);
                _lastMousePos = pos;
                RedrawWpOverlays();
                _vm.Playback.RefreshMarkerPosition();
            }
            e.Handled = true;
        }

        // 마우스 휠 - 줌 처리 및 오버레이 재계산
        private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _vm.ZoomMap(e.Delta > 0 ? 1.05 : 0.95);
            RedrawWpOverlays();
            _vm.Playback.RefreshMarkerPosition();
            e.Handled = true;
        }

        // 웨이포인트 편집 모드 토글
        private void ToggleWaypointMode(object sender, RoutedEventArgs e)
        {
            _isWaypointMode = !_isWaypointMode;
            _vm.Mission.IsEditMode = _isWaypointMode;
            BtnWaypoint.Content = _isWaypointMode ? "✏ 편집 중" : "✏ WAYPOINT";
            BtnWaypoint.Foreground = _isWaypointMode
                ? new SolidColorBrush(Color.FromRgb(255, 179, 0))
                : new SolidColorBrush(Color.FromRgb(180, 130, 0));
            BtnWaypoint.Background = _isWaypointMode
                ? new SolidColorBrush(Color.FromRgb(40, 25, 0))
                : new SolidColorBrush(Color.FromRgb(12, 24, 32));
        }

        // 지도 클릭 - 웨이포인트 추가 또는 드래그 시작
        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(MapCanvas);

            if (_isWaypointMode && !_is3DMode)
            {
                // 고도 입력값 읽기
                if (double.TryParse(TxtDefaultAltitude.Text, out double alt))
                    _vm.Mission.DefaultAltitude = alt;

                // 캔버스 좌표 → 유니티 월드 좌표 변환 후 웨이포인트 추가
                var (worldX, worldZ) = _vm.CanvasToWorld(pos.X, pos.Y);
                var wp = _vm.Mission.AddWaypoint(worldX, worldZ, pos.X, pos.Y);
                AddWpMarker(wp);
                UpdateWpOverlays();
                TxtWpCount.Text = $"{_vm.Mission.WaypointCount} WP";
                e.Handled = true;
                return;
            }

            // 드래그 패닝 시작
            _isDragging = true;
            _lastMousePos = pos;
            MapContainer.CaptureMouse();
            e.Handled = true;
        }

        // 웨이포인트 마커(원형 + 번호 레이블)를 캔버스에 추가
        private void AddWpMarker(WaypointItem wp)
        {
            // 원형 마커
            var circle = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromRgb(255, 179, 0)),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 220, 100)),
                StrokeThickness = 1.5,
                Tag = wp,
            };
            Canvas.SetLeft(circle, wp.CanvasX - 7);
            Canvas.SetTop(circle, wp.CanvasY - 7);
            Canvas.SetZIndex(circle, 10);

            // 번호 레이블
            var label = new TextBlock
            {
                Text = wp.Index.ToString(),
                FontSize = 8,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = Brushes.Black,
                Tag = wp,
            };
            Canvas.SetLeft(label, wp.CanvasX - 4);
            Canvas.SetTop(label, wp.CanvasY - 6);
            Canvas.SetZIndex(label, 11);

            MapCanvas.Children.Add(circle);
            MapCanvas.Children.Add(label);
            _wpOverlays.Add(circle);
            _wpOverlays.Add(label);
        }

        // 웨이포인트 간 연결선 갱신 (기존 선 제거 후 재생성)
        private void UpdateWpOverlays()
        {
            var lines = _wpOverlays.OfType<Line>().ToList();
            foreach (var l in lines) { MapCanvas.Children.Remove(l); _wpOverlays.Remove(l); }

            var wps = _vm.Mission.Waypoints.ToList();
            for (int i = 1; i < wps.Count; i++)
            {
                var prev = wps[i - 1];
                var curr = wps[i];
                var line = new Line
                {
                    X1 = prev.CanvasX,
                    Y1 = prev.CanvasY,
                    X2 = curr.CanvasX,
                    Y2 = curr.CanvasY,
                    Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 179, 0)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                };
                Canvas.SetZIndex(line, 9);
                MapCanvas.Children.Add(line);
                _wpOverlays.Add(line);
            }
        }

        // 웨이포인트 삭제 버튼 클릭 - 마커/선 제거 및 인덱스 갱신
        private void RemoveWaypoint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is WaypointItem wp)
            {
                _vm.Mission.RemoveWaypoint(wp);
                var toRemove = _wpOverlays.Where(o => o is FrameworkElement fe && fe.Tag == wp).ToList();
                foreach (var o in toRemove) { MapCanvas.Children.Remove(o); _wpOverlays.Remove(o); }
                // 남은 마커 번호 텍스트 갱신
                foreach (var o in _wpOverlays.OfType<TextBlock>())
                    if (o.Tag is WaypointItem w) o.Text = w.Index.ToString();
                UpdateWpOverlays();
                TxtWpCount.Text = $"{_vm.Mission.WaypointCount} WP";
            }
        }

        // 미션 전송 버튼 클릭 - 유효성 검사 후 JSON 전송
        private void SendMission_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Mission.Waypoints.Count == 0)
            {
                MessageBox.Show("웨이포인트를 먼저 찍어주세요.", "AirGuard",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_vm.SelectedVehicle == null)
            {
                MessageBox.Show("드론을 먼저 선택해주세요.", "AirGuard",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string json = _vm.Mission.ToJson(_vm.SelectedVehicle.VehicleId);
            _vm.SendRaw(json);
            _vm.Mission.MissionStatus = "전송됨";
        }

        // 패닝/줌 후 웨이포인트 오버레이 위치 재계산
        private void RedrawWpOverlays()
        {
            if (_mapRenderer == null) return;
            foreach (var wp in _vm.Mission.Waypoints)
            {
                var (cx, cy) = _mapRenderer.WorldToCanvas(wp.WorldX, wp.WorldZ);
                wp.CanvasX = cx;
                wp.CanvasY = cy;

                foreach (var o in _wpOverlays)
                {
                    if (o is FrameworkElement fe && fe.Tag == wp)
                    {
                        if (o is Ellipse)
                        {
                            Canvas.SetLeft(o, cx - 7);
                            Canvas.SetTop(o, cy - 7);
                        }
                        else if (o is TextBlock)
                        {
                            Canvas.SetLeft(o, cx - 4);
                            Canvas.SetTop(o, cy - 6);
                        }
                    }
                }
            }
            UpdateWpOverlays();
        }

        // 미션 초기화 버튼 클릭 - 웨이포인트 및 오버레이 전체 제거
        private void ClearMission_Click(object sender, RoutedEventArgs e)
        {
            _vm.Mission.ClearWaypoints();
            foreach (var o in _wpOverlays) MapCanvas.Children.Remove(o);
            _wpOverlays.Clear();
            TxtWpCount.Text = "0 WP";
        }

        // 마우스 버튼 업 - 드래그 종료
        private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            MapContainer.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}