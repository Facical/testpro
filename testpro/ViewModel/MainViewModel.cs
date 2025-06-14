using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using testpro.Models;

namespace testpro.ViewModels
{
    public enum ViewMode
    {
        Mode2D,
        Mode3D
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        // 컬렉션
        private ObservableCollection<StoreObject> _storeObjects;
        public ObservableCollection<StoreObject> StoreObjects
        {
            get => _storeObjects;
            set
            {
                _storeObjects = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Wall> _walls;
        public ObservableCollection<Wall> Walls
        {
            get => _walls;
            set
            {
                _walls = value;
                OnPropertyChanged();
            }
        }

        // 선택된 객체
        private StoreObject _selectedObject;
        public StoreObject SelectedObject
        {
            get => _selectedObject;
            set
            {
                _selectedObject = value;
                OnPropertyChanged();
                UpdateStatusText();
            }
        }

        // 현재 도구
        private string _currentTool = "Select";
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

        // 뷰 모드
        private ViewMode _currentViewMode = ViewMode.Mode2D;
        public ViewMode CurrentViewMode
        {
            get => _currentViewMode;
            set
            {
                _currentViewMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Is2DMode));
                OnPropertyChanged(nameof(Is3DMode));
            }
        }

        public bool Is2DMode => CurrentViewMode == ViewMode.Mode2D;
        public bool Is3DMode => CurrentViewMode == ViewMode.Mode3D;

        // 상태 텍스트
        private string _statusText = "준비";
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        // 선택된 객체 타입 (배치용)
        private ObjectType? _selectedObjectType;
        public ObjectType? SelectedObjectType
        {
            get => _selectedObjectType;
            set
            {
                _selectedObjectType = value;
                OnPropertyChanged();

                if (value.HasValue)
                {
                    CurrentTool = "PlaceObject";
                }
            }
        }

        // 성능 정보
        private string _performanceInfo;
        public string PerformanceInfo
        {
            get => _performanceInfo;
            set
            {
                _performanceInfo = value;
                OnPropertyChanged();
            }
        }

        // 생성자
        public MainViewModel()
        {
            StoreObjects = new ObservableCollection<StoreObject>();
            Walls = new ObservableCollection<Wall>();

            // 컬렉션 변경 이벤트 구독
            StoreObjects.CollectionChanged += (s, e) => UpdateStatusText();
            Walls.CollectionChanged += (s, e) => UpdateStatusText();
        }

        // 상태 텍스트 업데이트
        private void UpdateStatusText()
        {
            if (SelectedObject != null)
            {
                StatusText = $"선택됨: {SelectedObject.GetDisplayName()} | 도구: {CurrentTool}";
            }
            else
            {
                StatusText = $"객체: {StoreObjects.Count}개, 벽: {Walls.Count}개 | 도구: {CurrentTool}";
            }
        }

        // 3D 뷰 업데이트
        public void Update3DView()
        {
            OnPropertyChanged(nameof(StoreObjects));
        }

        // 통계 정보
        public string GetStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine($"총 객체 수: {StoreObjects.Count}");

            var groupedByType = StoreObjects.GroupBy(o => o.Type);
            foreach (var group in groupedByType)
            {
                stats.AppendLine($"- {GetObjectTypeName(group.Key)}: {group.Count()}개");
            }

            stats.AppendLine($"벽 개수: {Walls.Count}");

            return stats.ToString();
        }

        private string GetObjectTypeName(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Shelf: return "선반";
                case ObjectType.Refrigerator: return "냉장고";
                case ObjectType.Freezer: return "냉동고";
                case ObjectType.Checkout: return "계산대";
                case ObjectType.DisplayStand: return "진열대";
                case ObjectType.Pillar: return "기둥";
                default: return "기타";
            }
        }

        // PropertyChanged 구현
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // RelayCommand 구현
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
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

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke((T)parameter) ?? true;
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }
}