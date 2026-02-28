using System;

namespace AirGuard.WPF.Models
{
    /// <summary>
    /// 드론 텔레메트리 시계열 포인트 (그래프용)
    /// </summary>
    public class TelemetryPoint
    {
        public DateTime Time { get; set; }
        public double Battery { get; set; }
        public double Speed { get; set; }
        public double Altitude { get; set; }
        public string TimeLabel => Time.ToString("HH:mm:ss");
    }
}