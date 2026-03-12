using System.Windows.Media;

namespace AirGuard.WPF.Models
{
    /// <summary>
    /// 드론 관제 시스템에서 발생한 알림 정보를 저장
    /// </summary>
    public class AlertEntry
    {
        public string Title { get; set; } = "";           // 알림 제목
        public string Message { get; set; } = "";         // 알림 내용
        public string UnitId { get; set; } = "";          // 알림이 발생한 드론 ID
        public string Time { get; set; } = "";            // 알림 발생 시간
        public Brush AlertColor { get; set; } = Brushes.Orange; // 알림 표시 색상
    }

    /// <summary>
    /// 시스템 로그 정보를 저장
    /// </summary>
    public class LogEntry
    {
        public string Time { get; set; } = "";            // 로그 발생 시간
        public string Level { get; set; } = "INFO";       // 로그 레벨
        public string Message { get; set; } = "";         // 로그 메시지
        public Brush LevelColor { get; set; } = Brushes.Gray; // 로그 레벨 표시 색상
    }
}