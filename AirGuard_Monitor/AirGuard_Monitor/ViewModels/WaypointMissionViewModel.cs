using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirGuard.WPF.ViewModels;

namespace AirGuard.WPF.ViewModels
{
    /// <summary>
    /// 단일 웨이포인트 데이터 및 상태 관리
    /// </summary>
    public class WaypointItem : BaseViewModel
    {
        // 웨이포인트 순서 번호
        private int _index;
        // 유니티 월드 X 좌표
        private double _worldX;
        // 유니티 월드 Z 좌표
        private double _worldZ;
        // 목표 고도 (m)
        private double _altitude;
        // 현재 진행 중인 웨이포인트 여부
        private bool _isCurrent;
        // 도달 완료 여부
        private bool _isReached;

        // 순서 번호 프로퍼티
        public int Index { get => _index; set => SetProperty(ref _index, value); }
        // 유니티 X 프로퍼티
        public double WorldX { get => _worldX; set => SetProperty(ref _worldX, value); }
        // 유니티 Z 프로퍼티
        public double WorldZ { get => _worldZ; set => SetProperty(ref _worldZ, value); }
        // 고도 프로퍼티
        public double Altitude { get => _altitude; set => SetProperty(ref _altitude, value); }
        // 현재 웨이포인트 여부 프로퍼티
        public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }
        // 도달 완료 여부 프로퍼티
        public bool IsReached { get => _isReached; set => SetProperty(ref _isReached, value); }

        // 캔버스 표시 X 좌표
        public double CanvasX { get; set; }
        // 캔버스 표시 Y 좌표
        public double CanvasY { get; set; }

        // 목록 표시 레이블 (번호 + 고도)
        public string Label => $"WP{Index}  {Altitude:F0}m";
    }

    /// <summary>
    /// 미션 전송용 JSON 최상위 모델
    /// </summary>
    public class WaypointMissionJson
    {
        // 메시지 타입 ("mission" 고정)
        [JsonPropertyName("type")]
        public string Type { get; set; } = "mission";

        // 대상 드론 ID
        [JsonPropertyName("targetId")]
        public string TargetId { get; set; } = "";

        // 웨이포인트 목록
        [JsonPropertyName("waypoints")]
        public List<WaypointJson> Waypoints { get; set; } = new();
    }

    /// <summary>
    /// 단일 웨이포인트 JSON 직렬화 모델
    /// </summary>
    public class WaypointJson
    {
        // 웨이포인트 순서 번호
        [JsonPropertyName("index")]
        public int Index { get; set; }

        // 유니티 X 좌표
        [JsonPropertyName("x")]
        public double X { get; set; }

        // 유니티 Z 좌표
        [JsonPropertyName("z")]
        public double Z { get; set; }

        // 목표 고도
        [JsonPropertyName("altitude")]
        public double Altitude { get; set; }
    }

    /// <summary>
    /// 웨이포인트 미션 뷰모델 - 편집/전송/진행상황 관리
    /// </summary>
    public class WaypointMissionViewModel : BaseViewModel
    {
        // 웨이포인트 목록
        public ObservableCollection<WaypointItem> Waypoints { get; } = new();

        // 편집 모드 활성 여부
        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                SetProperty(ref _isEditMode, value);
                OnPropertyChanged(nameof(EditModeButtonText));
                OnPropertyChanged(nameof(EditModeButtonColor));
            }
        }

        // 기본 고도 (m)
        private double _defaultAltitude = 20.0;
        public double DefaultAltitude
        {
            get => _defaultAltitude;
            set => SetProperty(ref _defaultAltitude, value);
        }

        // 미션 상태 텍스트
        private string _missionStatus = "대기";
        public string MissionStatus
        {
            get => _missionStatus;
            set => SetProperty(ref _missionStatus, value);
        }

        // 편집 모드 버튼 텍스트
        public string EditModeButtonText => IsEditMode ? "✏ 편집 중" : "✏ 웨이포인트";
        // 편집 모드 버튼 색상
        public string EditModeButtonColor => IsEditMode ? "#FF8C00" : "#0C1820";
        // 총 웨이포인트 수
        public int WaypointCount => Waypoints.Count;

        // 웨이포인트 추가 - 월드/캔버스 좌표 입력, 생성된 WaypointItem 반환
        public WaypointItem AddWaypoint(double worldX, double worldZ, double canvasX, double canvasY)
        {
            var wp = new WaypointItem
            {
                Index = Waypoints.Count + 1,
                WorldX = worldX,
                WorldZ = worldZ,
                Altitude = DefaultAltitude,
                CanvasX = canvasX,
                CanvasY = canvasY,
            };
            Waypoints.Add(wp);
            OnPropertyChanged(nameof(WaypointCount));
            return wp;
        }

        // 웨이포인트 삭제 후 인덱스 재정렬
        public void RemoveWaypoint(WaypointItem wp)
        {
            Waypoints.Remove(wp);
            for (int i = 0; i < Waypoints.Count; i++)
                Waypoints[i].Index = i + 1;
            OnPropertyChanged(nameof(WaypointCount));
        }

        // 전체 웨이포인트 초기화
        public void ClearWaypoints()
        {
            Waypoints.Clear();
            MissionStatus = "대기";
            OnPropertyChanged(nameof(WaypointCount));
        }

        // 현재 진행 중인 웨이포인트 표시 갱신
        public void SetCurrentWaypoint(int index)
        {
            foreach (var wp in Waypoints)
            {
                wp.IsReached = wp.Index < index;
                wp.IsCurrent = wp.Index == index;
            }
            MissionStatus = index > Waypoints.Count
                ? "미션 완료"
                : $"WP{index} 이동 중";
        }

        // 미션을 JSON 문자열로 직렬화 (TCP 전송용)
        public string ToJson(string targetVehicleId)
        {
            var mission = new WaypointMissionJson
            {
                TargetId = targetVehicleId,
                Waypoints = Waypoints.Select(wp => new WaypointJson
                {
                    Index = wp.Index,
                    X = wp.WorldX,
                    Z = wp.WorldZ,
                    Altitude = wp.Altitude,
                }).ToList()
            };
            return JsonSerializer.Serialize(mission);
        }
    }
}