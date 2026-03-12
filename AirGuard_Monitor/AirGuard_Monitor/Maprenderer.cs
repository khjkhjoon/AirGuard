using System;
using System.Collections.Generic;
using System.Linq;
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
        public string Tag { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float H { get; set; }
        public float Sh { get; set; }
        public float Sx { get; set; }
        public float Sy { get; set; }
        public float Rot { get; set; }
        public float Fx { get; set; }  // forward.x
        public float Fz { get; set; }  // forward.z
        public float Rx { get; set; }  // right.x
        public float Rz { get; set; }  // right.z
    }

    public class MapData
    {
        public string Type { get; set; } = "";
        public float OriginX { get; set; }
        public float OriginY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public List<MapObjectData> Objects { get; set; } = new();
    }

    // ===== 맵 렌더러 =====
    public class MapRenderer
    {
        private readonly Canvas _canvas;
        private MapData? _mapData;
        private double _scaleX, _scaleY;
        private double _canvasW, _canvasH;

        // 패닝 오프셋 (맵 오브젝트 + 드론 좌표에 공통 적용)
        private double _panOffsetX = 0;
        private double _panOffsetY = 0;

        // 태그별 색상
        private static readonly Dictionary<string, (Brush fill, Brush stroke)> TagColors = new()
        {
            ["Building"] = (new SolidColorBrush(Color.FromRgb(30, 60, 90)),
                            new SolidColorBrush(Color.FromRgb(50, 100, 150))),
            ["Road"] = (new SolidColorBrush(Color.FromRgb(20, 28, 38)),
                            new SolidColorBrush(Color.FromRgb(35, 50, 65))),
            ["Nature"] = (new SolidColorBrush(Color.FromRgb(15, 45, 25)),
                            new SolidColorBrush(Color.FromRgb(25, 70, 40))),
            ["Prop"] = (new SolidColorBrush(Color.FromRgb(40, 40, 55)),
                            new SolidColorBrush(Color.FromRgb(60, 60, 80))),
            ["Vehicle"] = (new SolidColorBrush(Color.FromRgb(60, 80, 40)),
                            new SolidColorBrush(Color.FromRgb(90, 120, 60))),
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
            _canvasW = canvas.ActualWidth > 0 ? canvas.ActualWidth : 800;
            _canvasH = canvas.ActualHeight > 0 ? canvas.ActualHeight : 600;
            canvas.SizeChanged += (_, e) =>
            {
                _canvasW = e.NewSize.Width;
                _canvasH = e.NewSize.Height;
                if (_mapData != null) Render();
            };
        }

        public bool IsMapLoaded => _mapData != null;

        /// <summary>
        /// 맵 전체(오브젝트 포함)를 dx, dy 만큼 이동합니다.
        /// MainViewModel.PanMap 에서 호출하세요.
        /// </summary>
        public void Pan(double dx, double dy)
        {
            _panOffsetX += dx;
            _panOffsetY += dy;

            // 렌더된 map_obj 들을 일괄 이동 (전체 리렌더 없이 빠르게 처리)
            foreach (UIElement el in _canvas.Children)
            {
                if (el is Rectangle r && (string)(r.Tag ?? "") == "map_obj")
                {
                    Canvas.SetLeft(r, Canvas.GetLeft(r) + dx);
                    Canvas.SetTop(r, Canvas.GetTop(r) + dy);
                }
            }
        }

        /// <summary>패닝 오프셋 초기화 (맵 재로드 시 자동 호출됨)</summary>
        public void ResetPan()
        {
            _panOffsetX = 0;
            _panOffsetY = 0;
        }

        /// <summary>
        /// 유니티 월드 좌표 → Canvas 픽셀 좌표 변환.
        /// 패닝 오프셋이 포함되어 있으므로 드론 위치에 별도 오프셋 불필요.
        /// </summary>
        public (double cx, double cy) WorldToCanvas(double worldX, double worldZ)
        {
            if (_mapData == null) return (0, 0);
            double pad = 20;
            double cx = pad + (worldX - _mapData.OriginX) * _scaleX + _panOffsetX;
            double cy = _canvasH - pad - (worldZ - _mapData.OriginY) * _scaleY + _panOffsetY;
            return (cx, cy);
        }

        /// <summary>
        /// 캔버스 픽셀 좌표 → 유니티 월드 좌표 (WebToCanvas 역변환)
        /// </summary>
        public (double worldX, double worldZ) CanvasToWorld(double cx, double cy)
        {
            if (_mapData == null) return (0, 0);
            double pad = 20;
            double worldX = (cx - pad - _panOffsetX) / _scaleX + _mapData.OriginX;
            double worldZ = (_canvasH - pad - cy - _panOffsetY) / _scaleY + _mapData.OriginY;
            return (worldX, worldZ);
        }

        public bool HasMapData => _mapData != null;

        // JSON 파싱 후 렌더링
        public MapData? LoadFromJson(string json)
        {
            try
            {
                _mapData = JsonSerializer.Deserialize<MapData>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (_mapData == null) return null;

                ResetPan(); // 맵 재로드 시 패닝 초기화

                double pad = 20;
                _scaleX = (_canvasW - pad * 2) / (_mapData.Width > 0 ? _mapData.Width : 1);
                _scaleY = (_canvasH - pad * 2) / (_mapData.Height > 0 ? _mapData.Height : 1);
                double scale = Math.Min(_scaleX, _scaleY);
                _scaleX = scale; _scaleY = scale;

                Render();
                return _mapData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"맵 파싱 오류: {ex.Message}");
                return null;
            }
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

            _canvas.Background = new SolidColorBrush(Color.FromRgb(8, 12, 20));

            string[] order = { "Road", "Nature", "Prop", "Building", "Vehicle" };

            foreach (var tag in order)
            {
                foreach (var obj in _mapData.Objects)
                {
                    if (!obj.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)) continue;

                    var (cx, cy) = WorldToCanvas(obj.X, obj.Y); // 패닝 오프셋 포함
                    double w = Math.Max(obj.Sx * _scaleX, 2);
                    double h = Math.Max(obj.Sy * _scaleY, 2);

                    if (!TagColors.TryGetValue(tag, out var colors))
                        colors = (Brushes.Gray, Brushes.White);

                    var rect = new Rectangle
                    {
                        Width = w,
                        Height = h,
                        Fill = colors.fill,
                        Stroke = colors.stroke,
                        StrokeThickness = tag == "Building" ? 0.8 : 0.3,
                        Tag = "map_obj", // Pan() 에서 식별용
                    };

                    if (Math.Abs(obj.Rot) > 0.5)
                    {
                        rect.RenderTransformOrigin = new Point(0.5, 0.5);
                        rect.RenderTransform = new RotateTransform(-obj.Rot);
                    }

                    Canvas.SetLeft(rect, cx - w / 2);
                    Canvas.SetTop(rect, cy - h / 2);
                    _canvas.Children.Add(rect);
                }
            }
        }

        // 맵 오브젝트 전체 제거 및 상태 초기화
        public void Clear()
        {
            // map_obj(지형) 제거
            var toRemove = _canvas.Children
                .OfType<UIElement>()
                .Where(e => e is Rectangle r && (string)(r.Tag ?? "") == "map_obj")
                .ToList();
            foreach (var el in toRemove)
                _canvas.Children.Remove(el);

            // 드론 마커/경로/레이블 등 나머지 동적 요소 전체 제거
            var dynamicElements = _canvas.Children
                .OfType<UIElement>()
                .Where(e => e is not Rectangle r || (string)(r.Tag ?? "") != "map_obj")
                .ToList();
            foreach (var el in dynamicElements)
                _canvas.Children.Remove(el);

            _canvas.Background = new SolidColorBrush(Color.FromRgb(6, 10, 18));
            _mapData = null;
            ResetPan();
        }
    }
}