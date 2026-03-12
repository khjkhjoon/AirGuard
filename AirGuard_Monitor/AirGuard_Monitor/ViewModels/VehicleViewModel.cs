using AirGuard.WPF.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace AirGuard.WPF.ViewModels
{
    /// <summary>
    /// 드론 단일 유닛 뷰모델 - 상태/위치/배터리/바람/텔레메트리/경로 표시 관리
    /// </summary>
    public class VehicleViewModel : BaseViewModel
    {
        // 상태별 텍스트 색상 브러시 캐시
        private static readonly Brush BrushActive = new SolidColorBrush(Color.FromRgb(0, 255, 136));
        private static readonly Brush BrushIdle = new SolidColorBrush(Color.FromRgb(122, 155, 181));
        private static readonly Brush BrushEmergency = new SolidColorBrush(Color.FromRgb(255, 59, 59));
        private static readonly Brush BrushBlue = new SolidColorBrush(Color.FromRgb(13, 127, 255));
        // 배터리 색상 브러시 캐시
        private static readonly Brush BrushBattGreen = new SolidColorBrush(Color.FromRgb(0, 255, 136));
        private static readonly Brush BrushBattOrange = new SolidColorBrush(Color.FromRgb(255, 140, 0));
        private static readonly Brush BrushBattRed = new SolidColorBrush(Color.FromRgb(255, 59, 59));
        // 상태 배지 배경 브러시 캐시
        private static readonly Brush BadgeActive = new SolidColorBrush(Color.FromRgb(0, 100, 50));
        private static readonly Brush BadgeIdle = new SolidColorBrush(Color.FromRgb(40, 60, 80));
        private static readonly Brush BadgeEmergency = new SolidColorBrush(Color.FromRgb(120, 20, 20));
        private static readonly Brush BadgeBlue = new SolidColorBrush(Color.FromRgb(10, 60, 120));
        // 선택 오버레이 브러시 캐시
        private static readonly Brush SelectionOn = new SolidColorBrush(Color.FromArgb(60, 13, 127, 255));
        private static readonly Brush SelectionOff = new SolidColorBrush(Colors.Transparent);

        // 정적 생성자 - 브러시 동결 (WPF 렌더링 스레드에서 바로 쓸 수 있게)
        static VehicleViewModel()
        {
            BrushActive.Freeze(); BrushIdle.Freeze();
            BrushEmergency.Freeze(); BrushBlue.Freeze();
            BrushBattGreen.Freeze(); BrushBattOrange.Freeze(); BrushBattRed.Freeze();
            BadgeActive.Freeze(); BadgeIdle.Freeze();
            BadgeEmergency.Freeze(); BadgeBlue.Freeze();
            SelectionOn.Freeze(); SelectionOff.Freeze();
        }

        // 드론 고유 ID
        private string _vehicleId = "";
        // 드론 이름
        private string _name = "";
        // 위도
        private double _latitude;
        // 경도
        private double _longitude;
        // 고도 (m)
        private double _altitude;
        // 속도 (m/s)
        private double _speed;
        // 배터리 잔량 (%)
        private double _battery;
        // 상태 문자열
        private string _status = "Idle";
        // 기수 방향 (도)
        private double _heading;
        // 마지막 수신 시각
        private DateTime _timestamp;
        // 선택 여부
        private bool _isSelected;
        // 목록 표시 여부 (필터 적용)
        private bool _isVisible = true;
        // 지도 캔버스 X 좌표
        private double _mapX;
        // 지도 캔버스 Y 좌표
        private double _mapY;
        // 지도 경로 포인트 문자열
        private string _pathPoints = "";
        // 미션 상태 문자열
        private string _missionStatus = "STANDBY";
        // 바람 속도 (m/s)
        private double _windSpeed;
        // 바람 경고 등급
        private string _windAlert = "CALM";

        // 지도 경로 히스토리 (픽셀 좌표)
        private readonly List<Point> _pathHistory = new();

        // 텔레메트리 히스토리 (그래프용, 최대 60개)
        public ObservableCollection<TelemetryPoint> TelemetryHistory { get; } = new();

        // 비행 기록 (경로 플레이백용, 최대 500개)
        private readonly List<PathRecord> _pathRecords = new();
        public IReadOnlyList<PathRecord> PathRecords => _pathRecords;

        // 드론 ID 프로퍼티
        public string VehicleId
        {
            get => _vehicleId;
            private set => SetProperty(ref _vehicleId, value);
        }

        // 드론 이름 프로퍼티
        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value);
        }

        // 위도 프로퍼티
        public double Latitude
        {
            get => _latitude;
            private set => SetProperty(ref _latitude, value);
        }

        // 경도 프로퍼티
        public double Longitude
        {
            get => _longitude;
            private set => SetProperty(ref _longitude, value);
        }

        // 고도 프로퍼티
        public double Altitude
        {
            get => _altitude;
            private set => SetProperty(ref _altitude, value);
        }

        // 속도 프로퍼티 - 변경 시 SpeedText 연동
        public double Speed
        {
            get => _speed;
            private set
            {
                if (SetProperty(ref _speed, value))
                    OnPropertyChanged(nameof(SpeedText));
            }
        }

        // 배터리 프로퍼티 - 구간 변경 시에만 색상/경고 notify
        public double Battery
        {
            get => _battery;
            private set
            {
                var prevTier = BatteryTier(_battery);
                if (SetProperty(ref _battery, value))
                {
                    OnPropertyChanged(nameof(BatteryBarWidth));
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

        // 상태 프로퍼티 - 변경 시 색상/배지/비상 여부 연동
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

        // 기수 방향 프로퍼티 - 변경 시 HeadingText 연동
        public double Heading
        {
            get => _heading;
            private set
            {
                if (SetProperty(ref _heading, value))
                    OnPropertyChanged(nameof(HeadingText));
            }
        }

        // 바람 속도 프로퍼티
        public double WindSpeed
        {
            get => _windSpeed;
            private set => SetProperty(ref _windSpeed, value);
        }

        // 바람 경고 등급 프로퍼티 - 변경 시 색상/표시 여부 연동
        public string WindAlert
        {
            get => _windAlert;
            private set
            {
                if (SetProperty(ref _windAlert, value))
                {
                    OnPropertyChanged(nameof(WindAlertColor));
                    OnPropertyChanged(nameof(WindAlertVisibility));
                }
            }
        }

        // 바람 경고 색상 (등급별)
        public string WindAlertColor => _windAlert switch
        {
            "TURBULENCE" => "#FF4444",
            "MODERATE" => "#FFA500",
            _ => "#00FF88"
        };

        // 바람 경고 배지 표시 여부
        public string WindAlertVisibility => _windAlert == "CALM" ? "Collapsed" : "Visible";

        // 마지막 수신 시각 프로퍼티 - 변경 시 TimestampText 연동
        public DateTime Timestamp
        {
            get => _timestamp;
            private set
            {
                if (SetProperty(ref _timestamp, value))
                    OnPropertyChanged(nameof(TimestampText));
            }
        }

        // 선택 여부 프로퍼티 - 변경 시 배경/링 표시 연동
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

        // 목록 표시 여부 프로퍼티 - 필터 적용 시 변경
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                    OnPropertyChanged(nameof(IsVisibleVisibility));
            }
        }

        // 미션 상태 텍스트 (상태 기반 계산)
        public string MissionStatus
        {
            get => Status switch
            {
                "Active" => "MISSION",
                "Emergency" => "EMERGENCY",
                _ => "STANDBY"
            };
        }

        // 지도 X 좌표 프로퍼티
        public double MapX { get => _mapX; set => SetProperty(ref _mapX, value); }
        // 지도 Y 좌표 프로퍼티
        public double MapY { get => _mapY; set => SetProperty(ref _mapY, value); }

        // 지도 경로 포인트 문자열 프로퍼티
        public string PathPoints
        {
            get => _pathPoints;
            private set => SetProperty(ref _pathPoints, value);
        }

        // 시각 표시 텍스트
        public string TimestampText => Timestamp.ToString("HH:mm:ss");
        // 속도 표시 텍스트
        public string SpeedText => $"{Speed:F1} m/s";
        // 기수 방향 표시 텍스트
        public string HeadingText => $"{Heading:F0}°";
        // 비상 여부
        public bool IsEmergency => Status.Equals("Emergency", StringComparison.OrdinalIgnoreCase);
        // 배터리 바 너비 (최대 160px)
        public double BatteryBarWidth => (_battery / 100.0) * 160.0;
        // 배터리 경고 여부
        public bool BatteryWarning => Battery <= 20;
        // 배터리 경고 텍스트
        public string BatteryWarningText => Battery <= 10 ? "⚡ CRITICAL" : Battery <= 20 ? "⚡ LOW" : "";
        // 배터리 경고 표시 여부
        public Visibility BatteryWarningVisibility => BatteryWarning ? Visibility.Visible : Visibility.Collapsed;
        // 선택 링 표시 여부
        public Visibility SelectionRingVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
        // 목록 표시 여부 (Visibility)
        public Visibility IsVisibleVisibility => IsVisible ? Visibility.Visible : Visibility.Collapsed;

        // 총 비행 거리 텍스트
        public string TotalDistanceText => $"{TotalDistanceM():F0} m";
        // 최대 고도 텍스트
        public string MaxAltitudeText => $"{(_pathRecords.Count > 0 ? MaxAlt() : 0):F1} m";
        // 평균 속도 텍스트
        public string AvgSpeedText => $"{(_pathRecords.Count > 0 ? AvgSpd() : 0):F1} m/s";

        // 상태별 텍스트 색상 브러시 반환
        public Brush StatusAccentColor => StatusBrush(_status);
        // 상태별 배지 배경 브러시 반환
        public Brush StatusBadgeColor => BadgeBrush(_status);
        // 배터리 구간별 색상 브러시 반환
        public Brush BatteryColor => BatteryTier(_battery) switch
        {
            2 => BrushBattGreen,
            1 => BrushBattOrange,
            _ => BrushBattRed
        };
        // 선택 오버레이 배경 브러시
        public Brush SelectionBackground => _isSelected ? SelectionOn : SelectionOff;

        // 생성자 - VehicleData로 초기화
        public VehicleViewModel(VehicleData data) => UpdateFrom(data);

        // 수신 데이터로 프로퍼티 갱신 (변경된 값만 notify)
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
            WindSpeed = data.WindSpeed;
            WindAlert = data.WindAlert ?? "CALM";
            Timestamp = data.Timestamp == default ? DateTime.Now : data.Timestamp;

            // 텔레메트리 추가 (최대 60개 유지)
            TelemetryHistory.Add(new TelemetryPoint
            {
                Time = Timestamp,
                Battery = data.Battery,
                Speed = data.Speed,
                Altitude = data.Altitude
            });
            while (TelemetryHistory.Count > 60) TelemetryHistory.RemoveAt(0);

            // 비행 기록 추가 (최대 500개 유지)
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

        // 지도 픽셀 좌표 갱신 및 경로 히스토리 추가
        public void UpdateMapXY(double x, double y)
        {
            MapX = x; MapY = y;
            _pathHistory.Add(new Point(x, y));
            if (_pathHistory.Count > 60) _pathHistory.RemoveAt(0);

            // 경로 포인트 문자열 재생성
            var sb = new StringBuilder();
            foreach (var p in _pathHistory) sb.Append($"{p.X:F1},{p.Y:F1} ");
            PathPoints = sb.ToString().TrimEnd();
        }

        // 비행 통계 프로퍼티 강제 갱신 (외부 호출용)
        public void NotifyStatsChanged()
        {
            OnPropertyChanged(nameof(TotalDistanceText));
            OnPropertyChanged(nameof(MaxAltitudeText));
            OnPropertyChanged(nameof(AvgSpeedText));
        }

        // 텔레메트리/경로 히스토리 초기화
        public void ClearHistory()
        {
            TelemetryHistory.Clear();
            _pathRecords.Clear();
            _pathHistory.Clear();
            PathPoints = "";
        }

        // 상태 문자열로 텍스트 색상 브러시 반환
        private static Brush StatusBrush(string status) => status switch
        {
            "Active" => BrushActive,
            "Idle" => BrushIdle,
            "Emergency" => BrushEmergency,
            _ => BrushBlue
        };

        // 상태 문자열로 배지 배경 브러시 반환
        private static Brush BadgeBrush(string status) => status switch
        {
            "Active" => BadgeActive,
            "Idle" => BadgeIdle,
            "Emergency" => BadgeEmergency,
            _ => BadgeBlue
        };

        // 배터리 구간 반환 (2=초록, 1=주황, 0=빨강)
        private static int BatteryTier(double b) => b > 50 ? 2 : b > 20 ? 1 : 0;

        // 총 비행 거리 계산 (Haversine, 미터)
        private double TotalDistanceM()
        {
            if (_pathRecords.Count < 2) return 0;
            double total = 0;
            for (int i = 1; i < _pathRecords.Count; i++)
                total += HaversineM(_pathRecords[i - 1].Latitude, _pathRecords[i - 1].Longitude,
                                    _pathRecords[i].Latitude, _pathRecords[i].Longitude);
            return total;
        }

        // 최대 고도 반환
        private double MaxAlt() { double m = 0; foreach (var p in _pathRecords) if (p.Altitude > m) m = p.Altitude; return m; }

        // 평균 속도 반환
        private double AvgSpd() { double s = 0; foreach (var p in _pathRecords) s += p.Speed; return s / _pathRecords.Count; }

        // Haversine 공식으로 두 좌표 간 거리 계산 (미터)
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