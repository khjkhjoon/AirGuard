using AirGuard.WPF.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace AirGuard.WPF.ViewModels
{
    public class VehicleViewModel : BaseViewModel
    {
        // 배터리 바 너비 (최대 180px 기준)
        public double BatteryBarWidth => (_battery / 100.0) * 180.0;

        // ===== 정적 브러시 캐시 — 매번 new SolidColorBrush 안 함 =====
        private static readonly Brush BrushActive = new SolidColorBrush(Color.FromRgb(0, 255, 136));
        private static readonly Brush BrushIdle = new SolidColorBrush(Color.FromRgb(122, 155, 181));
        private static readonly Brush BrushEmergency = new SolidColorBrush(Color.FromRgb(255, 59, 59));
        private static readonly Brush BrushBlue = new SolidColorBrush(Color.FromRgb(13, 127, 255));
        private static readonly Brush BrushBattGreen = new SolidColorBrush(Color.FromRgb(0, 255, 136));
        private static readonly Brush BrushBattOrange = new SolidColorBrush(Color.FromRgb(255, 140, 0));
        private static readonly Brush BrushBattRed = new SolidColorBrush(Color.FromRgb(255, 59, 59));
        private static readonly Brush BadgeActive = new SolidColorBrush(Color.FromRgb(0, 100, 50));
        private static readonly Brush BadgeIdle = new SolidColorBrush(Color.FromRgb(40, 60, 80));
        private static readonly Brush BadgeEmergency = new SolidColorBrush(Color.FromRgb(120, 20, 20));
        private static readonly Brush BadgeBlue = new SolidColorBrush(Color.FromRgb(10, 60, 120));
        private static readonly Brush SelectionOn = new SolidColorBrush(Color.FromArgb(60, 13, 127, 255));
        private static readonly Brush SelectionOff = new SolidColorBrush(Colors.Transparent);

        static VehicleViewModel()
        {
            // 브러시 동결 — WPF 렌더링 스레드에서 바로 쓸 수 있게
            BrushActive.Freeze(); BrushIdle.Freeze();
            BrushEmergency.Freeze(); BrushBlue.Freeze();
            BrushBattGreen.Freeze(); BrushBattOrange.Freeze(); BrushBattRed.Freeze();
            BadgeActive.Freeze(); BadgeIdle.Freeze();
            BadgeEmergency.Freeze(); BadgeBlue.Freeze();
            SelectionOn.Freeze(); SelectionOff.Freeze();
        }

        // ===== 필드 =====
        private string _vehicleId = "";
        private string _name = "";
        private double _latitude;
        private double _longitude;
        private double _altitude;
        private double _speed;
        private double _battery;
        private string _status = "Idle";
        private double _heading;
        private DateTime _timestamp;
        private bool _isSelected;
        private bool _isVisible = true;
        private double _mapX;
        private double _mapY;
        private string _pathPoints = "";
        private string _missionStatus = "STANDBY";

        private readonly List<Point> _pathHistory = new();

        // 텔레메트리 (그래프용)
        public ObservableCollection<TelemetryPoint> TelemetryHistory { get; } = new();

        // 경로 플레이백
        private readonly List<PathRecord> _pathRecords = new();
        public IReadOnlyList<PathRecord> PathRecords => _pathRecords;

        // ===== 프로퍼티 (값이 바뀔 때만 notify) =====
        public string VehicleId
        {
            get => _vehicleId;
            private set => SetProperty(ref _vehicleId, value);
        }
        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value);
        }
        public double Latitude
        {
            get => _latitude;
            private set => SetProperty(ref _latitude, value);
        }
        public double Longitude
        {
            get => _longitude;
            private set => SetProperty(ref _longitude, value);
        }
        public double Altitude
        {
            get => _altitude;
            private set => SetProperty(ref _altitude, value);
        }
        public double Speed
        {
            get => _speed;
            private set
            {
                if (SetProperty(ref _speed, value))
                    OnPropertyChanged(nameof(SpeedText));
            }
        }
        public double Battery
        {
            get => _battery;
            private set
            {
                var prevTier = BatteryTier(_battery);
                if (SetProperty(ref _battery, value))
                {
                    OnPropertyChanged(nameof(BatteryBarWidth));
                    // 배터리 색상은 구간이 바뀔 때만 notify
                    if (BatteryTier(value) != prevTier)
                    {
                        OnPropertyChanged(nameof(BatteryColor));
                        OnPropertyChanged(nameof(BatteryWarning));
                        OnPropertyChanged(nameof(BatteryWarningText));
                        OnPropertyChanged(nameof(BatteryWarningVisibility));
                    }
                }
            }
        }
        public string Status
        {
            get => _status;
            private set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusAccentColor));
                    OnPropertyChanged(nameof(StatusBadgeColor));
                    OnPropertyChanged(nameof(IsEmergency));
                    OnPropertyChanged(nameof(MissionStatus));
                }
            }
        }
        public double Heading
        {
            get => _heading;
            private set
            {
                if (SetProperty(ref _heading, value))
                    OnPropertyChanged(nameof(HeadingText));
            }
        }
        public DateTime Timestamp
        {
            get => _timestamp;
            private set
            {
                if (SetProperty(ref _timestamp, value))
                    OnPropertyChanged(nameof(TimestampText));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    OnPropertyChanged(nameof(SelectionBackground));
                    OnPropertyChanged(nameof(SelectionRingVisibility));
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                    OnPropertyChanged(nameof(IsVisibleVisibility));
            }
        }

        public string MissionStatus
        {
            get => Status switch
            {
                "Active" => "MISSION",
                "Emergency" => "EMERGENCY",
                _ => "STANDBY"
            };
        }

        public double MapX { get => _mapX; set => SetProperty(ref _mapX, value); }
        public double MapY { get => _mapY; set => SetProperty(ref _mapY, value); }

        public string PathPoints
        {
            get => _pathPoints;
            private set => SetProperty(ref _pathPoints, value);
        }

        // ===== 표시용 (계산 프로퍼티) =====
        public string TimestampText => Timestamp.ToString("HH:mm:ss");
        public string SpeedText => $"{Speed:F1} m/s";
        public string HeadingText => $"{Heading:F0}°";
        public bool IsEmergency => Status.Equals("Emergency", StringComparison.OrdinalIgnoreCase);
        public bool BatteryWarning => Battery <= 20;
        public string BatteryWarningText => Battery <= 10 ? "⚡ CRITICAL" : Battery <= 20 ? "⚡ LOW" : "";
        public Visibility BatteryWarningVisibility => BatteryWarning ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SelectionRingVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsVisibleVisibility => IsVisible ? Visibility.Visible : Visibility.Collapsed;

        // 비행 통계
        public string TotalDistanceText => $"{TotalDistanceM():F0} m";
        public string MaxAltitudeText => $"{(_pathRecords.Count > 0 ? MaxAlt() : 0):F1} m";
        public string AvgSpeedText => $"{(_pathRecords.Count > 0 ? AvgSpd() : 0):F1} m/s";

        // ===== 캐시된 브러시 반환 =====
        public Brush StatusAccentColor => StatusBrush(_status);
        public Brush StatusBadgeColor => BadgeBrush(_status);
        public Brush BatteryColor => BatteryTier(_battery) switch
        {
            2 => BrushBattGreen,
            1 => BrushBattOrange,
            _ => BrushBattRed
        };
        public Brush SelectionBackground => _isSelected ? SelectionOn : SelectionOff;

        // ===== 생성자 =====
        public VehicleViewModel(VehicleData data) => UpdateFrom(data);

        // ===== 업데이트 (최소 notify) =====
        public void UpdateFrom(VehicleData data)
        {
            VehicleId = data.VehicleId;
            Name = data.Name;
            Latitude = data.Latitude;
            Longitude = data.Longitude;
            Altitude = data.Altitude;
            Speed = data.Speed;
            Battery = data.Battery;
            Status = data.Status;
            Heading = data.Heading;
            Timestamp = data.Timestamp == default ? DateTime.Now : data.Timestamp;

            // TelemetryHistory, PathRecord 제거 — 별도 메서드로 분리
            _pathRecords.Add(new PathRecord
            {
                Time = Timestamp,
                Latitude = data.Latitude,
                Longitude = data.Longitude,
                Altitude = data.Altitude,
                Speed = data.Speed,
                Status = data.Status
            });
            if (_pathRecords.Count > 500) _pathRecords.RemoveAt(0);
        }

        public void UpdateTelemetry(VehicleData data)
        {
            TelemetryHistory.Add(new TelemetryPoint
            {
                Time = DateTime.Now,
                Battery = data.Battery,
                Speed = data.Speed,
                Altitude = data.Altitude
            });
            while (TelemetryHistory.Count > 60) TelemetryHistory.RemoveAt(0);
        }

        public void UpdateMapXY(double x, double y)
        {
            MapX = x; MapY = y;
            _pathHistory.Add(new Point(x, y));
            if (_pathHistory.Count > 60) _pathHistory.RemoveAt(0);

            var sb = new StringBuilder();
            foreach (var p in _pathHistory) sb.Append($"{p.X:F1},{p.Y:F1} ");
            PathPoints = sb.ToString().TrimEnd();
        }

        public void NotifyStatsChanged()
        {
            OnPropertyChanged(nameof(TotalDistanceText));
            OnPropertyChanged(nameof(MaxAltitudeText));
            OnPropertyChanged(nameof(AvgSpeedText));
        }

        public void ClearHistory()
        {
            TelemetryHistory.Clear();
            _pathRecords.Clear();
            _pathHistory.Clear();
            PathPoints = "";
        }

        // ===== 유틸 =====
        private static Brush StatusBrush(string status) => status switch
        {
            "Active" => BrushActive,
            "Idle" => BrushIdle,
            "Emergency" => BrushEmergency,
            _ => BrushBlue
        };
        private static Brush BadgeBrush(string status) => status switch
        {
            "Active" => BadgeActive,
            "Idle" => BadgeIdle,
            "Emergency" => BadgeEmergency,
            _ => BadgeBlue
        };
        private static int BatteryTier(double b) => b > 50 ? 2 : b > 20 ? 1 : 0;

        private double TotalDistanceM()
        {
            if (_pathRecords.Count < 2) return 0;
            double total = 0;
            for (int i = 1; i < _pathRecords.Count; i++)
                total += HaversineM(_pathRecords[i - 1].Latitude, _pathRecords[i - 1].Longitude,
                                    _pathRecords[i].Latitude, _pathRecords[i].Longitude);
            return total;
        }
        private double MaxAlt() { double m = 0; foreach (var p in _pathRecords) if (p.Altitude > m) m = p.Altitude; return m; }
        private double AvgSpd() { double s = 0; foreach (var p in _pathRecords) s += p.Speed; return s / _pathRecords.Count; }

        private static double HaversineM(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6_371_000;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}