using System;
using UnityEngine;

namespace AirGuard.Data
{
    /// <summary>
    /// 차량 데이터 모델 (서버 전송용)
    /// </summary>
    [Serializable]
    public class VehicleData
    {
        public string VehicleId;
        public string Name;
        public double Latitude;
        public double Longitude;
        public double Altitude;
        public double Speed;
        public double Battery;
        public string Status;
        public double Heading;
        public string Timestamp;

        public VehicleData(string id, string name)
        {
            VehicleId = id;
            Name = name;
            Latitude = 0;
            Longitude = 0;
            Altitude = 0;
            Speed = 0;
            Battery = 100;
            Status = VehicleStatus.Idle.ToString();
            Heading = 0;
            Timestamp = DateTime.Now.ToString("o");
        }

        public void UpdateFromTransform(Transform transform, float speed, float battery, VehicleStatus status)
        {
            // Unity 좌표 → 위도/경도 변환 (서울 기준)
            //const double BASE_LATITUDE = 37.5665;
            //const double BASE_LONGITUDE = 126.9780;
            //const double COORDINATE_SCALE = 0.0001;

            //Latitude = BASE_LATITUDE + transform.position.z * COORDINATE_SCALE;
            //Longitude = BASE_LONGITUDE + transform.position.x * COORDINATE_SCALE;
            //Altitude = transform.position.y;

            Latitude = transform.position.z;
            Longitude = transform.position.x;
            Altitude = transform.position.y;

            Speed = speed;
            Battery = battery;
            Status = status.ToString();
            Heading = transform.eulerAngles.y;
            Timestamp = DateTime.Now.ToString("o");
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }

    /// <summary>
    /// 차량 상태
    /// </summary>
    public enum VehicleStatus
    {
        Idle,           // 대기
        Active,         // 활동
        InMission,      // 임무 수행
        Returning,      // 귀환
        Emergency,      // 긴급
        Offline,        // 오프라인
        Maintenance     // 정비
    }

    /// <summary>
    /// 차량 타입
    /// </summary>
    public enum VehicleType
    {
        Drone,
        GroundVehicle,
        AerialVehicle
    }
}