using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using testpro.Models;
using testpro.Services;
using testpro.ViewModels;
using testpro.Dialogs;

namespace testpro.Views
{
    public partial class DrawingCanvas : UserControl
    {
        // 기존 필드들
        private MainViewModel _viewModel;
        private Point2D _tempStartPoint;
        private bool _isDrawingWall = false;
        private Rectangle _previewWall;
        private const double GridSize = 12.0;
        private double _zoomFactor = 1.0;
        private Point _lastPanPoint;
        private bool _isPanning = false;

        // 배경 이미지 관련
        private Image _backgroundImage;
        private BitmapImage _loadedFloorPlan; // 이 필드가 없으면 추가

        // 객체 배치 관련
        private bool _isDrawingObject = false;
        private Point2D _objectStartPoint;
        private Rectangle _objectPreview;
        private StoreObject _selectedObject;
        private StoreObject _hoveredObject;
        private Rectangle _hoverHighlight;
        private bool _isDraggingObject = false;
        private Point2D _dragOffset;

        // 객체 감지 관련 필드들 추가
        private List<DetectedObject> _detectedObjects = new List<DetectedObject>();
        private Canvas _detectedObjectsCanvas;
        private DetectedObject _hoveredDetectedObject;

        public MainWindow MainWindow { get; set; }

        public MainViewModel ViewModel
        {
            get => _viewModel;
            set
            {
                _viewModel = value;
                DataContext = _viewModel;
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                }
            }
        }

