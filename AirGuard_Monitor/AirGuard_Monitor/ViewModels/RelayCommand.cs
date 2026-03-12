using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AirGuard.WPF.ViewModels
{
    /// <summary>
    /// MVVM 커맨드 구현체 - async/sync 람다를 ICommand로 래핑
    /// </summary>
    public class RelayCommand : ICommand
    {
        // 실행할 비동기 델리게이트
        private readonly Func<object?, Task> _execute;
        // 실행 가능 여부 판단 델리게이트 (null이면 항상 true)
        private readonly Func<object?, bool>? _canExecute;
        // 현재 실행 중 여부 (중복 실행 방지)
        private bool _isExecuting;

        // async 버전 생성자
        public RelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        // sync 버전 생성자 - Action을 Task로 래핑
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = p => { execute(p); return Task.CompletedTask; };
            _canExecute = canExecute;
        }

        // CanExecute 변경 알림 - WPF CommandManager 재평가 연동
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        // 실행 중이 아니고 canExecute 조건 만족 시 true 반환
        public bool CanExecute(object? parameter)
            => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

        // 실행 - 중복 호출 방지 후 await, finally에서 플래그 해제
        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            try { await _execute(parameter); }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}