using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using testpro.Models;
using testpro.ViewModels;
using testpro.Views;

namespace testpro
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private EditMode _currentEditMode = EditMode.Drawing;

        private enum EditMode
        {
            Drawing,
            Loading
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeViewModel();
            SetupEventHandlers();
            AddTestButtons();
        }

        private void InitializeViewModel()
        {
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // 자식 컨트롤에 ViewModel 전달
            DrawingCanvasControl.ViewModel = _viewModel;
            Viewer3DControl.ViewModel = _viewModel;
            DrawingCanvasControl.MainWindow = this;

            // 초기 상태 설정
            _viewModel.CurrentTool = "Select";
            UpdateStatistics();
        }

        private void SetupEventHandlers()
        {
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.StoreObjects))
                {
                    UpdateStatistics();
                }
            };

            _viewModel.StoreObjects.CollectionChanged += (s, e) => UpdateStatistics();
            _viewModel.Walls.CollectionChanged += (s, e) => UpdateStatistics();
        }

        private void AddTestButtons()
        {
            // 툴바에 구분선 추가
            MainToolBar.Items.Add(new Separator());

            // 냉장고 테스트 버튼
            var refrigeratorTestButton = new Button
            {
                Content = "냉장고 테스트",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                ToolTip = "테스트용 냉장고 추가"
            };
            refrigeratorTestButton.Click += TestRefrigerator_Click;
            MainToolBar.Items.Add(refrigeratorTestButton);

            // 레이아웃 테스트 버튼
            var multiTestButton = new Button
            {
                Content = "레이아웃 테스트",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                ToolTip = "여러 객체 배치 테스트"
            };
            multiTestButton.Click += TestMultipleObjects_Click;
            MainToolBar.Items.Add(multiTestButton);

            // 3D 뷰 리셋 버튼
            var resetViewButton = new Button
            {
                Content = "3D 뷰 리셋",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                ToolTip = "3D 카메라 위치 초기화"
            };
            resetViewButton.Click += (s, e) => Viewer3DControl.ResetCamera();
            MainToolBar.Items.Add(resetViewButton);
        }

        // 메뉴 이벤트 핸들러
        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("현재 프로젝트를 초기화하시겠습니까?", "새 프로젝트",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _viewModel.StoreObjects.Clear();
                _viewModel.Walls.Clear();
                DrawingCanvasControl.ClearBackgroundImage();
                _viewModel.StatusText = "새 프로젝트가 생성되었습니다.";
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 프로젝트 열기 구현
            MessageBox.Show("프로젝트 열기 기능은 아직 구현되지 않았습니다.", "알림");
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 프로젝트 저장 구현
            MessageBox.Show("프로젝트 저장 기능은 아직 구현되지 않았습니다.", "알림");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // 도구 버튼 이벤트 핸들러
        private void SelectTool_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CurrentTool = "Select";
        }

        private void WallTool_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CurrentTool = "WallStraight";
        }

        private void PlaceRefrigerator_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectedObjectType = ObjectType.Refrigerator;
            _viewModel.CurrentTool = "PlaceObject";
        }

        private void PlaceShelf_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectedObjectType = ObjectType.Shelf;
            _viewModel.CurrentTool = "PlaceObject";
        }

        private void PlaceCheckout_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectedObjectType = ObjectType.Checkout;
            _viewModel.CurrentTool = "PlaceObject";
        }

        // 모드 전환
        private void DrawModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (DrawModeButton.IsChecked == true)
            {
                LoadModeButton.IsChecked = false;
                _currentEditMode = EditMode.Drawing;
                DrawingModePanel.Visibility = Visibility.Visible;
                LoadingModePanel.Visibility = Visibility.Collapsed;
                _viewModel.StatusText = "그리기 모드";
            }
            else
            {
                DrawModeButton.IsChecked = true;
            }
        }

        private void LoadModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadModeButton.IsChecked == true)
            {
                DrawModeButton.IsChecked = false;
                _currentEditMode = EditMode.Loading;
                DrawingModePanel.Visibility = Visibility.Collapsed;
                LoadingModePanel.Visibility = Visibility.Visible;

                if (_viewModel.CurrentTool == "WallStraight")
                {
                    _viewModel.CurrentTool = "Select";
                }

                _viewModel.StatusText = "도면 불러오기 모드";
            }
            else
            {
                LoadModeButton.IsChecked = true;
            }
        }

        // 도면 불러오기
        private void LoadFloorPlan_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp|모든 파일|*.*",
                Title = "도면 이미지 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
                    DrawingCanvasControl.SetBackgroundImage(bitmap);
                    _viewModel.StatusText = "도면이 로드되었습니다.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"이미지 로드 실패: {ex.Message}", "오류",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 객체 감지
        private void DetectObjects_Click(object sender, RoutedEventArgs e)
        {
            DrawingCanvasControl.DetectObjectsInFloorPlan();
            var count = DrawingCanvasControl.GetDetectedObjectsCount();
            DetectedObjectsCountText.Text = $"{count}개";

            if (count > 0)
            {
                MessageBox.Show($"{count}개의 객체가 감지되었습니다.", "객체 감지 완료");
            }
        }

        // 테스트 메서드
        private void TestRefrigerator_Click(object sender, RoutedEventArgs e)
        {
            var centerX = DrawingCanvasControl.DesignCanvas.ActualWidth / 2;
            var centerY = DrawingCanvasControl.DesignCanvas.ActualHeight / 2;

            var refrigerator = new StoreObject(
                ObjectType.Refrigerator,
                new Point2D(centerX - 18, centerY - 12))
            {
                Temperature = 4.0,
                Layers = 3,
                CategoryCode = "BEVERAGE"
            };

            _viewModel.StoreObjects.Add(refrigerator);
            _viewModel.StatusText = "테스트 냉장고가 추가되었습니다.";
            Viewer3DControl.UpdateView();
        }

        private void TestMultipleObjects_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StoreObjects.Clear();
            _viewModel.Walls.Clear();

            // 매장 외벽
            var storeWidth = 600;
            var storeHeight = 400;
            var offsetX = 50;
            var offsetY = 50;

            // 외벽 생성
            _viewModel.Walls.Add(new Wall(
                new Point2D(offsetX, offsetY),
                new Point2D(offsetX + storeWidth, offsetY)));
            _viewModel.Walls.Add(new Wall(
                new Point2D(offsetX + storeWidth, offsetY),
                new Point2D(offsetX + storeWidth, offsetY + storeHeight)));
            _viewModel.Walls.Add(new Wall(
                new Point2D(offsetX + storeWidth, offsetY + storeHeight),
                new Point2D(offsetX, offsetY + storeHeight)));
            _viewModel.Walls.Add(new Wall(
                new Point2D(offsetX, offsetY + storeHeight),
                new Point2D(offsetX, offsetY)));

            // 냉장고 배치
            for (int i = 0; i < 3; i++)
            {
                var refrigerator = new StoreObject(
                    ObjectType.Refrigerator,
                    new Point2D(offsetX + 20, offsetY + 50 + i * 100))
                {
                    Temperature = 4.0,
                    Layers = 3,
                    IsHorizontal = false
                };
                _viewModel.StoreObjects.Add(refrigerator);
            }

            // 선반 배치
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    var shelf = new StoreObject(
                        ObjectType.Shelf,
                        new Point2D(offsetX + 150 + col * 80, offsetY + 100 + row * 150))
                    {
                        Layers = 4,
                        CategoryCode = "SNACK"
                    };
                    _viewModel.StoreObjects.Add(shelf);
                }
            }

            // 계산대
            var checkout = new StoreObject(
                ObjectType.Checkout,
                new Point2D(offsetX + storeWidth / 2 - 24, offsetY + 20));
            _viewModel.StoreObjects.Add(checkout);

            _viewModel.StatusText = "테스트 레이아웃이 생성되었습니다.";
            DrawingCanvasControl.RedrawAll();
            Viewer3DControl.UpdateView();
        }

        private void UpdateStatistics()
        {
            if (StatisticsText != null)
            {
                StatisticsText.Text = _viewModel.GetStatistics();
            }
        }
    }
}