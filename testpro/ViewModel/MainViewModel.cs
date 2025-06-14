using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using testpro.Services;

namespace testpro.ViewModels
{
    public enum ViewMode
    {
        View2D,
        View3D
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private DrawingService _drawingService;
        private string _currentTool = "Select";
        private string _statusText = "도구를 선택하세요";
        private ViewMode _currentViewMode = ViewMode.View2D;

        public DrawingService DrawingService
        {
            get => _drawingService;
            set
            {
                _drawingService = value;
                OnPropertyChanged();
            }
        }

        public string CurrentTool
        {
            get => _currentTool;
            set
            {
                _currentTool = value;
                OnPropertyChanged();
                UpdateStatusText();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public ViewMode CurrentViewMode
        {
            get => _currentViewMode;
            set
            {
                _currentViewMode = value;
                OnPropertyChanged();
                UpdateStatusText();
            }
        }

        // 뷰 모드 관련 속성들
        public bool Is2DMode => CurrentViewMode == ViewMode.View2D;
        public bool Is3DMode => CurrentViewMode == ViewMode.View3D;

        public ICommand SelectToolCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand Switch2DCommand { get; }
        public ICommand Switch3DCommand { get; }

        public MainViewModel()
        {
            DrawingService = new DrawingService();

            // DrawingService의 변경사항을 감지
            DrawingService.PropertyChanged += (s, e) =>
            {
                // DrawingService가 변경되면 전체 뷰모델의 변경을 알림
                OnPropertyChanged(nameof(DrawingService));
            };

            SelectToolCommand = new RelayCommand(() => CurrentTool = "Select");
            ClearCommand = new RelayCommand(() => {
                DrawingService.Clear();
                CurrentTool = "Select";
            });

            // 뷰 모드 전환 명령
            Switch2DCommand = new RelayCommand(() => CurrentViewMode = ViewMode.View2D);
            Switch3DCommand = new RelayCommand(() => CurrentViewMode = ViewMode.View3D);

            CurrentTool = "Select";
        }

        private void UpdateStatusText()
        {
            var modeText = CurrentViewMode == ViewMode.View2D ? "2D" : "3D";

            switch (CurrentTool)
            {
                case "WallStraight":
                    StatusText = $"[{modeText}] 직선 벽 그리기: 시작점을 클릭하세요";
                    break;
                case "Select":
                    StatusText = $"[{modeText}] 선택 도구 활성화";
                    break;
                default:
                    StatusText = $"[{modeText}] 도구를 선택하세요";
                    break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // 뷰 모드 관련 속성들 자동 업데이트
            if (propertyName == nameof(CurrentViewMode))
            {
                OnPropertyChanged(nameof(Is2DMode));
                OnPropertyChanged(nameof(Is3DMode));
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly System.Action _execute;
        private readonly System.Func<bool> _canExecute;

        public RelayCommand(System.Action execute, System.Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new System.ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event System.EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}