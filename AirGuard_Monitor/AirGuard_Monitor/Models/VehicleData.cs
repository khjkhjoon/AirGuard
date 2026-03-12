using System;

namespace AirGuard.WPF.Models
{
    /// <summary>
    /// 드론의 실시간 텔레메트리 및 상태 정보를 저장하는 모델
    /// </summary>
    public class VehicleData
    {
        public string VehicleId { get; set; } = ""; // 드론 ID
        public string Name { get; set; } = "";      // 드론 이름

        public double Latitude { get; set; }        // 현재 위도
        public double Longitude { get; set; }       // 현재 경도
        public double Altitude { get; set; }        // 현재 고도
        public double Speed { get; set; }           // 현재 속도
        public double Battery { get; set; }         // 배터리 잔량 (%)

        public string Status { get; set; } = "Idle"; // 드론 상태 (Idle, Flying 등)

        public double Heading { get; set; }         // 드론 진행 방향 (각도)
        public double WindSpeed { get; set; }       // 현재 풍속
        public string WindAlert { get; set; } = "CALM"; // 풍속 경고 상태

        public DateTime Timestamp { get; set; }     // 데이터 수신 시간
    }
}