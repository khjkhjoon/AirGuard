using UnityEngine;
using UnityEngine.UI;
using AirGuard.Data;
using TMPro;

namespace AirGuard.UI
{
    public class VehicleUIController : MonoBehaviour
    {
        [Header("Drone")]
        [SerializeField] private Controllers.DronePhysicsSystem drone;

        [Header("UI")]
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text batteryText;
        [SerializeField] private TMP_Text windText;
        [SerializeField] private TMP_Text windDirText;
        [SerializeField] private GameObject alertBadge;

        [Header("Flight HUD")]
        [SerializeField] private TMP_Text altitudeText;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text headingText;
        [SerializeField] private Transform homePoint;

        [Header("Compass Settings")]
        [SerializeField] public RectTransform compassStrip;
        [SerializeField] private GameObject tickPrefab;
        [SerializeField] private float tickSpacing = 60f;  // 15도당 픽셀

        // 컴패스는 -360 ~ +360 범위(총 720도) 생성 → 어느 방향이든 끊기지 않음
        private const int COMPASS_HALF = 720;

        public float pixelsPerDegree;

        [SerializeField] private Canvas _canvas;

        private void Start() => GenerateCompass();

        // ══════════════════════════════════════════
        // 컴패스 생성
        // ══════════════════════════════════════════
        private void GenerateCompass()
        {
            if (compassStrip == null || tickPrefab == null) return;

            // 기존 틱 제거 (재생성 방지)
            foreach (Transform child in compassStrip)
                Destroy(child.gameObject);

            pixelsPerDegree = tickSpacing / 15f;

            // -360 ~ +360 범위로 생성 (중앙=0도=N 기준)
            for (int angle = -COMPASS_HALF; angle <= COMPASS_HALF; angle += 15)
            {
                GameObject tick = Instantiate(tickPrefab, compassStrip);
                RectTransform rt = tick.GetComponent<RectTransform>();

                // 0도가 중앙에 오도록 픽셀 위치 계산
                float posX = angle * pixelsPerDegree;
                rt.anchoredPosition = new Vector2(posX, 0);

                TMP_Text txt = tick.GetComponentInChildren<TMP_Text>();
                if (txt != null)
                {
                    // angle을 0~359로 정규화해서 라벨 결정
                    int normalized = ((angle % 360) + 360) % 360;
                    txt.text = GetCompassLabel(normalized);
                }
            }
        }

        private string GetCompassLabel(int angle)
        {
            return angle switch
            {
                0 => "N",
                45 => "NE",
                90 => "E",
                135 => "SE",
                180 => "S",
                225 => "SW",
                270 => "W",
                315 => "NW",
                _ => "|"
            };
        }

        // ══════════════════════════════════════════
        // 컴패스 업데이트
        // ══════════════════════════════════════════
        private void UpdateCompass()
        {
            if (drone == null || compassStrip == null) return;

            float heading = drone.transform.eulerAngles.y % 360f;
            if (heading < 0) heading += 360f;

            // heading 0~360을 픽셀로 변환
            // 틱이 -720~+720 범위에 생성되어 있으므로 항상 보임
            float offset = heading * pixelsPerDegree;
            compassStrip.anchoredPosition = new Vector2(-offset, 0);
        }

        // ══════════════════════════════════════════
        // 메인 UI 업데이트
        // ══════════════════════════════════════════
        public void UpdateUI(VehicleData data, float battery, VehicleStatus status,
                             bool connected, string connectionStatus)
        {
            if (_canvas == null || data == null) return;

            if (headerText != null) headerText.text = $"{data.Name} [{data.VehicleId}]";
            if (statusText != null) statusText.text = status.ToString();
            if (batteryText != null) batteryText.text = $"{battery:F0}%";
            if (windText != null) windText.text = $"{data.WindSpeed:F1} m/s  {data.WindAlert ?? "CALM"}";
            if (windDirText != null) windDirText.text = GetWindArrow();
            if (alertBadge != null) alertBadge.SetActive(data.WindAlert == "TURBULENCE");

            UpdateFlightHUD();
            UpdateCompass();
        }

        // ══════════════════════════════════════════
        // 비행 HUD
        // ══════════════════════════════════════════
        private void UpdateFlightHUD()
        {
            if (drone == null) return;

            var rb = drone.GetComponent<Rigidbody>();
            if (rb == null) return;

            float speed = rb.linearVelocity.magnitude;
            float altitude = drone.transform.position.y;
            float heading = drone.transform.eulerAngles.y;

            if (speedText != null) speedText.text = $"SPD  {speed:F1} m/s";
            if (altitudeText != null) altitudeText.text = $"ALT  {altitude:F1} m";
            if (headingText != null) headingText.text = $"HDG  {heading:F0}°  {GetHeadingDir(heading)}";

            if (homePoint != null && distanceText != null)
            {
                float dist = Vector3.Distance(homePoint.position, drone.transform.position);
                distanceText.text = $"DST  {dist:F1} m";
            }
        }

        // ══════════════════════════════════════════
        // 헬퍼
        // ══════════════════════════════════════════
        private string GetWindArrow()
        {
            if (drone == null) return "--";
            Vector3 dir = drone.WindDirection;
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            int sector = Mathf.RoundToInt(angle / 45f) % 8;
            string[] arrows = { "N ^", "NE ^>", "E >", "SE v>", "S v", "SW v<", "W <", "NW ^<" };
            return arrows[sector];
        }

        private string GetHeadingDir(float angle)
        {
            if (angle < 22.5f) return "N";
            if (angle < 67.5f) return "NE";
            if (angle < 112.5f) return "E";
            if (angle < 157.5f) return "SE";
            if (angle < 202.5f) return "S";
            if (angle < 247.5f) return "SW";
            if (angle < 292.5f) return "W";
            if (angle < 337.5f) return "NW";
            return "N";
        }
    }
}