        public DrawingCanvas()
        {
            InitializeComponent();


            // 감지된 객체 오버레이를 위한 캔버스 추가
            _detectedObjectsCanvas = new Canvas
            {
                IsHitTestVisible = true
            };
            MainCanvas.Children.Add(_detectedObjectsCanvas);
            Canvas.SetZIndex(_detectedObjectsCanvas, 10); // 위에 표시

            Loaded += DrawingCanvas_Loaded;
            SizeChanged += DrawingCanvas_SizeChanged;

            // 키 이벤트를 위해 포커스 가능하도록 설정
            Focusable = true;

            // 마우스 엔터 시 포커스 설정
            MouseEnter += (s, e) => Focus();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.CurrentTool))
            {
                UpdateMousePointerVisibility();

                // Cancel any ongoing drawing when tool changes
                if (_isDrawingWall && _viewModel.CurrentTool != "WallStraight")
                {
                    CancelWallDrawing();
                }

                // Cancel object drawing when tool changes
                if (_isDrawingObject && _viewModel.CurrentTool != "PlaceObject")
                {
                    CancelObjectDrawing();
                }
            }
        }


        // 감지된 객체 수 반환
        public int GetDetectedObjectsCount()
        {
            return _detectedObjects.Count;
        }

       
        // 감지된 객체가 StoreObject로 변환되었는지 확인
        public bool IsDetectedObjectConverted(DetectedObject obj)
        {
            return obj.ConvertedStoreObject != null;
        }

        // 모든 감지된 객체를 StoreObject로 일괄 변환
        public void ConvertAllDetectedObjects()
        {
            foreach (var obj in _detectedObjects)
            {
                if (!IsDetectedObjectConverted(obj) && obj.Type != DetectedObjectType.Unknown)
                {
                    var storeObject = obj.ToStoreObject();
                    _viewModel.DrawingService.AddStoreObject(storeObject.Type,
                        new Point2D(obj.Bounds.Left, obj.Bounds.Top));

                    var addedObject = _viewModel.DrawingService.StoreObjects.Last();
                    addedObject.Width = obj.Bounds.Width;
                    addedObject.Length = obj.Bounds.Height;

                    obj.ConvertedStoreObject = addedObject;
                    obj.IsSelected = true;

                    // 오버레이 업데이트
                    if (obj.OverlayShape != null)
                    {
                        obj.OverlayShape.Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0));
                        obj.OverlayShape.Stroke = Brushes.Green;
                    }
                }
            }

            RedrawAll();
        }

        private void UpdateMousePointerVisibility()
        {
            if (_viewModel?.CurrentTool == "WallStraight" || _viewModel?.CurrentTool == "PlaceObject")
            {
                MousePointer.Visibility = Visibility.Visible;
                CrosshairH.Visibility = CrosshairV.Visibility = Visibility.Collapsed;
            }
            else
            {
                MousePointer.Visibility = Visibility.Collapsed;
                StartPointIndicator.Visibility = Visibility.Collapsed;
                CrosshairH.Visibility = CrosshairV.Visibility = Visibility.Visible;
            }
        }

        private void DrawingCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            DrawGrid();
            Focus();
            UpdateMousePointerVisibility();
        }

        // 도면에서 객체 감지하는 메서드
        public void DetectObjectsInFloorPlan()
        {
            if (_loadedFloorPlan == null || _backgroundImage == null) return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // 기존 감지된 객체 제거
                ClearDetectedObjects();

                // 도면 분석
                var analyzer = new FloorPlanAnalyzer();
                var bounds = analyzer.FindFloorPlanBounds(_loadedFloorPlan);

                if (bounds != null)
                {
                    // 이미지 좌표를 캔버스 좌표로 변환하기 위한 비율 계산
                    double imageLeft = Canvas.GetLeft(_backgroundImage);
                    double imageTop = Canvas.GetTop(_backgroundImage);
                    double imageWidth = _backgroundImage.Width;
                    double imageHeight = _backgroundImage.Height;

                    double scaleX = imageWidth / _loadedFloorPlan.PixelWidth;
                    double scaleY = imageHeight / _loadedFloorPlan.PixelHeight;

                    // 객체 감지
                    var detectedObjects = analyzer.DetectFloorPlanObjects(_loadedFloorPlan, bounds);

                    foreach (var obj in detectedObjects)
                    {
                        // 이미지 좌표를 캔버스 좌표로 변환
                        var canvasRect = new Rect(
                            imageLeft + obj.Bounds.Left * scaleX,
                            imageTop + obj.Bounds.Top * scaleY,
                            obj.Bounds.Width * scaleX,
                            obj.Bounds.Height * scaleY
                        );

                        obj.Bounds = canvasRect;
                        CreateDetectedObjectOverlay(obj);
                        _detectedObjects.Add(obj);
                    }

                    _viewModel.StatusText = $"{_detectedObjects.Count}개의 객체가 감지되었습니다.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"객체 감지 중 오류: {ex.Message}", "오류",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // 감지된 객체에 대한 오버레이 생성
        private void CreateDetectedObjectOverlay(DetectedObject obj)
        {
            // 투명한 사각형 (호버 감지용)
            var overlay = new Rectangle
            {
                Width = obj.Bounds.Width,
                Height = obj.Bounds.Height,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Transparent,
                StrokeThickness = 2,
                Tag = obj,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(overlay, obj.Bounds.Left);
            Canvas.SetTop(overlay, obj.Bounds.Top);

            // 이벤트 핸들러
            overlay.MouseEnter += DetectedObject_MouseEnter;
            overlay.MouseLeave += DetectedObject_MouseLeave;
            overlay.MouseLeftButtonDown += DetectedObject_MouseLeftButtonDown;

            obj.OverlayShape = overlay;
            _detectedObjectsCanvas.Children.Add(overlay);
        }

        // 호버 이벤트
        private void DetectedObject_MouseEnter(object sender, MouseEventArgs e)
        {
            var rect = sender as Rectangle;
            var obj = rect?.Tag as DetectedObject;

            if (obj != null && !obj.IsSelected)
            {
                _hoveredDetectedObject = obj;
                obj.IsHovered = true;

                // 파란색 하이라이트
                rect.Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 255));
                rect.Stroke = Brushes.Blue;

                // 툴팁 표시
                var tooltip = new ToolTip
                {
                    Content = $"클릭하여 객체 타입 선택\n추측: {obj.GetTypeName()}"
                };
                rect.ToolTip = tooltip;
            }
        }

        private void DetectedObject_MouseLeave(object sender, MouseEventArgs e)
        {
            var rect = sender as Rectangle;
            var obj = rect?.Tag as DetectedObject;

            if (obj != null && !obj.IsSelected)
            {
                _hoveredDetectedObject = null;
                obj.IsHovered = false;

                // 하이라이트 제거
                rect.Fill = Brushes.Transparent;
                rect.Stroke = Brushes.Transparent;
            }
        }

        // 클릭 이벤트
        private void DetectedObject_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var rect = sender as Rectangle;
            var obj = rect?.Tag as DetectedObject;

            if (obj != null)
            {
                ShowObjectTypeSelectionDialog(obj);
                e.Handled = true;
            }
        }

        private void ShowObjectTypeSelectionDialog(DetectedObject obj)
        {
            var dialog = new ObjectTypeSelectionDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                // 다이얼로그에서 설정한 속성들로 StoreObject 생성
                obj.Type = dialog.SelectedType;

                // ObjectType 변환
                ObjectType storeType = ObjectTypeSelectionDialog.ConvertToObjectType(dialog.SelectedType);

                // StoreObject 생성
                var storeObject = new StoreObject(storeType, new Point2D(obj.Bounds.Left, obj.Bounds.Top))
                {
                    Width = dialog.ObjectWidth,
                    Height = dialog.ObjectHeight,
                    Length = dialog.ObjectLength,
                    Layers = dialog.ObjectLayers,
                    IsHorizontal = dialog.IsHorizontal,
                    Temperature = dialog.Temperature,
                    CategoryCode = dialog.CategoryCode
                };

                // DrawingService에 추가
                _viewModel.DrawingService.StoreObjects.Add(storeObject);

                obj.ConvertedStoreObject = storeObject;
                obj.IsSelected = true;

                // 오버레이 업데이트
                obj.OverlayShape.Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0));
                obj.OverlayShape.Stroke = Brushes.Green;

                // 화면 업데이트
                RedrawAll();

                // 3D 뷰도 업데이트 (3D 모드인 경우)
                if (_viewModel.CurrentViewMode == ViewMode.View3D)
                {
                    var mainWindow = Window.GetWindow(this) as MainWindow;
                    mainWindow?.Viewer3DControl?.UpdateAll3DModels();
                }

                _viewModel.StatusText = $"{obj.GetTypeName()}이(가) 추가되었습니다. " +
                                       $"크기: {dialog.ObjectWidth / 12:F1}' x {dialog.ObjectLength / 12:F1}' x {dialog.ObjectHeight / 12:F1}', " +
                                       $"층수: {dialog.ObjectLayers}";
            }
        }

        // 감지된 객체 제거
        private void ClearDetectedObjects()
        {
            _detectedObjectsCanvas.Children.Clear();
            _detectedObjects.Clear();
            _hoveredDetectedObject = null;
        }


        private void DrawingCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel?.CurrentTool == "Select")
            {
                var mousePos = Mouse.GetPosition(this);
                UpdateCrosshair(mousePos);
            }
        }

        // 배경 이미지 설정
        // 배경 이미지 설정 (크기 매칭 개선)
        public void SetBackgroundImage(string imagePath)
        {
            try
            {
                if (_backgroundImage != null)
                {
                    MainCanvas.Children.Remove(_backgroundImage);
                }

                var bitmap = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                _loadedFloorPlan = bitmap; // 도면 이미지 저장

                // 이미지와 캔버스 크기 비율 계산
                double canvasWidth = MainCanvas.Width;
                double canvasHeight = MainCanvas.Height;

                // 캔버스에 맞게 이미지 크기 조정 (여백 고려)
                double margin = 100; // 여백
                double maxImageWidth = canvasWidth - (margin * 2);
                double maxImageHeight = canvasHeight - (margin * 2);

                double scaleX = maxImageWidth / bitmap.PixelWidth;
                double scaleY = maxImageHeight / bitmap.PixelHeight;
                double scale = Math.Min(scaleX, scaleY);

                double imageWidth = bitmap.PixelWidth * scale;
                double imageHeight = bitmap.PixelHeight * scale;

                // 이미지를 캔버스 중앙에 배치
                _backgroundImage = new Image
                {
                    Source = bitmap,
                    Width = imageWidth,
                    Height = imageHeight,
                    Stretch = Stretch.Uniform,
                    Opacity = 0.8
                };

                // 중앙 정렬
                double left = (canvasWidth - imageWidth) / 2;
                double top = (canvasHeight - imageHeight) / 2;

                Canvas.SetLeft(_backgroundImage, left);
                Canvas.SetTop(_backgroundImage, top);
                Canvas.SetZIndex(_backgroundImage, -1);

                MainCanvas.Children.Insert(0, _backgroundImage);

                // MainWindow에 배경 이미지 정보 전달
                if (MainWindow != null)
                {
                    var backgroundImageField = MainWindow.GetType().GetField("_backgroundImage",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    backgroundImageField?.SetValue(MainWindow, _backgroundImage);

                    // 도면 이미지도 전달
                    var floorPlanField = MainWindow.GetType().GetField("_loadedFloorPlan",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    floorPlanField?.SetValue(MainWindow, _loadedFloorPlan);
                }

                // 디버깅 정보
                System.Diagnostics.Debug.WriteLine($"배경 이미지 설정:");
                System.Diagnostics.Debug.WriteLine($"  원본 크기: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
                System.Diagnostics.Debug.WriteLine($"  스케일: {scale:F3}");
                System.Diagnostics.Debug.WriteLine($"  표시 크기: {imageWidth:F1}x{imageHeight:F1}");
                System.Diagnostics.Debug.WriteLine($"  위치: ({left:F1}, {top:F1})");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 로드 실패: {ex.Message}");
            }
        }

        public void ClearBackgroundImage()
        {
            if (_backgroundImage != null)
            {
                MainCanvas.Children.Remove(_backgroundImage);
                _backgroundImage = null;
            }
        }

        private void DrawGrid()
        {
            GridCanvas.Children.Clear();

            var width = MainCanvas.Width;
            var height = MainCanvas.Height;

            // Vertical lines
            for (double x = 0; x <= width; x += GridSize)
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = x % (GridSize * 12) == 0 ? 1 : 0.5,
                    Opacity = 0.5
                };
                GridCanvas.Children.Add(line);
            }

            // Horizontal lines
            for (double y = 0; y <= height; y += GridSize)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = y % (GridSize * 12) == 0 ? 1 : 0.5,
                    Opacity = 0.5
                };
                GridCanvas.Children.Add(line);
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();

            var position = e.GetPosition(MainCanvas);
            var snappedPosition = SnapToGrid(new Point2D(position.X, position.Y));

            switch (_viewModel?.CurrentTool)
            {
                case "WallStraight":
                    HandleWallTool(snappedPosition);
                    break;

                case "PlaceObject":
                    HandlePlaceObjectStart(snappedPosition);
                    break;

                case "Select":
                    HandleSelectTool(snappedPosition, e);
                    break;
            }

            UpdateCrosshair();
        }

        private void HandleWallTool(Point2D position)
        {
            if (!_isDrawingWall)
            {
                // Start drawing wall
                _tempStartPoint = position;
                _isDrawingWall = true;
                _viewModel.StatusText = "직선 벽 그리기: 끝점을 클릭하세요";

                StartPointIndicator.Visibility = Visibility.Visible;
                UpdateStartPointIndicatorPosition();

                // Create preview wall rectangle
                _previewWall = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.FromArgb(100, 200, 200, 255)),
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                };
                TempCanvas.Children.Add(_previewWall);
            }
            else
            {
                // End drawing wall
                var wall = _viewModel.DrawingService.AddWall(_tempStartPoint, position);
                _isDrawingWall = false;
                _viewModel.StatusText = "직선 벽 그리기: 시작점을 클릭하세요";

                // Remove preview elements
                TempCanvas.Children.Remove(_previewWall);
                _previewWall = null;
                StartPointIndicator.Visibility = Visibility.Collapsed;

                // Redraw everything
                RedrawAll();
            }
        }

        private void HandlePlaceObjectStart(Point2D position)
        {
            if (!_isDrawingObject)
            {
                _objectStartPoint = position;
                _isDrawingObject = true;

                // Create preview rectangle
                _objectPreview = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 255)),
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                };

                Canvas.SetLeft(_objectPreview, position.X);
                Canvas.SetTop(_objectPreview, position.Y);
                _objectPreview.Width = 0;
                _objectPreview.Height = 0;

                TempCanvas.Children.Add(_objectPreview);

                _viewModel.StatusText = "영역을 드래그하여 크기를 지정하세요";
            }
        }

        private void HandlePlaceObjectEnd(Point2D position)
        {
            if (_isDrawingObject && _objectPreview != null)
            {
                var width = Math.Abs(position.X - _objectStartPoint.X);
                var height = Math.Abs(position.Y - _objectStartPoint.Y);

                if (width > 10 && height > 10) // 최소 크기 체크
                {
                    // MainWindow 인스턴스를 통해 호출
                    var objectType = MainWindow?.GetCurrentObjectTool();
                    if (!string.IsNullOrEmpty(objectType))
                    {
                        ObjectType type;
                        switch (objectType)
                        {
                            case "Shelf":
                                type = ObjectType.Shelf;
                                break;
                            case "Refrigerator":
                                type = ObjectType.Refrigerator;
                                break;
                            case "Freezer":
                                type = ObjectType.Freezer;
                                break;
                            case "Checkout":
                                type = ObjectType.Checkout;
                                break;
                            case "DisplayStand":
                                type = ObjectType.DisplayStand;
                                break;
                            default:
                                return;
                        }

                        // 객체 생성
                        var topLeft = new Point2D(
                            Math.Min(_objectStartPoint.X, position.X),
                            Math.Min(_objectStartPoint.Y, position.Y)
                        );

                        var obj = _viewModel.DrawingService.AddStoreObject(type, topLeft);
                        obj.Width = width;
                        obj.Length = height;

                        // MainWindow 인스턴스 메서드 호출
                        MainWindow?.OnObjectPlaced(obj);

                        RedrawAll();
                    }
                }

                // Clean up
                TempCanvas.Children.Remove(_objectPreview);
                _objectPreview = null;
                _isDrawingObject = false;
            }
        }

        private void HandleSelectTool(Point2D position, MouseButtonEventArgs e)
        {
            // 객체 선택 확인
            var obj = _viewModel.DrawingService.GetObjectAt(position);

            if (obj != null)
            {
                SelectObject(obj);

                // 드래그 시작 준비
                _isDraggingObject = true;
                _dragOffset = new Point2D(
                    position.X - obj.Position.X,
                    position.Y - obj.Position.Y
                );
                MainCanvas.CaptureMouse();
            }
            else
            {
                // 빈 공간 클릭 - 선택 해제
                SelectObject(null);

                // 패닝 시작
                _isPanning = true;
                _lastPanPoint = e.GetPosition(CanvasScrollViewer);
                MainCanvas.CaptureMouse();
            }
        }

        private void SelectObject(StoreObject obj)
        {
            // 이전 선택 해제
            if (_selectedObject != null)
            {
                _selectedObject.IsSelected = false;
            }

            _selectedObject = obj;

            if (obj != null)
            {
                obj.IsSelected = true;
                _viewModel.StatusText = $"{obj.GetDisplayName()} 선택됨";
            }

            // MainWindow의 속성 패널 업데이트
            MainWindow?.SelectObject(obj);

            RedrawAll();
        }

        private void CancelWallDrawing()
        {
            if (_isDrawingWall)
            {
                _isDrawingWall = false;
                if (_previewWall != null)
                {
                    TempCanvas.Children.Remove(_previewWall);
                    _previewWall = null;
                }
                StartPointIndicator.Visibility = Visibility.Collapsed;

                if (_viewModel != null)
                {
                    _viewModel.StatusText = _viewModel.CurrentTool == "WallStraight" ?
                        "직선 벽 그리기: 시작점을 클릭하세요" : "도구를 선택하세요";
                }
            }
        }

        private void CancelObjectDrawing()
        {
            if (_isDrawingObject && _objectPreview != null)
            {
                TempCanvas.Children.Remove(_objectPreview);
                _objectPreview = null;
                _isDrawingObject = false;
            }
        }

        private Point2D SnapToGrid(Point2D point)
        {
            var snappedX = Math.Round(point.X / GridSize) * GridSize;
            var snappedY = Math.Round(point.Y / GridSize) * GridSize;
            return new Point2D(snappedX, snappedY);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var mousePos = e.GetPosition(MainCanvas);
            var snappedPosition = SnapToGrid(new Point2D(mousePos.X, mousePos.Y));

            // 화면상의 마우스 위치
            var screenPosition = e.GetPosition(this);

            // Update mouse pointer position
            if (_viewModel?.CurrentTool == "WallStraight" || _viewModel?.CurrentTool == "PlaceObject")
            {
                Canvas.SetLeft(MousePointer, screenPosition.X - 4);
                Canvas.SetTop(MousePointer, screenPosition.Y - 4);
                MousePointer.Visibility = Visibility.Visible;
            }
            else
            {
                MousePointer.Visibility = Visibility.Collapsed;
            }

            // Update preview wall if drawing
            if (_isDrawingWall && _previewWall != null)
            {
                UpdatePreviewWall(_tempStartPoint, snappedPosition);
            }

            // Update object preview if drawing
            if (_isDrawingObject && _objectPreview != null)
            {
                UpdateObjectPreview(snappedPosition);
            }

            // Handle object dragging
            if (_isDraggingObject && _selectedObject != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var newPosition = new Point2D(
                    snappedPosition.X - _dragOffset.X,
                    snappedPosition.Y - _dragOffset.Y
                );
                _selectedObject.MoveTo(newPosition);
                RedrawAll();
            }

            // Handle canvas panning
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(CanvasScrollViewer);
                var deltaX = currentPoint.X - _lastPanPoint.X;
                var deltaY = currentPoint.Y - _lastPanPoint.Y;

                CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - deltaX);
                CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset - deltaY);

                _lastPanPoint = currentPoint;
            }

            // Update hover highlight for objects
            if (_viewModel?.CurrentTool == "Select" && !_isDraggingObject)
            {
                UpdateObjectHover(new Point2D(mousePos.X, mousePos.Y));
            }

            // Update crosshair
            UpdateCrosshair(screenPosition);
        }

        private void UpdateObjectPreview(Point2D currentPos)
        {
            if (_objectPreview != null)
            {
                var width = Math.Abs(currentPos.X - _objectStartPoint.X);
                var height = Math.Abs(currentPos.Y - _objectStartPoint.Y);
                var left = Math.Min(_objectStartPoint.X, currentPos.X);
                var top = Math.Min(_objectStartPoint.Y, currentPos.Y);

                Canvas.SetLeft(_objectPreview, left);
                Canvas.SetTop(_objectPreview, top);
                _objectPreview.Width = width;
                _objectPreview.Height = height;
            }
        }

        private void UpdateObjectHover(Point2D position)
        {
            var obj = _viewModel.DrawingService.GetObjectAt(position);

            if (obj != _hoveredObject)
            {
                // Remove previous highlight
                if (_hoverHighlight != null)
                {
                    TempCanvas.Children.Remove(_hoverHighlight);
                    _hoverHighlight = null;
                }

                _hoveredObject = obj;

                // Add new highlight
                if (_hoveredObject != null && _hoveredObject != _selectedObject)
                {
                    var (min, max) = _hoveredObject.GetBoundingBox();
                    _hoverHighlight = new Rectangle
                    {
                        Width = max.X - min.X + 6,
                        Height = max.Y - min.Y + 6,
                        Fill = Brushes.Transparent,
                        Stroke = Brushes.Blue,
                        StrokeThickness = 2,
                        Opacity = 0.5
                    };

                    Canvas.SetLeft(_hoverHighlight, min.X - 3);
                    Canvas.SetTop(_hoverHighlight, min.Y - 3);
                    TempCanvas.Children.Add(_hoverHighlight);
                }
            }
        }

        private void UpdateCrosshair(Point? screenPosition = null)
        {
            if (_viewModel?.CurrentTool == "Select")
            {
                var mousePos = screenPosition ?? Mouse.GetPosition(this);

                CrosshairH.X1 = 0;
                CrosshairH.X2 = ActualWidth;
                CrosshairH.Y1 = CrosshairH.Y2 = mousePos.Y;

                CrosshairV.Y1 = 0;
                CrosshairV.Y2 = ActualHeight;
                CrosshairV.X1 = CrosshairV.X2 = mousePos.X;

                CrosshairH.Visibility = CrosshairV.Visibility = Visibility.Visible;
            }
            else
            {
                CrosshairH.Visibility = CrosshairV.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateStartPointIndicatorPosition()
        {
            if (_isDrawingWall && StartPointIndicator.Visibility == Visibility.Visible)
            {
                var canvasPoint = new Point(_tempStartPoint.X, _tempStartPoint.Y);
                var transformedPoint = MainCanvas.TransformToAncestor(this).Transform(canvasPoint);

                Canvas.SetLeft(StartPointIndicator, transformedPoint.X - 5);
                Canvas.SetTop(StartPointIndicator, transformedPoint.Y - 5);
            }
        }

        private void UpdatePreviewWall(Point2D startPoint, Point2D endPoint)
        {
            if (_previewWall == null) return;

            var thickness = 10.0;

            var minX = Math.Min(startPoint.X, endPoint.X) - thickness / 2;
            var minY = Math.Min(startPoint.Y, endPoint.Y) - thickness / 2;
            var width = Math.Abs(endPoint.X - startPoint.X) + thickness;
            var height = Math.Abs(endPoint.Y - startPoint.Y) + thickness;

            Canvas.SetLeft(_previewWall, minX);
            Canvas.SetTop(_previewWall, minY);
            _previewWall.Width = width;
            _previewWall.Height = height;
            _previewWall.RenderTransform = null;
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            CancelWallDrawing();
            CancelObjectDrawing();
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Focus();

            var mousePositionBefore = e.GetPosition(MainCanvas);

            var delta = e.Delta > 0 ? 1.1 : 0.9;
            var oldZoomFactor = _zoomFactor;
            _zoomFactor *= delta;

            _zoomFactor = Math.Max(0.1, Math.Min(_zoomFactor, 10.0));

            var scaleTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
            MainCanvas.RenderTransform = scaleTransform;

            var mousePositionAfter = e.GetPosition(MainCanvas);
            var offset = new Point(
                (mousePositionAfter.X - mousePositionBefore.X) * _zoomFactor,
                (mousePositionAfter.Y - mousePositionBefore.Y) * _zoomFactor
            );

            CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - offset.X);
            CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset - offset.Y);

            MainCanvas.Width = 2000 * _zoomFactor;
            MainCanvas.Height = 2000 * _zoomFactor;

            UpdateStartPointIndicatorPosition();

            var screenPos = e.GetPosition(this);
            if (_viewModel?.CurrentTool == "Select")
            {
                UpdateCrosshair(screenPos);
            }

            e.Handled = true;
        }

        private void Canvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelWallDrawing();
                CancelObjectDrawing();

                e.Handled = true;
            }
            else if (e.Key == Key.Delete && _selectedObject != null)
            {
                _viewModel.DrawingService.RemoveStoreObject(_selectedObject);
                SelectObject(null);
                RedrawAll();
                e.Handled = true;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelWallDrawing();
                CancelObjectDrawing();

                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                MainCanvas.ReleaseMouseCapture();
            }

            if (_isDraggingObject)
            {
                _isDraggingObject = false;
                MainCanvas.ReleaseMouseCapture();
            }

            if (_isDrawingObject)
            {
                var position = e.GetPosition(MainCanvas);
                var snappedPosition = SnapToGrid(new Point2D(position.X, position.Y));
                HandlePlaceObjectEnd(snappedPosition);
            }

            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            CrosshairH.Visibility = CrosshairV.Visibility = Visibility.Collapsed;
            MousePointer.Visibility = Visibility.Collapsed;

            // Remove hover highlight
            if (_hoverHighlight != null)
            {
                TempCanvas.Children.Remove(_hoverHighlight);
                _hoverHighlight = null;
                _hoveredObject = null;
            }

            base.OnMouseLeave(e);
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (_viewModel?.CurrentTool == "Select")
            {
                var mousePos = e.GetPosition(this);
                UpdateCrosshair(mousePos);
            }
            base.OnMouseEnter(e);
        }

        public void RedrawAll()
        {
            if (_viewModel?.DrawingService == null) return;

            WallCanvas.Children.Clear();
            LabelCanvas.Children.Clear();
            RoomCanvas.Children.Clear();

            DrawRooms();
            DrawWalls();
            DrawStoreObjects();
        }

        private void DrawRooms()
        {
            foreach (var room in _viewModel.DrawingService.Rooms)
            {
                if (!room.IsClosedRoom()) continue;

                var points = GetRoomPoints(room);
                if (points.Count < 3) continue;

                var polygon = new Polygon
                {
                    // 옅은 회색에 투명도 적용 (도면이 잘 보이도록)
                    Fill = new SolidColorBrush(Color.FromArgb(50, 200, 200, 200)), // 알파값 50으로 투명도 설정
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 0
                };

                foreach (var point in points)
                {
                    polygon.Points.Add(new Point(point.X, point.Y));
                }

                RoomCanvas.Children.Add(polygon);
            }
        }

        private void DrawStoreObjects()
        {
            foreach (var obj in _viewModel.DrawingService.StoreObjects)
            {
                DrawStoreObject(obj);
            }
        }

        private void DrawStoreObject(StoreObject obj)
        {
            double actualWidth = obj.IsHorizontal ? obj.Width : obj.Length;
            double actualLength = obj.IsHorizontal ? obj.Length : obj.Width;

            var rect = new Rectangle
            {
                Width = actualWidth,
                Height = actualLength,
                Fill = obj.Fill,
                Stroke = obj.IsSelected ? Brushes.Red : obj.Stroke,
                StrokeThickness = obj.IsSelected ? 3 : 1
            };

            Canvas.SetLeft(rect, obj.Position.X);
            Canvas.SetTop(rect, obj.Position.Y);
            WallCanvas.Children.Add(rect);

            // 객체 이름 표시
            var label = new TextBlock
            {
                Text = obj.GetDisplayName(),
                FontSize = 10,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(2)
            };

            Canvas.SetLeft(label, obj.Position.X + actualWidth / 2 - 20);
            Canvas.SetTop(label, obj.Position.Y + actualLength / 2 - 8);
            LabelCanvas.Children.Add(label);

            // 층수 표시 (2층 이상인 경우)
            if (obj.Layers > 1)
            {
                var layersText = new TextBlock
                {
                    Text = $"{obj.Layers}층",
                    FontSize = 9,
                    Foreground = Brushes.White,
                    Background = Brushes.Black,
                    Padding = new Thickness(2)
                };

                Canvas.SetLeft(layersText, obj.Position.X + 2);
                Canvas.SetTop(layersText, obj.Position.Y + 2);
                LabelCanvas.Children.Add(layersText);
            }
        }

        private List<Point2D> GetRoomPoints(Room room)
        {
            var points = new List<Point2D>();
            if (room.Walls.Count == 0) return points;

            var current = room.Walls.First().StartPoint;
            points.Add(current);
            var usedWalls = new HashSet<Wall>();

            while (usedWalls.Count < room.Walls.Count)
            {
                Wall nextWall = null;
                Point2D nextPoint = null;

                foreach (var wall in room.Walls.Where(w => !usedWalls.Contains(w)))
                {
                    if (Math.Abs(wall.StartPoint.X - current.X) < 1 && Math.Abs(wall.StartPoint.Y - current.Y) < 1)
                    {
                        nextWall = wall;
                        nextPoint = wall.EndPoint;
                        break;
                    }
                    else if (Math.Abs(wall.EndPoint.X - current.X) < 1 && Math.Abs(wall.EndPoint.Y - current.Y) < 1)
                    {
                        nextWall = wall;
                        nextPoint = wall.StartPoint;
                        break;
                    }
                }

                if (nextWall == null) break;

                usedWalls.Add(nextWall);
                if (Math.Abs(nextPoint.X - points.First().X) > 1 || Math.Abs(nextPoint.Y - points.First().Y) > 1)
                {
                    points.Add(nextPoint);
                    current = nextPoint;
                }
                else
                {
                    break;
                }
            }

            return points;
        }

        private Point2D GetPolygonCenter(List<Point2D> points)
        {
            double centerX = points.Average(p => p.X);
            double centerY = points.Average(p => p.Y);
            return new Point2D(centerX, centerY);
        }

        private void DrawWalls()
        {
            foreach (var wall in _viewModel.DrawingService.Walls)
            {
                DrawWall(wall);
            }
        }


        // DrawingCanvas.xaml.cs의 DrawWall 메서드에서 벽 길이 표시 부분만 수정

        private void DrawWall(Wall wall)
        {
            var startPoint = wall.StartPoint;
            var endPoint = wall.EndPoint;
            var thickness = wall.Thickness;

            var dx = endPoint.X - startPoint.X;
            var dy = endPoint.Y - startPoint.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0) return;

            var unitX = dx / length;
            var unitY = dy / length;
            var perpX = -unitY * thickness / 2;
            var perpY = unitX * thickness / 2;

            var wallPolygon = new Polygon
            {
                Fill = wall.Fill,
                Stroke = wall.Stroke,
                StrokeThickness = 1
            };

            wallPolygon.Points.Add(new Point(startPoint.X + perpX, startPoint.Y + perpY));
            wallPolygon.Points.Add(new Point(endPoint.X + perpX, endPoint.Y + perpY));
            wallPolygon.Points.Add(new Point(endPoint.X - perpX, endPoint.Y - perpY));
            wallPolygon.Points.Add(new Point(startPoint.X - perpX, startPoint.Y - perpY));

            WallCanvas.Children.Add(wallPolygon);

            // 벽 길이 표시
            var midPoint = wall.MidPoint;

            // 실제 길이 표시
            string lengthText;
            if (wall.RealLengthInInches.HasValue)
            {
                // 저장된 실제 길이 사용
                var realLength = wall.RealLengthInInches.Value;
                var feet = (int)(realLength / 12.0);
                var inches = (int)(realLength % 12.0);
                lengthText = $"{feet}'-{inches}\"";
            }
            else
            {
                // 스케일 기반 계산 (폴백)
                lengthText = wall.LengthDisplay;
            }

            var lengthLabel = new TextBlock
            {
                Text = lengthText,
                FontSize = 12,
                Background = Brushes.White,
                Padding = new Thickness(2)
            };

            // 벽의 방향 확인
            bool isHorizontal = Math.Abs(dx) > Math.Abs(dy);

            // 텍스트 크기 측정을 위한 FormattedText 생성
            var formattedText = new FormattedText(
                lengthText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Brushes.Black);

            double textWidth = formattedText.Width;
            double textHeight = formattedText.Height;

            // 텍스트 위치 조정 - 벽의 외부에 배치
            double offsetDistance = 20; // 벽으로부터의 거리
            double labelX, labelY;

            if (isHorizontal)
            {
                // 가로 벽 - 위쪽에 배치 (중앙 정렬)
                labelX = midPoint.X - textWidth / 2;
                labelY = midPoint.Y - offsetDistance - textHeight / 2;
            }
            else
            {
                // 세로 벽 - 왼쪽에 배치 (중앙 정렬)
                labelX = midPoint.X - offsetDistance - textWidth;
                labelY = midPoint.Y - textHeight / 2;
            }

            // 캔버스 경계 확인 및 조정
            if (labelX < 5) labelX = 5;
            if (labelY < 5) labelY = 5;
            if (labelX + textWidth > MainCanvas.Width - 5)
                labelX = MainCanvas.Width - textWidth - 5;
            if (labelY + textHeight > MainCanvas.Height - 5)
                labelY = MainCanvas.Height - textHeight - 5;

            Canvas.SetLeft(lengthLabel, labelX);
            Canvas.SetTop(lengthLabel, labelY);
            LabelCanvas.Children.Add(lengthLabel);

            // 벽의 각도에 따라 텍스트 회전 (가로 벽만)
            if (isHorizontal)
            {
                var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                if (Math.Abs(angle) > 90)
                    angle += 180;

                // 텍스트의 중심점을 기준으로 회전
                lengthLabel.RenderTransformOrigin = new Point(0.5, 0.5);
                lengthLabel.RenderTransform = new RotateTransform(angle);
            }
            // 세로 벽은 회전 없이 표시
        }
    }
}