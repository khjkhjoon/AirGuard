using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AirGuard.WPF.Map
{
    // ===== 맵 데이터 모델 =====
    public class MapObjectData
    {
        public string Tag  { get; set; } = "";
        public float  X    { get; set; }
        public float  Y    { get; set; }
        public float  H    { get; set; }
        public float  Sx   { get; set; }
        public float  Sy   { get; set; }
        public float  Rot  { get; set; }
    }

    public class MapData
    {
        public string               Type    { get; set; } = "";
        public float                OriginX { get; set; }
        public float                OriginY { get; set; }
        public float                Width   { get; set; }
        public float                Height  { get; set; }
        public List<MapObjectData>  Objects { get; set; } = new();
    }

    // ===== 맵 렌더러 =====
    public class MapRenderer
    {
        private readonly Canvas _canvas;
        private MapData?  _mapData;
        private double    _scaleX, _scaleY;
        private double    _canvasW, _canvasH;

        // 태그별 색상
        private static readonly Dictionary<string, (Brush fill, Brush stroke)> TagColors = new()
        {
            ["Building"] = (new SolidColorBrush(Color.FromRgb(30,  60,  90)),
                            new SolidColorBrush(Color.FromRgb(50, 100, 150))),
            ["Road"]     = (new SolidColorBrush(Color.FromRgb(20,  28,  38)),
                            new SolidColorBrush(Color.FromRgb(35,  50,  65))),
            ["Nature"]   = (new SolidColorBrush(Color.FromRgb(15,  45,  25)),
                            new SolidColorBrush(Color.FromRgb(25,  70,  40))),
            ["Prop"]     = (new SolidColorBrush(Color.FromRgb(40,  40,  55)),
                            new SolidColorBrush(Color.FromRgb(60,  60,  80))),
            ["Vehicle"]  = (new SolidColorBrush(Color.FromRgb(60,  80,  40)),
                            new SolidColorBrush(Color.FromRgb(90, 120,  60))),
        };

        static MapRenderer()
        {
            foreach (var (fill, stroke) in TagColors.Values)
            {
                fill.Freeze(); stroke.Freeze();
            }
        }

        public MapRenderer(Canvas canvas)
        {
            _canvas = canvas;
            _canvasW = canvas.ActualWidth  > 0 ? canvas.ActualWidth  : 800;
            _canvasH = canvas.ActualHeight > 0 ? canvas.ActualHeight : 600;
            canvas.SizeChanged += (_, e) =>
            {
                _canvasW = e.NewSize.Width;
                _canvasH = e.NewSize.Height;
                if (_mapData != null) Render();
            };
        }

        public bool IsMapLoaded => _mapData != null;

        // JSON 파싱 후 렌더링
        public void LoadFromJson(string json)
        {
            try
            {
                _mapData = JsonSerializer.Deserialize<MapData>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (_mapData == null) return;

                // 스케일 계산 (패딩 20px)
                double pad = 20;
                _scaleX = (_canvasW - pad * 2) / (_mapData.Width  > 0 ? _mapData.Width  : 1);
                _scaleY = (_canvasH - pad * 2) / (_mapData.Height > 0 ? _mapData.Height : 1);
                // 비율 유지
                double scale = Math.Min(_scaleX, _scaleY);
                _scaleX = scale; _scaleY = scale;

                Render();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"맵 파싱 오류: {ex.Message}");
            }
        }

        // 좌표 변환: 유니티 월드 → Canvas 픽셀
        public (double cx, double cy) WorldToCanvas(double worldX, double worldZ)
        {
            if (_mapData == null) return (0, 0);
            double pad = 20;
            double cx = pad + (worldX - _mapData.OriginX) * _scaleX;
            double cy = _canvasH - pad - (worldZ - _mapData.OriginY) * _scaleY; // Y 반전
            return (cx, cy);
        }

        private void Render()
        {
            if (_mapData == null) return;
            var toRemove = _canvas.Children
    .OfType<UIElement>()
    .Where(e => e is Rectangle r && (string)(r.Tag ?? "") == "map_obj")
    .ToList();
            foreach (var el in toRemove)
                _canvas.Children.Remove(el);

            // 배경
            _canvas.Background = new SolidColorBrush(Color.FromRgb(8, 12, 20));

            // 렌더 순서: Road → Nature → Prop → Building → Vehicle
            string[] order = { "Road", "Nature", "Prop", "Building", "Vehicle" };

            foreach (var tag in order)
            {
                foreach (var obj in _mapData.Objects)
                {
                    if (!obj.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)) continue;

                    var (cx, cy) = WorldToCanvas(obj.X, obj.Y);
                    double w = Math.Max(obj.Sx * _scaleX, 2);
                    double h = Math.Max(obj.Sy * _scaleY, 2);

                    if (!TagColors.TryGetValue(tag, out var colors))
                        colors = (Brushes.Gray, Brushes.White);

                    var rect = new Rectangle
                    {
                        Width           = w,
                        Height          = h,
                        Fill            = colors.fill,
                        Stroke          = colors.stroke,
                        StrokeThickness = tag == "Building" ? 0.8 : 0.3,
                    };

                    // 회전
                    if (Math.Abs(obj.Rot) > 0.5)
                    {
                        rect.RenderTransformOrigin = new Point(0.5, 0.5);
                        rect.RenderTransform = new RotateTransform(-obj.Rot);
                    }

                    Canvas.SetLeft(rect, cx - w / 2);
                    Canvas.SetTop(rect,  cy - h / 2);
                    _canvas.Children.Add(rect);
                }
            }
        }
    }
}