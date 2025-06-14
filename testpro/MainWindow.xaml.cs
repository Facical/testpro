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
using testpro.Views; // DrawingCanvas 네임스페이스 추가

namespace testpro
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private BitmapImage? _loadedFloorPlan;
        private string? _currentObjectTool = null;
        private StoreObject? _selectedObject = null;
        private System.Windows.Controls.Image? _backgroundImage = null;

        private enum EditMode
        {
            Drawing,
            Loading
        }

        private EditMode _currentEditMode = EditMode.Drawing;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Set up the drawing canvas with the view model
            DrawingCanvasControl.ViewModel = _viewModel;
            DrawingCanvasControl.MainWindow = this;

            // Set up the 3D viewer with the view model
            Viewer3DControl.ViewModel = _viewModel;

            // Handle mouse move for coordinate display
            DrawingCanvasControl.MouseMove += (s, e) =>
            {
                var pos = e.GetPosition(DrawingCanvasControl);
                CoordinatesText.Text = $"좌표: ({pos.X:F0}, {pos.Y:F0})";
            };

            // Set up property change notifications
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // 키 이벤트
            KeyDown += MainWindow_KeyDown;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        // 모드 전환 버튼 이벤트
        private void DrawModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (DrawModeButton.IsChecked == true)
            {
                LoadModeButton.IsChecked = false;
                _currentEditMode = EditMode.Drawing;
                DrawingModePanel.Visibility = Visibility.Visible;
                LoadingModePanel.Visibility = Visibility.Collapsed;

                // 배경 이미지 제거
                DrawingCanvasControl.ClearBackgroundImage();
                _viewModel.DrawingService.BackgroundImagePath = null;

                // 상태 업데이트
                _viewModel.StatusText = "도면 그리기 모드";
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

                // 벽 그리기 도구 비활성화
                if (_viewModel.CurrentTool == "WallStraight")
                {
                    _viewModel.CurrentTool = "Select";
                }

                // 상태 업데이트
                _viewModel.StatusText = "도면 불러오기 모드";
            }
            else
            {
                LoadModeButton.IsChecked = true;
            }
        }

        private void DetectObjects_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedFloorPlan == null)
            {
                MessageBox.Show("먼저 도면 이미지를 불러와주세요.", "알림",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("=== 객체 감지 시작 ===");

                // DrawingCanvas의 감지 메서드 호출
                DrawingCanvasControl.DetectObjectsInFloorPlan();

                // 감지된 객체 수 업데이트
                var detectedCount = DrawingCanvasControl.GetDetectedObjectsCount();
                DetectedObjectsCountText.Text = $"{detectedCount}개";

                System.Diagnostics.Debug.WriteLine($"=== 객체 감지 완료: {detectedCount}개 ===");

                if (detectedCount > 0)
                {
                    MessageBox.Show($"{detectedCount}개의 객체가 감지되었습니다.\n" +
                                  "• 진열대는 개별 섹션으로 분리되어 표시됩니다.\n" +
                                  "• 각 객체 위에 마우스를 올려 파란색으로 표시되면 클릭하여 타입을 지정할 수 있습니다.",
                                  "객체 감지 완료",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("도면에서 객체를 찾을 수 없습니다.\n" +
                                  "도면의 선이 명확하게 표시되어 있는지 확인해주세요.",
                                  "알림",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"객체 감지 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"스택 추적:\n{ex.StackTrace}");

                MessageBox.Show($"객체 감지 중 오류가 발생했습니다:\n{ex.Message}", "오류",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "이미지 파일 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "도면 이미지 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // BitmapImage 로드
                    _loadedFloorPlan = new BitmapImage(new Uri(openFileDialog.FileName));

                    // 크기 입력 다이얼로그 표시
                    var dimensionDialog = new DimensionInputDialog();
                    dimensionDialog.Owner = this;

                    if (dimensionDialog.ShowDialog() == true)
                    {
                        Mouse.OverrideCursor = Cursors.Wait;

                        // 기존 데이터 초기화
                        _viewModel.DrawingService.Clear();

                        // 배경 이미지 설정
                        _viewModel.DrawingService.BackgroundImagePath = openFileDialog.FileName;
                        DrawingCanvasControl.SetBackgroundImage(openFileDialog.FileName);

                        // 파일명 표시
                        LoadedImageName.Text = $"불러온 이미지: {System.IO.Path.GetFileName(openFileDialog.FileName)}";

                        // 비율 유지 여부 묻기
                        var result = MessageBox.Show(
                            "도면에 벽을 정확히 맞추시겠습니까?\n\n" +
                            "예: 도면 경계에 정확히 맞춤 (비율이 다를 수 있음)\n" +
                            "아니오: 입력한 크기의 비율 유지",
                            "벽 생성 옵션",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            // 도면에 정확히 맞춤
                            CreateOuterWallsWithDimensionsExact(dimensionDialog.WidthInInches, dimensionDialog.HeightInInches);
                        }
                        else
                        {
                            // 비율 유지하며 생성
                            CreateOuterWallsWithDimensions(dimensionDialog.WidthInInches, dimensionDialog.HeightInInches);
                        }

                        _viewModel.StatusText = $"도면 불러오기 완료. 외곽벽 생성 ({dimensionDialog.WidthInInches / 12:F0}'-{dimensionDialog.WidthInInches % 12:F0}\" x {dimensionDialog.HeightInInches / 12:F0}'-{dimensionDialog.HeightInInches % 12:F0}\")";
                    }
                    else
                    {
                        // 다이얼로그 취소
                        _viewModel.StatusText = "도면 불러오기가 취소되었습니다.";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"이미지를 불러오는 중 오류가 발생했습니다:\n{ex.Message}",
                                  "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    _viewModel.StatusText = "도면 불러오기 실패";
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        private void CreateOuterWallsWithDimensionsExact(double widthInInches, double heightInInches)
        {
            if (_backgroundImage != null && _loadedFloorPlan != null)
            {
                try
                {
                    // 도면의 실제 경계 찾기
                    var analyzer = new FloorPlanAnalyzer();
                    var floorPlanBounds = analyzer.FindFloorPlanBounds(_loadedFloorPlan);

                    if (floorPlanBounds != null)
                    {
                        // 디버깅 정보
                        System.Diagnostics.Debug.WriteLine($"[정확히 맞춤] 도면 경계: {floorPlanBounds.Left}, {floorPlanBounds.Top}, {floorPlanBounds.Width}x{floorPlanBounds.Height}");
                        System.Diagnostics.Debug.WriteLine($"[정확히 맞춤] 실제 도면 크기: {widthInInches / 12:F0}'-{widthInInches % 12:F0}\" x {heightInInches / 12:F0}'-{heightInInches % 12:F0}\"");

                        // DrawingCanvas에서 실제 배경 이미지의 위치와 크기 가져오기
                        double imageLeft = Canvas.GetLeft(_backgroundImage);
                        double imageTop = Canvas.GetTop(_backgroundImage);
                        double imageWidth = _backgroundImage.Width;
                        double imageHeight = _backgroundImage.Height;

                        // 도면 경계가 전체 이미지에서 차지하는 비율 계산
                        double boundsRatioLeft = (double)floorPlanBounds.Left / _loadedFloorPlan.PixelWidth;
                        double boundsRatioTop = (double)floorPlanBounds.Top / _loadedFloorPlan.PixelHeight;
                        double boundsRatioRight = (double)floorPlanBounds.Right / _loadedFloorPlan.PixelWidth;
                        double boundsRatioBottom = (double)floorPlanBounds.Bottom / _loadedFloorPlan.PixelHeight;

                        // 캔버스에서의 실제 도면 위치 계산 (이미지 상의 도면 경계를 캔버스 좌표로 변환)
                        double wallLeft = imageLeft + (imageWidth * boundsRatioLeft);
                        double wallTop = imageTop + (imageHeight * boundsRatioTop);
                        double wallRight = imageLeft + (imageWidth * boundsRatioRight);
                        double wallBottom = imageTop + (imageHeight * boundsRatioBottom);

                        // 정확한 너비와 높이 계산
                        double wallWidth = wallRight - wallLeft;
                        double wallHeight = wallBottom - wallTop;

                        // 그리드에 맞춤 (선택적)
                        if (SnapToGridCheckBox?.IsChecked == true)
                        {
                            wallLeft = Math.Round(wallLeft / 12.0) * 12.0;
                            wallTop = Math.Round(wallTop / 12.0) * 12.0;
                            wallRight = Math.Round(wallRight / 12.0) * 12.0;
                            wallBottom = Math.Round(wallBottom / 12.0) * 12.0;

                            // 그리드 스냅 후 크기 재계산
                            wallWidth = wallRight - wallLeft;
                            wallHeight = wallBottom - wallTop;
                        }

                        // 실제 크기와 캔버스 크기의 비율 계산 (정보용)
                        double scaleX = wallWidth / widthInInches;
                        double scaleY = wallHeight / heightInInches;

                        // 디버깅 정보
                        System.Diagnostics.Debug.WriteLine($"[정확히 맞춤] 벽 생성 위치: Left={wallLeft:F1}, Top={wallTop:F1}, Right={wallRight:F1}, Bottom={wallBottom:F1}");
                        System.Diagnostics.Debug.WriteLine($"[정확히 맞춤] 벽 크기: {wallWidth:F1} x {wallHeight:F1}");
                        System.Diagnostics.Debug.WriteLine($"[정확히 맞춤] 스케일: X={scaleX:F2}, Y={scaleY:F2}");

                        // 4개의 외곽벽 생성 (도면에 정확히 맞춤)
                        // 상단 벽 (가로)
                        var topWall = _viewModel.DrawingService.AddWall(
                            new Point2D(wallLeft, wallTop),
                            new Point2D(wallRight, wallTop)
                        );
                        topWall.RealLengthInInches = widthInInches;

                        // 우측 벽 (세로)
                        var rightWall = _viewModel.DrawingService.AddWall(
                            new Point2D(wallRight, wallTop),
                            new Point2D(wallRight, wallBottom)
                        );
                        rightWall.RealLengthInInches = heightInInches;

                        // 하단 벽 (가로)
                        var bottomWall = _viewModel.DrawingService.AddWall(
                            new Point2D(wallRight, wallBottom),
                            new Point2D(wallLeft, wallBottom)
                        );
                        bottomWall.RealLengthInInches = widthInInches;

                        // 좌측 벽 (세로)
                        var leftWall = _viewModel.DrawingService.AddWall(
                            new Point2D(wallLeft, wallBottom),
                            new Point2D(wallLeft, wallTop)
                        );
                        leftWall.RealLengthInInches = heightInInches;

                        // 스케일 정보 저장
                        _viewModel.DrawingService.SetScaleXY(scaleX, scaleY);

                        // 화면 업데이트
                        DrawingCanvasControl.RedrawAll();

                        _viewModel.StatusText = $"도면에 정확히 맞춰 벽 생성 완료 ({widthInInches / 12:F0}'-{widthInInches % 12:F0}\" x {heightInInches / 12:F0}'-{heightInInches % 12:F0}\")";
                    }
                    else
                    {
                        // 도면 경계를 찾지 못한 경우
                        MessageBox.Show("도면의 외곽선을 찾을 수 없습니다. 기본 크기로 벽을 생성합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"외곽벽 생성 중 오류: {ex.Message}");
                    MessageBox.Show($"도면 처리 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
                }
            }
            else
            {
                CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
            }
        }

        private void CreateOuterWallsWithDimensions(double widthInInches, double heightInInches)
        {
            if (_backgroundImage != null && _loadedFloorPlan != null)
            {
                try
                {
                    // 도면의 실제 경계 찾기
                    var analyzer = new FloorPlanAnalyzer();
                    var floorPlanBounds = analyzer.FindFloorPlanBounds(_loadedFloorPlan);

                    if (floorPlanBounds != null)
                    {
                        // 디버깅 정보
                        System.Diagnostics.Debug.WriteLine($"도면 경계: {floorPlanBounds.Left}, {floorPlanBounds.Top}, {floorPlanBounds.Width}x{floorPlanBounds.Height}");
                        System.Diagnostics.Debug.WriteLine($"실제 도면 크기: {widthInInches / 12:F0}'-{widthInInches % 12:F0}\" x {heightInInches / 12:F0}'-{heightInInches % 12:F0}\"");

                        // DrawingCanvas에서 실제 배경 이미지의 위치와 크기 가져오기
                        double imageLeft = Canvas.GetLeft(_backgroundImage);
                        double imageTop = Canvas.GetTop(_backgroundImage);
                        double imageWidth = _backgroundImage.Width;
                        double imageHeight = _backgroundImage.Height;

                        // 도면 경계가 전체 이미지에서 차지하는 비율 계산
                        double boundsRatioLeft = (double)floorPlanBounds.Left / _loadedFloorPlan.PixelWidth;
                        double boundsRatioTop = (double)floorPlanBounds.Top / _loadedFloorPlan.PixelHeight;
                        double boundsRatioWidth = (double)floorPlanBounds.Width / _loadedFloorPlan.PixelWidth;
                        double boundsRatioHeight = (double)floorPlanBounds.Height / _loadedFloorPlan.PixelHeight;

                        // 캔버스에서의 실제 도면 위치 계산 (이미지 상의 도면 경계를 캔버스 좌표로 변환)
                        double floorPlanLeft = imageLeft + (imageWidth * boundsRatioLeft);
                        double floorPlanTop = imageTop + (imageHeight * boundsRatioTop);
                        double floorPlanWidth = imageWidth * boundsRatioWidth;
                        double floorPlanHeight = imageHeight * boundsRatioHeight;

                        // 실제 크기와 캔버스 크기의 비율 계산 (각 축별로)
                        double scaleX = floorPlanWidth / widthInInches;
                        double scaleY = floorPlanHeight / heightInInches;

                        // 비율이 다르면 경고만 표시하고 계속 진행
                        double scaleDiff = Math.Abs(scaleX - scaleY);
                        double avgScale = (scaleX + scaleY) / 2.0;

                        if (scaleDiff > avgScale * 0.1) // 10% 이상 차이나면
                        {
                            System.Diagnostics.Debug.WriteLine($"경고: X축과 Y축 스케일 차이가 큽니다. X: {scaleX:F2}, Y: {scaleY:F2}");
                        }

                        // 벽을 도면 경계에 정확히 맞춤 (중앙 정렬 없이)
                        double wallLeft = floorPlanLeft;
                        double wallTop = floorPlanTop;
                        double wallWidth = floorPlanWidth;
                        double wallHeight = floorPlanHeight;

                        // 그리드에 맞춤
                        if (SnapToGridCheckBox?.IsChecked == true)
                        {
                            wallLeft = Math.Round(wallLeft / 12.0) * 12.0;
                            wallTop = Math.Round(wallTop / 12.0) * 12.0;
                            wallWidth = Math.Round(wallWidth / 12.0) * 12.0;
                            wallHeight = Math.Round(wallHeight / 12.0) * 12.0;
                        }

                        // 디버깅 정보
                        System.Diagnostics.Debug.WriteLine($"벽 생성 위치: {wallLeft}, {wallTop}, {wallWidth}x{wallHeight}");

                        // 4개의 외곽벽 생성
                        // 상단 벽 (가로)
                        var topWall = _viewModel.DrawingService.AddWall(
                            new Point2D(wallLeft, wallTop),
                            new Point2D(wallLeft + wallWidth, wallTop)
                        );
                        topWall.RealLengthInInches = widthInInches;

                        // 우측 벽 (세로)
                        var rightWall = _viewModel.DrawingService.AddWall(
                            new Point2D(wallLeft + wallWidth, wallTop),
                            new Point2D(wallLeft + wallWidth, wallTop + wallHeight)
                        );
                        rightWall.RealLengthInInches = heightInInches;

                        // 하단 벽 (가로)
                        var bottomWall = _viewModel.DrawingService.AddWall(
                            new Point2D(wallLeft + wallWidth, wallTop + wallHeight),
                            new Point2D(wallLeft, wallTop + wallHeight)
                        );
                        bottomWall.RealLengthInInches = widthInInches;

                        // 좌측 벽 (세로)
                        var leftWall = _viewModel.DrawingService.AddWall(
                            new Point2D(wallLeft, wallTop + wallHeight),
                            new Point2D(wallLeft, wallTop)
                        );
                        leftWall.RealLengthInInches = heightInInches;

                        // 스케일 정보 저장
                        _viewModel.DrawingService.SetScaleXY(scaleX, scaleY);

                        // 화면 업데이트
                        DrawingCanvasControl.RedrawAll();

                        _viewModel.StatusText = $"도면 외곽선에 맞춰 벽 생성 완료 ({widthInInches / 12:F0}'-{widthInInches % 12:F0}\" x {heightInInches / 12:F0}'-{heightInInches % 12:F0}\")";
                    }
                    else
                    {
                        // 도면 경계를 찾지 못한 경우
                        MessageBox.Show("도면의 외곽선을 찾을 수 없습니다. 기본 크기로 벽을 생성합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"외곽벽 생성 중 오류: {ex.Message}");
                    MessageBox.Show($"도면 처리 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
                }
            }
            else
            {
                CreateDefaultOuterWallsWithSize(widthInInches, heightInInches);
            }
        }

        // 배경 이미지 없이 크기만으로 벽 생성
        private void CreateDefaultOuterWallsWithSize(double widthInInches, double heightInInches)
        {
            // 캔버스 크기
            double canvasWidth = 2000;
            double canvasHeight = 2000;

            // 여백을 고려한 최대 크기
            double maxWidth = canvasWidth - 200;
            double maxHeight = canvasHeight - 200;

            // 스케일 계산 (실제 크기를 캔버스에 맞춤)
            double scaleX = maxWidth / widthInInches;
            double scaleY = maxHeight / heightInInches;
            double scale = Math.Min(scaleX, scaleY);

            // 스케일 적용된 크기
            double scaledWidth = widthInInches * scale;
            double scaledHeight = heightInInches * scale;

            // 중앙 정렬을 위한 시작점
            double startX = (canvasWidth - scaledWidth) / 2;
            double startY = (canvasHeight - scaledHeight) / 2;

            // 그리드에 맞춤
            if (SnapToGridCheckBox?.IsChecked == true)
            {
                startX = Math.Round(startX / 12.0) * 12.0;
                startY = Math.Round(startY / 12.0) * 12.0;
                scaledWidth = Math.Round(scaledWidth / 12.0) * 12.0;
                scaledHeight = Math.Round(scaledHeight / 12.0) * 12.0;
            }

            // 4개의 외곽벽 생성 (실제 길이 정보 포함)
            // 상단 벽 (가로)
            var topWall = _viewModel.DrawingService.AddWall(
                new Point2D(startX, startY),
                new Point2D(startX + scaledWidth, startY)
            );
            topWall.RealLengthInInches = widthInInches;

            // 우측 벽 (세로)
            var rightWall = _viewModel.DrawingService.AddWall(
                new Point2D(startX + scaledWidth, startY),
                new Point2D(startX + scaledWidth, startY + scaledHeight)
            );
            rightWall.RealLengthInInches = heightInInches;

            // 하단 벽 (가로)
            var bottomWall = _viewModel.DrawingService.AddWall(
                new Point2D(startX + scaledWidth, startY + scaledHeight),
                new Point2D(startX, startY + scaledHeight)
            );
            bottomWall.RealLengthInInches = widthInInches;

            // 좌측 벽 (세로)
            var leftWall = _viewModel.DrawingService.AddWall(
                new Point2D(startX, startY + scaledHeight),
                new Point2D(startX, startY)
            );
            leftWall.RealLengthInInches = heightInInches;

            // 화면 업데이트
            DrawingCanvasControl.RedrawAll();

            // 스케일 정보 저장
            _viewModel.DrawingService.SetScaleXY(scale, scale);

            _viewModel.StatusText = $"벽 생성 완료 ({widthInInches / 12:F0}'-{widthInInches % 12:F0}\" x {heightInInches / 12:F0}'-{heightInInches % 12:F0}\")";
        }

        // 그리드 스냅 헬퍼 메서드
        private Point2D SnapToGrid(Point2D point)
        {
            const double gridSize = 12.0; // 1피트
            return new Point2D(
                Math.Round(point.X / gridSize) * gridSize,
                Math.Round(point.Y / gridSize) * gridSize
            );
        }

        // 객체 도구 클릭 이벤트들
        private void ShelfTool_Click(object sender, RoutedEventArgs e)
        {
            _currentObjectTool = "Shelf";
            _viewModel.CurrentTool = "PlaceObject";
            _viewModel.StatusText = "진열대 배치: 영역을 드래그하여 지정하세요";
        }

        private void RefrigeratorTool_Click(object sender, RoutedEventArgs e)
        {
            _currentObjectTool = "Refrigerator";
            _viewModel.CurrentTool = "PlaceObject";
            _viewModel.StatusText = "냉장고 배치: 영역을 드래그하여 지정하세요";
        }

        private void FreezerTool_Click(object sender, RoutedEventArgs e)
        {
            _currentObjectTool = "Freezer";
            _viewModel.CurrentTool = "PlaceObject";
            _viewModel.StatusText = "냉동고 배치: 영역을 드래그하여 지정하세요";
        }

        private void CheckoutTool_Click(object sender, RoutedEventArgs e)
        {
            _currentObjectTool = "Checkout";
            _viewModel.CurrentTool = "PlaceObject";
            _viewModel.StatusText = "계산대 배치: 영역을 드래그하여 지정하세요";
        }

        private void DisplayStandTool_Click(object sender, RoutedEventArgs e)
        {
            _currentObjectTool = "DisplayStand";
            _viewModel.CurrentTool = "PlaceObject";
            _viewModel.StatusText = "진열대 배치: 영역을 드래그하여 지정하세요";
        }

        // 층수 변경
        private void LayersCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedObject != null && LayersCombo.SelectedIndex >= 0)
            {
                _viewModel.DrawingService.UpdateStoreObject(_selectedObject,
                    _selectedObject.Height,
                    LayersCombo.SelectedIndex + 1,
                    _selectedObject.IsHorizontal);
                DrawingCanvasControl.RedrawAll();
            }
        }

        // public 메서드들
        public string? GetCurrentObjectTool()
        {
            return _currentObjectTool;
        }

        public void SelectObject(StoreObject? obj)
        {
            _selectedObject = obj;

            if (obj != null)
            {
                PropertyPanel.Visibility = Visibility.Visible;
                LayersCombo.SelectedIndex = obj.Layers - 1;

                // 객체 타입에 따라 층수 설정 표시 여부 결정
                if (obj.Type == ObjectType.Shelf || obj.Type == ObjectType.Refrigerator ||
                    obj.Type == ObjectType.Freezer || obj.Type == ObjectType.DisplayStand)
                {
                    LayersCombo.Visibility = Visibility.Visible;
                }
                else
                {
                    LayersCombo.Visibility = Visibility.Collapsed;
                }

                _viewModel.StatusText = $"{obj.GetDisplayName()} 선택됨";
            }
            else
            {
                PropertyPanel.Visibility = Visibility.Collapsed;
            }
        }

        public void OnObjectPlaced(StoreObject obj)
        {
            SelectObject(obj);
            _viewModel.CurrentTool = "Select";
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
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
                        _viewModel.DrawingService.RemoveStoreObject(_selectedObject);
                        SelectObject(null);
                        DrawingCanvasControl.RedrawAll();
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    if (_viewModel?.CurrentTool == "WallStraight" && _viewModel.CurrentViewMode == ViewMode.View2D)
                    {
                        DrawingCanvasControl.Focus();
                        var keyEventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Escape)
                        {
                            RoutedEvent = KeyDownEvent
                        };
                        DrawingCanvasControl.RaiseEvent(keyEventArgs);
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _viewModel?.CurrentTool == "WallStraight" && _viewModel.CurrentViewMode == ViewMode.View2D)
            {
                var drawingCanvas = DrawingCanvasControl;
                if (drawingCanvas != null)
                {
                    var cancelMethod = drawingCanvas.GetType().GetMethod("CancelWallDrawing",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    cancelMethod?.Invoke(drawingCanvas, null);
                }
                e.Handled = true;
            }
        }

        private void StraightWallRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CurrentTool = "WallStraight";
                if (_viewModel.CurrentViewMode == ViewMode.View2D)
                {
                    DrawingCanvasControl.Focus();
                }
            }
        }

        private void WallTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && StraightWallRadio.IsChecked == false)
            {
                _viewModel.CurrentTool = "Select";
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.CurrentTool))
            {
                UpdateCursor();

                if (_viewModel.CurrentTool != "WallStraight")
                {
                    StraightWallRadio.IsChecked = false;
                }

                if (_viewModel.CurrentTool == "WallStraight" && _viewModel.CurrentViewMode == ViewMode.View2D)
                {
                    DrawingCanvasControl.Focus();
                }
            }
            else if (e.PropertyName == nameof(_viewModel.CurrentViewMode))
            {
                OnViewModeChanged();
            }
        }

        private void OnViewModeChanged()
        {
            switch (_viewModel.CurrentViewMode)
            {
                case ViewMode.View2D:
                    if (_viewModel.CurrentTool == "WallStraight")
                    {
                        DrawingCanvasControl.Focus();
                    }
                    break;

                case ViewMode.View3D:
                    if (Viewer3DControl != null)
                    {
                        Viewer3DControl.UpdateAll3DModels();
                        Viewer3DControl.Focus();
                        Viewer3DControl.FocusOn3DModel();
                    }
                    break;
            }

            UpdateStatusBarViewMode();
        }

        private void UpdateStatusBarViewMode()
        {
            ViewModeText.Text = _viewModel.CurrentViewMode == ViewMode.View2D ? "2D 편집 모드" : "3D 시각화 모드";
        }

        private void UpdateCursor()
        {
            if (_viewModel.CurrentViewMode == ViewMode.View2D)
            {
                switch (_viewModel.CurrentTool)
                {
                    case "WallStraight":
                        DrawingCanvasControl.Cursor = Cursors.Cross;
                        break;
                    case "PlaceObject":
                        DrawingCanvasControl.Cursor = Cursors.Cross;
                        break;
                    case "Select":
                        DrawingCanvasControl.Cursor = Cursors.Arrow;
                        break;
                    default:
                        DrawingCanvasControl.Cursor = Cursors.Arrow;
                        break;
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("정말로 종료하시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Close();
            }
        }
    }
}