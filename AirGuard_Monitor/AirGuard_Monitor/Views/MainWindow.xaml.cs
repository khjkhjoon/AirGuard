using AirGuard.WPF.Map;
using AirGuard.WPF.ViewModels;
using HelixToolkit.Wpf;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace AirGuard.WPF.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _vm => (MainViewModel)DataContext;
        private bool _isDragging;
        private System.Windows.Point _lastMousePos;
        private MapRenderer? _mapRenderer;
        private DroneView3D? _droneView3D;
        private bool _is3DMode = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Loaded += (_, _) =>
            {
                // 2D 맵 초기화
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
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _vm.Dispose();
            base.OnClosing(e);
            Application.Current.Shutdown();
        }

        // ===== 2D/3D 전환 =====
        private void Toggle3DView(object sender, RoutedEventArgs e)
        {
            _is3DMode = !_is3DMode;
            MapContainer.Visibility = _is3DMode ? Visibility.Collapsed : Visibility.Visible;
            View3DContainer.Visibility = _is3DMode ? Visibility.Visible : Visibility.Collapsed;
            Btn3DToggle.Content = _is3DMode ? "2D MAP" : "3D VIEW";
            BtnTrack.Visibility = _is3DMode ? Visibility.Visible : Visibility.Collapsed;
            if (!_is3DMode) _droneView3D?.Clear();
        }

        // ===== 3D 추적 토글 =====
        private void ToggleTracking(object sender, RoutedEventArgs e)
        {
            if (_droneView3D == null) return;
            bool isNowTracking = _droneView3D.ToggleTracking();
            BtnTrack.Content = isNowTracking ? "📍 TRACK: ON" : "📍 TRACK: OFF";
            BtnTrack.Foreground = isNowTracking
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 136))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(122, 155, 181));
        }

        // ===== 드론 중심 정렬 =====
        private void CenterOnDrone_Click(object sender, RoutedEventArgs e)
        {
            var pos = _vm.GetSelectedDroneCanvasPos();
            if (pos == null) return;

            double dx = MapCanvas.ActualWidth / 2 - pos.Value.X;
            double dy = MapCanvas.ActualHeight / 2 - pos.Value.Y;
            _vm.PanMap(dx, dy);
        }

        // ===== 지도 인터랙션 =====
        // MapCanvas 위에 HelixViewport3D가 겹쳐있어 Canvas가 이벤트를 못 받는 경우가 있음
        // → MapContainer(부모 Border)에서 이벤트를 받아 처리
        private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(MapCanvas);
            _vm.UpdateMapCoordinate(pos);
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var delta = pos - _lastMousePos;
                _vm.PanMap(delta.X, delta.Y);
                _lastMousePos = pos;
            }
            e.Handled = true;
        }

        private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _vm.ZoomMap(e.Delta > 0 ? 1.05 : 0.95);
            e.Handled = true;
        }

        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(MapCanvas);
            // Canvas 대신 MapContainer에서 캡처 — HelixViewport3D 이벤트 차단
            MapContainer.CaptureMouse();
            e.Handled = true;
        }

        private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            MapContainer.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}