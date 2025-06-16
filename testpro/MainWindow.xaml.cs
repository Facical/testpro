using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Shapes;
using testpro.ViewModels;
using testpro.Models;
using testpro.Dialogs;
using testpro.Views;

namespace testpro
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private BitmapImage? _loadedFloorPlan;
        private string? _currentObjectTool = null;
        private StoreObject? _selectedObject = null;
        private System.Windows.Controls.Image? _backgroundImage = null;

        private enum EditMode { Drawing, Loading }
        private EditMode _currentEditMode = EditMode.Drawing;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            DrawingCanvasControl.ViewModel = _viewModel;
            DrawingCanvasControl.MainWindow = this;

            Viewer3DControl.ViewModel = _viewModel;

            DrawingCanvasControl.MouseMove += (s, e) =>
            {
                var pos = e.GetPosition(DrawingCanvasControl);
                CoordinatesText.Text = $"좌표: ({pos.X:F0}, {pos.Y:F0})";
            };

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // KeyDown 대신 PreviewKeyDown을 사용하여 키 이벤트를 우선적으로 처리
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            
            // Ctrl+C: 복사
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                DrawingCanvasControl.CopySelectedObject();
                e.Handled = true;
                return;
            }

            // Ctrl+V: 붙여넣기
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Point mousePosition = Mouse.GetPosition(DrawingCanvasControl);
                DrawingCanvasControl.PasteCopiedObject(mousePosition);
                e.Handled = true;
                return;
            }

            // 기타 단축키
            switch (e.Key)
            {
                case Key.F2:
                    _viewModel.CurrentViewMode = ViewMode.View2D;
                    e.Handled = true;
                    break;
                case Key.F3:
                    _viewModel.CurrentViewMode = ViewMode.View3D;
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (_selectedObject != null)
                    {
                        // DrawingService의 RemoveStoreObject를 호출하여 Undo/Redo 스택에 기록
                        _viewModel.DrawingService.RemoveStoreObject(_selectedObject);
                        SelectObject(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    // Escape 키는 DrawingCanvas가 직접 처리하도록 포커스만 전달
                    DrawingCanvasControl.Focus();
                    break;
            }
        }

        private void LayersCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedObject != null && LayersCombo.SelectedIndex >= 0)
            {
                _viewModel.DrawingService.UpdateStoreObject(
                    _selectedObject,
                    _selectedObject.Width,
                    _selectedObject.Length,
                    _selectedObject.Height,
                    LayersCombo.SelectedIndex + 1,
                    _selectedObject.IsHorizontal,
                    _selectedObject.Temperature,
                    _selectedObject.CategoryCode);
            }
        }

        public void SelectObject(StoreObject? obj)
        {
            _selectedObject = obj;
            if (obj != null)
            {
                PropertyPanel.Visibility = Visibility.Visible;
                LayersCombo.SelectedIndex = obj.Layers - 1;
                LayersCombo.Visibility = (obj.Type == ObjectType.Shelf || obj.Type == ObjectType.Refrigerator || obj.Type == ObjectType.Freezer || obj.Type == ObjectType.DisplayStand) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                PropertyPanel.Visibility = Visibility.Collapsed;
            }
        }

        #region Other Unchanged Methods

        private void DrawModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (DrawModeButton.IsChecked == true)
            {
                LoadModeButton.IsChecked = false;
                _currentEditMode = EditMode.Drawing;
                DrawingModePanel.Visibility = Visibility.Visible;

                LoadingModePanel.Visibility = Visibility.Collapsed;

                DrawingCanvasControl.ClearBackgroundImage();
                _viewModel.DrawingService.BackgroundImagePath = null;
                _viewModel.StatusText = "도면 그리기 모드";
            }
            else DrawModeButton.IsChecked = true;
        }

        private void LoadModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoadModeButton.IsChecked == true)
            {
                DrawModeButton.IsChecked = false;
                _currentEditMode = EditMode.Loading;
                DrawingModePanel.Visibility = Visibility.Collapsed;
                LoadingModePanel.Visibility = Visibility.Visible;
                if (_viewModel.CurrentTool == "WallStraight") _viewModel.CurrentTool = "Select";
                _viewModel.StatusText = "도면 불러오기 모드";
            }
            else LoadModeButton.IsChecked = true;
        }

        private void DetectObjects_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedFloorPlan == null)
            {
                MessageBox.Show("먼저 도면 이미지를 불러와주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                DrawingCanvasControl.DetectObjectsInFloorPlan();
                var detectedCount = DrawingCanvasControl.GetDetectedObjectsCount();
                DetectedObjectsCountText.Text = $"{detectedCount}개";
                MessageBox.Show(detectedCount > 0 ? $"{detectedCount}개의 객체가 감지되었습니다." : "도면에서 객체를 찾을 수 없습니다.", "객체 감지 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"객체 감지 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog { Filter = "이미지 파일 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp", Title = "도면 이미지 선택" };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _loadedFloorPlan = new BitmapImage(new Uri(openFileDialog.FileName));
                    var dimensionDialog = new DimensionInputDialog { Owner = this };
                    if (dimensionDialog.ShowDialog() == true)
                    {
                        Mouse.OverrideCursor = Cursors.Wait;
                        _viewModel.DrawingService.Clear();
                        _viewModel.DrawingService.BackgroundImagePath = openFileDialog.FileName;
                        DrawingCanvasControl.SetBackgroundImage(openFileDialog.FileName);
                        LoadedImageName.Text = $"불러온 이미지: {System.IO.Path.GetFileName(openFileDialog.FileName)}";
                        var result = MessageBox.Show("도면에 벽을 정확히 맞추시겠습니까?\n\n예: 도면 경계에 정확히 맞춤\n아니오: 입력한 크기의 비율 유지", "벽 생성 옵션", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes) CreateOuterWallsWithDimensionsExact(dimensionDialog.WidthInInches, dimensionDialog.HeightInInches);
                        else CreateOuterWallsWithDimensions(dimensionDialog.WidthInInches, dimensionDialog.HeightInInches);
                    }
                }
                catch (Exception ex) { MessageBox.Show($"이미지를 불러오는 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error); }
                finally { Mouse.OverrideCursor = null; }
            }
        }

        private void CreateOuterWallsWithDimensionsExact(double widthInInches, double heightInInches)
        {
            // DrawingCanvasControl의 public 속성을 통해 실제 Image 컨트롤을 가져옵니다.
            var backgroundImage = DrawingCanvasControl.BackgroundImage;

            if (backgroundImage == null || _loadedFloorPlan == null)
            {
                CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
                return;
            }

            try
            {
                var analyzer = new FloorPlanAnalyzer();
                var bounds = analyzer.FindFloorPlanBounds(_loadedFloorPlan);

                if (bounds != null)
                {
                    // 실제 표시된 이미지의 위치와 크기를 사용합니다.
                    double imageLeft = Canvas.GetLeft(backgroundImage);
                    double imageTop = Canvas.GetTop(backgroundImage);
                    double imageWidth = backgroundImage.Width;
                    double imageHeight = backgroundImage.Height;

                    // 이미지 좌표를 캔버스 좌표로 변환
                    double wallLeft = imageLeft + (imageWidth * ((double)bounds.Left / _loadedFloorPlan.PixelWidth));
                    double wallTop = imageTop + (imageHeight * ((double)bounds.Top / _loadedFloorPlan.PixelHeight));
                    double wallRight = imageLeft + (imageWidth * ((double)bounds.Right / _loadedFloorPlan.PixelWidth));
                    double wallBottom = imageTop + (imageHeight * ((double)bounds.Bottom / _loadedFloorPlan.PixelHeight));

                    // 외벽 생성
                    var topWall = _viewModel.DrawingService.AddWall(new Point2D(wallLeft, wallTop), new Point2D(wallRight, wallTop));
                    topWall.RealLengthInInches = widthInInches;

                    var rightWall = _viewModel.DrawingService.AddWall(new Point2D(wallRight, wallTop), new Point2D(wallRight, wallBottom));
                    rightWall.RealLengthInInches = heightInInches;

                    var bottomWall = _viewModel.DrawingService.AddWall(new Point2D(wallRight, wallBottom), new Point2D(wallLeft, wallBottom));
                    bottomWall.RealLengthInInches = widthInInches;

                    var leftWall = _viewModel.DrawingService.AddWall(new Point2D(wallLeft, wallBottom), new Point2D(wallLeft, wallTop));
                    leftWall.RealLengthInInches = heightInInches;

                    // 스케일 설정
                    _viewModel.DrawingService.SetScaleXY((wallRight - wallLeft) / widthInInches, (wallBottom - wallTop) / heightInInches);
                }
                else
                {
                    // 경계를 찾지 못했을 경우 기본 크기로 생성
                    CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"도면 처리 중 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
            }
        }

        private void CreateOuterWallsWithDimensions(double widthInInches, double heightInInches)
        {
            if (_backgroundImage == null || _loadedFloorPlan == null) { CreateDefaultOuterWallsWithSize(widthInInches, heightInInches); return; }
            CreateOuterWallsWithDimensionsExact(widthInInches, heightInInches);
        }

        private void CreateDefaultOuterWallsWithSize(double widthInInches, double heightInInches)
        {
            double canvasWidth = 2000, canvasHeight = 2000;
            double maxWidth = canvasWidth - 200, maxHeight = canvasHeight - 200;
            double scale = Math.Min(maxWidth / widthInInches, maxHeight / heightInInches);
            double scaledWidth = widthInInches * scale, scaledHeight = heightInInches * scale;
            double startX = (canvasWidth - scaledWidth) / 2, startY = (canvasHeight - scaledHeight) / 2;
            var topWall = _viewModel.DrawingService.AddWall(new Point2D(startX, startY), new Point2D(startX + scaledWidth, startY)); topWall.RealLengthInInches = widthInInches;
            var rightWall = _viewModel.DrawingService.AddWall(new Point2D(startX + scaledWidth, startY), new Point2D(startX + scaledWidth, startY + scaledHeight)); rightWall.RealLengthInInches = heightInInches;
            var bottomWall = _viewModel.DrawingService.AddWall(new Point2D(startX + scaledWidth, startY + scaledHeight), new Point2D(startX, startY + scaledHeight)); bottomWall.RealLengthInInches = widthInInches;
            var leftWall = _viewModel.DrawingService.AddWall(new Point2D(startX, startY + scaledHeight), new Point2D(startX, startY)); leftWall.RealLengthInInches = heightInInches;
            _viewModel.DrawingService.SetScaleXY(scale, scale);
        }

        private void ShelfTool_Click(object sender, RoutedEventArgs e) { SetObjectTool("Shelf", "진열대"); }
        private void RefrigeratorTool_Click(object sender, RoutedEventArgs e) { SetObjectTool("Refrigerator", "냉장고"); }
        private void FreezerTool_Click(object sender, RoutedEventArgs e) { SetObjectTool("Freezer", "냉동고"); }
        private void CheckoutTool_Click(object sender, RoutedEventArgs e) { SetObjectTool("Checkout", "계산대"); }
        private void DisplayStandTool_Click(object sender, RoutedEventArgs e) { SetObjectTool("Shelf", "진열대"); }

        private void SetObjectTool(string toolName, string displayName)
        {
            _currentObjectTool = toolName;
            _viewModel.CurrentTool = "PlaceObject";
            _viewModel.StatusText = $"{displayName} 배치: 영역을 드래그하여 지정하세요";
        }

        public string? GetCurrentObjectTool() => _currentObjectTool;

        public void OnObjectPlaced(StoreObject obj)
        {
            SelectObject(obj);
            _viewModel.CurrentTool = "Select";
        }

        private void StraightWallRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CurrentTool = "WallStraight";
                if (_viewModel.CurrentViewMode == ViewMode.View2D) DrawingCanvasControl.Focus();
            }
        }

        private void WallTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && StraightWallRadio.IsChecked == false) _viewModel.CurrentTool = "Select";
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.CurrentTool))
            {
                UpdateCursor();
                if (_viewModel.CurrentTool != "WallStraight") StraightWallRadio.IsChecked = false;
                if (_viewModel.CurrentTool == "WallStraight" && _viewModel.CurrentViewMode == ViewMode.View2D) DrawingCanvasControl.Focus();
            }
            else if (e.PropertyName == nameof(_viewModel.CurrentViewMode)) OnViewModeChanged();
        }

        private void OnViewModeChanged()
        {
            if (_viewModel.CurrentViewMode == ViewMode.View3D && Viewer3DControl != null)
            {
                Viewer3DControl.UpdateAll3DModels();
                Viewer3DControl.Focus();
                Viewer3DControl.FocusOn3DModel();
            }
            UpdateStatusBarViewMode();
        }

        private void UpdateStatusBarViewMode() => ViewModeText.Text = _viewModel.CurrentViewMode == ViewMode.View2D ? "2D 편집 모드" : "3D 시각화 모드";

        private void UpdateCursor()
        {
            if (_viewModel.CurrentViewMode == ViewMode.View2D)
            {
                DrawingCanvasControl.Cursor = _viewModel.CurrentTool switch
                {
                    "WallStraight" or "PlaceObject" => Cursors.Cross,
                    _ => Cursors.Arrow,
                };
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("정말로 종료하시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) Close();
        }
        #endregion
    }
}