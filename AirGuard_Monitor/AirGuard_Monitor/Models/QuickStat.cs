using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AirGuard.WPF.Models
{
    public class QuickStat : INotifyPropertyChanged
    {
        private string _value = "0";

        public string Label { get; set; } = "";
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }
        public Brush Color { get; set; } = Brushes.White;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}