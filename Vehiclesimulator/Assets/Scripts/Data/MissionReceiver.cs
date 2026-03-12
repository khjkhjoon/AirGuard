using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AirGuard.Network;

namespace AirGuard.Data
{
    [System.Serializable]
    public class WaypointData
    {
        public int index;
        public float x;
        public float z;
        public float altitude;
    }

    public class MissionData
    {
        public string type;
        public string targetId;
        public List<WaypointData> waypoints;
    }

    public class MissionReceiver : MonoBehaviour
    {
        [Header("드론 설정")]
        [SerializeField] private string vehicleId = "DRONE_001";
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float arrivalDist = 1.5f;

        [Header("네트워크")]
        [SerializeField] private NetworkManager networkManager;

        private MissionData _mission;
        private int _currentWpIndex = 0;
        private bool _isExecuting = false;
        private Coroutine _missionCoroutine;

        private void Start()
        {
            NetworkManager.OnCommandReceived += OnCommandReceived;
        }

        private void OnDestroy()
        {
            NetworkManager.OnCommandReceived -= OnCommandReceived;
        }

        private void OnCommandReceived(string json)
        {
            if (!json.Contains("\"mission\"")) return;
            if (!json.Contains(vehicleId)) return;

            MissionData parsed = ParseMission(json);
            if (parsed == null)
            {
                Debug.LogWarning("[MissionReceiver] 파싱 실패");
                return;
            }

            _mission = parsed;
            Debug.Log("[MissionReceiver] 미션 수신: WP " + _mission.waypoints.Count + "개");

            if (_missionCoroutine != null)
                StopCoroutine(_missionCoroutine);

            _currentWpIndex = 0;
            _isExecuting = true;
            _missionCoroutine = StartCoroutine(ExecuteMission());
        }

        private MissionData ParseMission(string json)
        {
            Match typeMatch = Regex.Match(json, "\"type\"\\s*:\\s*\"([^\"]+)\"");
            Match idMatch = Regex.Match(json, "\"targetId\"\\s*:\\s*\"([^\"]+)\"");

            if (!typeMatch.Success || !idMatch.Success) return null;

            string parsedType = typeMatch.Groups[1].Value;
            string parsedId = idMatch.Groups[1].Value;

            if (parsedType != "mission" || parsedId != vehicleId) return null;

            MissionData data = new MissionData
            {
                type = parsedType,
                targetId = parsedId,
                waypoints = new List<WaypointData>()
            };

            MatchCollection wpMatches = Regex.Matches(json,
                "\"index\"\\s*:\\s*(\\d+).*?\"x\"\\s*:\\s*([\\d.Ee+\\-]+).*?\"z\"\\s*:\\s*([\\d.Ee+\\-]+).*?\"altitude\"\\s*:\\s*([\\d.Ee+\\-]+)");

            foreach (Match m in wpMatches)
            {
                data.waypoints.Add(new WaypointData
                {
                    index = int.Parse(m.Groups[1].Value),
                    x = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                    z = float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                    altitude = float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture)
                });
            }

            return data;
        }

        private IEnumerator ExecuteMission()
        {
            Debug.Log("[MissionReceiver] 미션 시작: " + _mission.waypoints.Count + "개 웨이포인트");

            foreach (WaypointData wp in _mission.waypoints)
            {
                _currentWpIndex = wp.index;
                SendProgress(wp.index);

                Vector3 target = new Vector3(wp.x, wp.altitude, wp.z);
                Debug.Log("[MissionReceiver] WP" + wp.index + " 이동 시작");

                while (Vector3.Distance(transform.position, target) > arrivalDist)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position, target, moveSpeed * Time.deltaTime);

                    Vector3 dir = target - transform.position;
                    if (dir.magnitude > 0.1f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
                    }

                    yield return null;
                }

                Debug.Log("[MissionReceiver] WP" + wp.index + " 도착");
                yield return new WaitForSeconds(0.5f);
            }

            _isExecuting = false;
            SendProgress(_mission.waypoints.Count + 1);
            Debug.Log("[MissionReceiver] 미션 완료!");
        }

        private void SendProgress(int wpIndex)
        {
            if (networkManager == null) return;
            string msg = "{\"type\":\"mission_progress\",\"vehicleId\":\"" + vehicleId + "\",\"currentWp\":" + wpIndex + "}";
            networkManager.SendRawData(msg);
        }

        public void CancelMission()
        {
            if (_missionCoroutine != null)
                StopCoroutine(_missionCoroutine);
            _isExecuting = false;
            Debug.Log("[MissionReceiver] 미션 취소됨");
        }


    }
}