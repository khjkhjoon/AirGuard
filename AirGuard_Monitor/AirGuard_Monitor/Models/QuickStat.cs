using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AirGuard.WPF.Models
{
    /// <summary>
    /// 관제 UI에서 빠른 상태 정보를 표시하는 모델 (실시간 갱신용)
    /// </summary>
    public class QuickStat : INotifyPropertyChanged
    {
        private string _value = "0"; // 실제 값 저장

        public string Label { get; set; } = ""; // 표시 이름 (예: Battery, Speed)

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); } // 값 변경 시 UI 갱신
        }

        public Brush Color { get; set; } = Brushes.White; // 텍스트 표시 색상

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 속성 변경 시 UI에 변경 사항을 알림
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}