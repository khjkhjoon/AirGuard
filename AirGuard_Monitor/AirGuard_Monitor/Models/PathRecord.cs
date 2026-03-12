using System;

namespace AirGuard.WPF.Models
{
    /// <summary>
    /// 드론 경로 기록 포인트 (플레이백용)
    /// </summary>
    public class PathRecord
    {
        public DateTime Time { get; set; }     // 기록 시간
        public double Latitude { get; set; }   // 위도
        public double Longitude { get; set; }  // 경도
        public double Altitude { get; set; }   // 고도
        public double Speed { get; set; }      // 속도
        public string Status { get; set; } = "Idle"; // 드론 상태
    }
}