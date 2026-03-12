using System;

namespace AirGuard.WPF.Models
{
    /// <summary>
    /// 드론 텔레메트리 시계열 포인트 (그래프용)
    /// </summary>
    public class TelemetryPoint
    {
        public DateTime Time { get; set; }   // 데이터 기록 시간
        public double Battery { get; set; }  // 배터리 잔량
        public double Speed { get; set; }    // 드론 속도
        public double Altitude { get; set; } // 드론 고도

        // 그래프나 UI에 표시할 시간 문자열
        public string TimeLabel => Time.ToString("HH:mm:ss");
    }
}