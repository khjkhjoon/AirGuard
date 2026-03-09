using UnityEngine;
using System.Collections.Generic;
using AirGuard.Network;

namespace AirGuard.Data
{
    public class MapDataSender : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        private static readonly string[] MapTags = { "Building", "Nature", "Road", "Prop", "Vehicle" };

        private void Awake() { networkManager = GetComponent<NetworkManager>(); }
        private void Start() { NetworkManager.OnCommandReceived += OnCommandReceived; }
        private void OnDestroy() { NetworkManager.OnCommandReceived -= OnCommandReceived; }

        private void OnCommandReceived(string command)
        {
            if (command.Trim() == "MAP_REQUEST")
            {
                Debug.Log("[MapDataSender] MAP_REQUEST 수신, 맵 전송 시작");
                SendMapData();
            }
        }

        private void SendMapData()
        {
            var objects = new List<MapObjectData>();

            foreach (var tag in MapTags)
            {
                var found = GameObject.FindGameObjectsWithTag(tag);
                foreach (var go in found)
                {
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    if (tag == "Prop" || tag == "Vehicle") continue;

                    var bounds = renderer.bounds;

                    float sx, sy, sh;
                    float fx, fz, rx, rz;

                    if (tag == "Road" || tag == "Nature")
                    {
                        var t = go.transform;
                        // 크기는 bounds XZ 그대로 사용
                        sx = bounds.size.x;
                        sy = bounds.size.z;
                        sh = 0f;
                        // 방향 벡터만 추가 (부모 회전 포함한 실제 월드 방향)
                        rx = t.right.x; rz = t.right.z;
                        fx = t.forward.x; fz = t.forward.z;
                    }
                    else
                    {
                        sx = bounds.size.x;
                        sy = bounds.size.z;
                        sh = bounds.size.y;
                        fx = 0; fz = 1; rx = 1; rz = 0;
                    }
                    objects.Add(new MapObjectData
                    {
                        tag = tag,
                        x = go.transform.position.x,
                        y = go.transform.position.z,
                        h = go.transform.position.y,
                        sh = sh,
                        sx = sx,
                        sy = sy,
                        rot = go.transform.eulerAngles.y,
                        fx = fx,
                        fz = fz,
                        rx = rx,
                        rz = rz,
                    });
                }
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var o in objects)
            {
                if (o.x - o.sx / 2 < minX) minX = o.x - o.sx / 2;
                if (o.x + o.sx / 2 > maxX) maxX = o.x + o.sx / 2;
                if (o.y - o.sy / 2 < minY) minY = o.y - o.sy / 2;
                if (o.y + o.sy / 2 > maxY) maxY = o.y + o.sy / 2;
            }

            var mapData = new MapData
            {
                type = "map",
                originX = minX,
                originY = minY,
                width = maxX - minX,
                height = maxY - minY,
                objects = objects
            };

            string json = JsonUtility.ToJson(mapData) + "\n";
            networkManager.SendRawData(json);
            Debug.Log($"[MapDataSender] 맵 전송 완료: {objects.Count}개 / 범위: {mapData.width:F1}x{mapData.height:F1}");
        }
    }

    [System.Serializable]
    public class MapObjectData
    {
        public string tag;
        public float x, y, h;
        public float sh;
        public float sx, sy;
        public float rot;
        // 월드 forward/right 방향 (부모 회전 포함한 실제 방향)
        public float fx, fz;  // 오브젝트 forward 방향 XZ
        public float rx, rz;  // 오브젝트 right 방향 XZ
    }

    [System.Serializable]
    public class MapData
    {
        public string type;
        public float originX, originY;
        public float width, height;
        public List<MapObjectData> objects;
    }
}