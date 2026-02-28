using System;

namespace AirGuard.WPF.Models
{
    /// <summary>
    /// 드론 경로 기록 포인트 (플레이백용)
    /// </summary>
    public class PathRecord
    {
        public DateTime Time { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public string Status { get; set; } = "Idle";
    }
}