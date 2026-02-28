using UnityEngine;
using AirGuard.Data;

namespace AirGuard.UI
{
    /// <summary>
    /// 차량 UI 컨트롤러
    /// Unity GUI로 정보 표시
    /// </summary>
    public class VehicleUIController : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private bool showUI = true;
        [SerializeField] private int fontSize = 14;
        [SerializeField] private Color textColor = Color.white;

        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;

        private string _vehicleId;
        private string _vehicleName;
        private VehicleStatus _currentStatus;
        private float _batteryPercentage;
        private bool _isConnected;
        private string _connectionStatus;

        private void Awake()
        {
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            _labelStyle = new GUIStyle
            {
                fontSize = fontSize,
                normal = { textColor = textColor }
            };

            _headerStyle = new GUIStyle
            {
                fontSize = fontSize + 2,
                fontStyle = FontStyle.Bold,
                normal = { textColor = textColor }
            };
        }

        public void UpdateUI(VehicleData data, float battery, VehicleStatus status,
                            bool connected, string connectionStatus)
        {
            _vehicleId = data.VehicleId;
            _vehicleName = data.Name;
            _batteryPercentage = battery;
            _currentStatus = status;
            _isConnected = connected;
            _connectionStatus = connectionStatus;
        }

        private void OnGUI()
        {
            if (!showUI) return;

            // 스타일 null 체크
            if (_labelStyle == null || _headerStyle == null)
            {
                InitializeStyles();
            }

            DrawVehicleInfo();
            DrawControls();
        }

        private void DrawVehicleInfo()
        {
            float x = 10;
            float y = 10;
            float lineHeight = 25;

            // 헤더
            GUI.Label(new Rect(x, y, 400, lineHeight),
                $"🚁 {_vehicleName} [{_vehicleId}]", _headerStyle);
            y += lineHeight;

            // 상태
            GUI.Label(new Rect(x, y, 400, lineHeight),
                $"상태: {GetStatusEmoji(_currentStatus)} {_currentStatus}", _labelStyle);
            y += lineHeight;

            // 배터리
            Color originalColor = GUI.color;
            GUI.color = GetBatteryColor(_batteryPercentage);
            GUI.Label(new Rect(x, y, 400, lineHeight),
                $"배터리: {_batteryPercentage:F1}% {GetBatteryBar(_batteryPercentage)}", _labelStyle);
            GUI.color = originalColor;
            y += lineHeight;

            // 연결 상태
            GUI.Label(new Rect(x, y, 400, lineHeight),
                $"연결: {(_isConnected ? "✅" : "❌")} {_connectionStatus}", _labelStyle);
        }

        private void DrawControls()
        {
            float x = 10;
            float y = 120;
            float lineHeight = 20;

            GUI.Label(new Rect(x, y, 500, lineHeight),
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━", _labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, 500, lineHeight),
                "조작: WASD-이동 | Space-상승 | Shift-하강", _labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, 500, lineHeight),
                "상태: E-긴급 | R-복구 | M-임무 | H-귀환", _labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, 500, lineHeight),
                "기타: B-충전", _labelStyle);
        }

        private string GetStatusEmoji(VehicleStatus status)
        {
            return status switch
            {
                VehicleStatus.Idle => "⏸️",
                VehicleStatus.Active => "▶️",
                VehicleStatus.InMission => "🎯",
                VehicleStatus.Returning => "🔄",
                VehicleStatus.Emergency => "🚨",
                VehicleStatus.Offline => "❌",
                VehicleStatus.Maintenance => "🔧",
                _ => "❓"
            };
        }

        private Color GetBatteryColor(float percentage)
        {
            if (percentage > 50) return Color.green;
            if (percentage > 20) return Color.yellow;
            return Color.red;
        }

        private string GetBatteryBar(float percentage)
        {
            int bars = Mathf.RoundToInt(percentage / 10);
            string filled = new string('█', bars);
            string empty = new string('░', 10 - bars);
            return $"[{filled}{empty}]";
        }

        public void ToggleUI()
        {
            showUI = !showUI;
        }

        public void SetFontSize(int size)
        {
            fontSize = size;
            InitializeStyles();
        }

        public void SetTextColor(Color color)
        {
            textColor = color;
            InitializeStyles();
        }
    }
}