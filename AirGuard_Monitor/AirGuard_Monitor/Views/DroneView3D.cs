using AirGuard.WPF.Map;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace AirGuard.WPF.Views
{
    public class DroneView3D
    {
        private readonly HelixViewport3D _viewport;
        private readonly Dictionary<string, List<ModelVisual3D>> _droneVisuals = new();
        private readonly Dictionary<string, List<Point3D>> _dronePaths = new();
        private readonly Dictionary<string, TubeVisual3D> _pathVisuals = new();
        private readonly Dictionary<string, BillboardTextVisual3D> _labelVisuals = new();
        private readonly Dictionary<string, Point3D> _lastPositions = new();

        // 지형지물 비주얼 (태그별)
        private readonly List<ModelVisual3D> _mapVisuals = new();

        // 추적 모드
        public bool IsTracking { get; private set; } = true;
        private string? _trackingTarget = null;

        private double _originLat = double.NaN;
        private double _originLon = double.NaN;

        // 태그별 3D 색상
        private static readonly Dictionary<string, (Color fill, double fixedHeight)> TagStyle = new()
        {
            ["Building"] = (Color.FromRgb(45, 85, 130), -1),    // H 필드 그대로 사용
            ["Road"] = (Color.FromRgb(30, 38, 50), 0.3),  // 납작하게
            ["Nature"] = (Color.FromRgb(15, 45, 20), 0.3),  // 낮게, 어둡게
            ["Prop"] = (Color.FromRgb(70, 70, 90), -1),
            ["Vehicle"] = (Color.FromRgb(80, 110, 45), -1),
        };

        public DroneView3D(HelixViewport3D viewport)
        {
            _viewport = viewport;
            SetupScene();
        }

        private void SetupScene()
        {
            _viewport.Children.Add(new DefaultLights());

            // 그리드 대폭 확대 — 유니티 월드 스케일에 맞게
            _viewport.Children.Add(new GridLinesVisual3D
            {
                Width = 10000,
                Length = 10000,
                MajorDistance = 500,
                MinorDistance = 100,
                Thickness = 0.3,
                Fill = Brushes.DimGray
            });
            ResetCamera();
        }

        private void ResetCamera()
        {
            _viewport.Camera = new PerspectiveCamera
            {
                Position = new Point3D(0, 200, 300),
                LookDirection = new Vector3D(0, -100, -200),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 60
            };
        }

        // ===== 맵 원점 설정 (드론 좌표와 동기화) =====
        public void SetMapOrigin(double centerX, double centerZ)
        {
            // 맵 로드 시 드론 origin을 맵 중심으로 고정
            _originLat = centerZ;
            _originLon = centerX;
        }

        // ===== 지형지물 로드 =====
        /// <summary>
        /// 유니티 맵 JSON 파싱 결과(MapData)를 받아 3D 지형지물을 렌더링합니다.
        /// MainWindow에서 MapDataReceived 이벤트 시 호출하세요.
        /// </summary>
        public void LoadMap(MapData mapData)
        {
            foreach (var v in _mapVisuals)
                _viewport.Children.Remove(v);
            _mapVisuals.Clear();

            // originX/Y는 맵의 최솟값 → 맵 중심으로 변환해서 드론 좌표계와 맞춤
            // 드론은 첫 수신 위치를 origin으로 쓰므로, 맵도 중심 기준으로 상대좌표 사용
            double mapCenterX = mapData.OriginX + mapData.Width / 2f;
            double mapCenterZ = mapData.OriginY + mapData.Height / 2f;

            // 드론 origin을 맵 중심으로 동기화
            SetMapOrigin(mapCenterX, mapCenterZ);

            // 맵 전체가 보이도록 카메라 높이/거리 자동 조정
            double mapSize = Math.Max(mapData.Width, mapData.Height);
            double camDist = mapSize * 0.8;
            _viewport.Camera = new PerspectiveCamera
            {
                Position = new Point3D(0, camDist * 0.7, camDist * 0.7),
                LookDirection = new Vector3D(0, -camDist * 0.7, -camDist * 0.7),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 60
            };

            string[] order = { "Road", "Nature", "Prop", "Vehicle", "Building" };

            foreach (var tag in order)
            {
                if (!TagStyle.TryGetValue(tag, out var style)) continue;

                foreach (var obj in mapData.Objects)
                {
                    if (!obj.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)) continue;

                    // 유니티 좌표 → 맵 중심 기준 상대좌표
                    double x = (obj.X - mapCenterX);
                    double z = -(obj.Y - mapCenterZ);   // Z축 반전
                    double w = Math.Max(obj.Sx, 0.5);
                    double d = Math.Max(obj.Sy, 0.5);

                    // Road/Nature는 최소 폭 2 보장 (좁은 도로가 선처럼 보이는 현상 방지)
                    if (style.fixedHeight >= 0)
                    {
                        w = Math.Max(w, 2.0);
                        d = Math.Max(d, 2.0);
                    }

                    // 높이: Sh(bounds.size.y) 우선, 없으면 태그 고정값
                    double h;
                    if (style.fixedHeight >= 0)
                        h = style.fixedHeight;
                    else if (obj.Sh > 0.1f)
                        h = obj.Sh;
                    else
                        h = Math.Max(w, d) * 0.5;
                    h = Math.Max(h, 0.2);

                    var brush = new SolidColorBrush(style.fill);
                    brush.Freeze();

                    ModelVisual3D visual;

                    if (style.fixedHeight >= 0)
                    {
                        bool hasDirVec = Math.Abs(obj.Rx) > 0.01f || Math.Abs(obj.Rz) > 0.01f;

                        var rect = new RectangleVisual3D
                        {
                            Origin = new Point3D(x, 0.05, z),
                            Normal = new Vector3D(0, 1, 0),
                            // right 방향이 Width, forward 방향이 Length
                            Width = hasDirVec ? w : w,
                            Length = hasDirVec ? d : d,
                            Fill = brush,
                        };

                        if (hasDirVec)
                        {
                            // forward 벡터를 LengthDirection으로 설정 (유니티 Z축 반전)
                            rect.LengthDirection = new Vector3D(obj.Fx, 0, -obj.Fz);
                        }
                        else if (Math.Abs(obj.Rot) > 0.5)
                        {
                            rect.Transform = new RotateTransform3D(
                                new AxisAngleRotation3D(new Vector3D(0, 1, 0), -obj.Rot),
                                new Point3D(x, 0.05, z));
                        }
                        visual = rect;
                    }
                    else
                    {
                        // Building/Prop/Vehicle: BoxVisual3D
                        double baseY = obj.H >= 0 ? obj.H : 0;
                        var box = new BoxVisual3D
                        {
                            Center = new Point3D(x, baseY + h / 2.0, z),
                            Width = w,
                            Height = h,
                            Length = d,
                            Fill = brush,
                        };
                        if (Math.Abs(obj.Rot) > 0.5)
                        {
                            box.Transform = new RotateTransform3D(
                                new AxisAngleRotation3D(new Vector3D(0, 1, 0), -obj.Rot),
                                new Point3D(x, baseY + h / 2.0, z));
                        }
                        visual = box;
                    }

                    _viewport.Children.Add(visual);
                    _mapVisuals.Add(visual);
                }
            }
        }

        // ===== 드론 업데이트 =====
        public void UpdateDrone(string vehicleId, string name, double lat, double lon,
                                double altitude, string status)
        {
            // lat = 유니티 Z, lon = 유니티 X
            if (double.IsNaN(_originLat)) { _originLat = lat; _originLon = lon; }

            double x = (lon - _originLon);
            double y = altitude;
            double z = -(lat - _originLat);
            var pos = new Point3D(x, y, z);
            _lastPositions[vehicleId] = pos;

            var color = status switch
            {
                "Active" => Colors.LimeGreen,
                "Emergency" => Colors.OrangeRed,
                _ => Colors.DodgerBlue
            };

            // 기존 비주얼 제거
            if (_droneVisuals.TryGetValue(vehicleId, out var old))
            {
                foreach (var v in old) _viewport.Children.Remove(v);
                _droneVisuals.Remove(vehicleId);
            }
            if (_labelVisuals.TryGetValue(vehicleId, out var oldLabel))
            {
                _viewport.Children.Remove(oldLabel);
                _labelVisuals.Remove(vehicleId);
            }

            var sphere = new SphereVisual3D
            {
                Center = pos,
                Radius = 3,
                Fill = new SolidColorBrush(color)
            };

            var altLine = new LinesVisual3D
            {
                Points = new Point3DCollection { pos, new Point3D(pos.X, 0, pos.Z) },
                Color = Color.FromArgb(120, color.R, color.G, color.B),
                Thickness = 0.6
            };

            var label = new BillboardTextVisual3D
            {
                Text = $"{name}  {altitude:F1}m",
                Position = new Point3D(pos.X, pos.Y + 7, pos.Z),
                Foreground = new SolidColorBrush(color),
                FontSize = 11,
                FontWeight = System.Windows.FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(160, 6, 10, 18)),
                Padding = new System.Windows.Thickness(5, 2, 5, 2)
            };

            _viewport.Children.Add(sphere);
            _viewport.Children.Add(altLine);
            _viewport.Children.Add(label);
            _droneVisuals[vehicleId] = new List<ModelVisual3D> { sphere, altLine };
            _labelVisuals[vehicleId] = label;

            // 경로 누적
            if (!_dronePaths.ContainsKey(vehicleId))
                _dronePaths[vehicleId] = new List<Point3D>();
            _dronePaths[vehicleId].Add(pos);
            if (_dronePaths[vehicleId].Count > 400)
                _dronePaths[vehicleId].RemoveAt(0);

            RefreshPath(vehicleId, color);

            if (IsTracking) TrackDrones();
        }

        private void TrackDrones()
        {
            if (_lastPositions.Count == 0) return;

            Point3D center;
            if (_trackingTarget != null && _lastPositions.TryGetValue(_trackingTarget, out var tp))
                center = tp;
            else
            {
                double ax = _lastPositions.Values.Average(p => p.X);
                double ay = _lastPositions.Values.Average(p => p.Y);
                double az = _lastPositions.Values.Average(p => p.Z);
                center = new Point3D(ax, ay, az);
            }

            double camY = center.Y + 60;
            double camZ = center.Z + 100;
            double camX = center.X;

            if (_viewport.Camera is PerspectiveCamera cam)
            {
                cam.Position = new Point3D(camX, camY, camZ);
                cam.LookDirection = new Vector3D(
                    center.X - camX,
                    center.Y - camY,
                    center.Z - camZ);
            }
        }

        private void RefreshPath(string vehicleId, Color color)
        {
            if (_pathVisuals.TryGetValue(vehicleId, out var old))
            {
                _viewport.Children.Remove(old);
                _pathVisuals.Remove(vehicleId);
            }
            var pts = _dronePaths[vehicleId];
            if (pts.Count < 2) return;

            var tube = new TubeVisual3D
            {
                Path = new Point3DCollection(pts),
                Diameter = 0.8,
                Fill = new SolidColorBrush(Color.FromArgb(160, color.R, color.G, color.B))
            };
            _viewport.Children.Add(tube);
            _pathVisuals[vehicleId] = tube;
        }

        public bool ToggleTracking()
        {
            IsTracking = !IsTracking;
            if (IsTracking) TrackDrones();
            return IsTracking;
        }

        public void SetTrackingTarget(string? vehicleId)
        {
            _trackingTarget = vehicleId;
            IsTracking = true;
            TrackDrones();
        }

        public void RemoveDrone(string vehicleId)
        {
            if (_droneVisuals.TryGetValue(vehicleId, out var visuals))
            {
                foreach (var v in visuals) _viewport.Children.Remove(v);
                _droneVisuals.Remove(vehicleId);
            }
            if (_pathVisuals.TryGetValue(vehicleId, out var path))
            {
                _viewport.Children.Remove(path);
                _pathVisuals.Remove(vehicleId);
            }
            if (_labelVisuals.TryGetValue(vehicleId, out var label))
            {
                _viewport.Children.Remove(label);
                _labelVisuals.Remove(vehicleId);
            }
            _dronePaths.Remove(vehicleId);
            _lastPositions.Remove(vehicleId);
        }

        public void Clear()
        {
            foreach (var list in _droneVisuals.Values)
                foreach (var v in list) _viewport.Children.Remove(v);
            foreach (var t in _pathVisuals.Values)
                _viewport.Children.Remove(t);
            foreach (var l in _labelVisuals.Values)
                _viewport.Children.Remove(l);
            foreach (var m in _mapVisuals)
                _viewport.Children.Remove(m);

            _droneVisuals.Clear();
            _dronePaths.Clear();
            _pathVisuals.Clear();
            _labelVisuals.Clear();
            _lastPositions.Clear();
            _mapVisuals.Clear();
            _originLat = double.NaN;
            _originLon = double.NaN;
            ResetCamera();
        }
    }
}