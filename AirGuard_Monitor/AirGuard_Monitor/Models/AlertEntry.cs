using System.Windows.Media;

namespace AirGuard.WPF.Models
{
    public class AlertEntry
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string UnitId { get; set; } = "";
        public string Time { get; set; } = "";
        public Brush AlertColor { get; set; } = Brushes.Orange;
    }

    public class LogEntry
    {
        public string Time { get; set; } = "";
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = "";
        public Brush LevelColor { get; set; } = Brushes.Gray;
    }
}