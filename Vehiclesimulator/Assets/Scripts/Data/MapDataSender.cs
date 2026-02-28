using UnityEngine;
using System.Collections.Generic;
using AirGuard.Network;

namespace AirGuard.Data
{
    /// <summary>
    /// WPF에서 MAP_REQUEST 수신 시 맵 데이터 전송
    /// </summary>
    public class MapDataSender : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;

        private static readonly string[] MapTags = { "Building", "Nature", "Road", "Prop", "Vehicle" };

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
        }

        private void Start()
        {
            // NetworkManager의 OnDataReceived 이벤트 구독
            NetworkManager.OnCommandReceived += OnCommandReceived;
        }

        private void OnDestroy()
        {
            NetworkManager.OnCommandReceived -= OnCommandReceived;
        }

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
                    objects.Add(new MapObjectData
                    {
                        tag = tag,
                        x = go.transform.position.x,
                        y = go.transform.position.z,
                        h = go.transform.position.y,
                        sx = bounds.size.x,
                        sy = bounds.size.z,
                        rot = go.transform.eulerAngles.y
                    });
                }
            }

            // 맵 범위 계산
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
        public float sx, sy;
        public float rot;
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