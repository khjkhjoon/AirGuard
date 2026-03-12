using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AirGuard.WPF.ViewModels
{
    /// <summary>
    /// ViewModel에서 사용하는 공통 PropertyChanged 기능을 제공하는 기본 ViewModel 클래스
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged; // 속성 변경 이벤트

        /// <summary>
        /// 속성 변경 알림 이벤트 발생
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// 속성 값을 변경하고 변경 이벤트를 발생시키는 공통 메서드
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);

            return true;
        }
    }
}