using AirGuard.WPF.Map;
using AirGuard.WPF.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AirGuard.WPF.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _vm => (MainViewModel)DataContext;
        private bool _isDragging;
        private System.Windows.Point _lastMousePos;
        private MapRenderer? _mapRenderer;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            Loaded += (_, _) =>
            {
                _mapRenderer = new MapRenderer(MapCanvas);
                _vm.SetMapRenderer(_mapRenderer);
                _vm.MapDataReceived += json => _mapRenderer.LoadFromJson(json);
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _vm.Dispose();
            base.OnClosing(e);
        }

        // ===== 지도 인터랙션 =====

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
        }

        private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _vm.ZoomMap(e.Delta > 0 ? 1.1 : 0.9);
        }

        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(MapCanvas);
            MapCanvas.CaptureMouse();
        }

        private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            MapCanvas.ReleaseMouseCapture();
        }
    }
}