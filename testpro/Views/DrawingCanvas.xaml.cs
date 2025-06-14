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
        private MainViewModel _viewModel;
        private Point2D _tempStartPoint;
        private bool _isDrawingWall = false;
        private Rectangle _previewWall;
        private const double GridSize = 12.0;
        private double _zoomFactor = 1.0;
        private Point _lastPanPoint;
        private bool _isPanning = false;

        private Image _backgroundImage;
        private BitmapImage _loadedFloorPlan;

        private bool _isDrawingObject = false;
        private Point2D _objectStartPoint;
        private Rectangle _objectPreview;
        private StoreObject _selectedObject;
        private StoreObject _hoveredObject;
        private Rectangle _hoverHighlight;
        private bool _isDraggingObject = false;
        private Point2D _dragOffset;

        private List<DetectedObject> _detectedObjects = new List<DetectedObject>();
        private Canvas _detectedObjectsCanvas;
        private DetectedObject _hoveredDetectedObject;

        private StoreObject _copiedObject = null;

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

            _detectedObjectsCanvas = new Canvas { IsHitTestVisible = true };
            MainCanvas.Children.Add(_detectedObjectsCanvas);
            Canvas.SetZIndex(_detectedObjectsCanvas, 10);

            Loaded += DrawingCanvas_Loaded;
            SizeChanged += DrawingCanvas_SizeChanged;

            Focusable = true;
            MouseEnter += (s, e) => Focus();
        }

        // *** --- 복사/붙여넣기 공개 메서드 (붙여넣기 메서드 수정) --- ***
        public void CopySelectedObject()
        {
            if (_selectedObject != null)
            {
                _copiedObject = _selectedObject;
                if (_viewModel != null)
                {
                    _viewModel.StatusText = $"{_copiedObject.GetDisplayName()} 복사됨.";
                }
            }
        }

        public void PasteCopiedObject(Point pastePosition)
        {
            if (_copiedObject == null) return;

            // 객체의 중심이 마우스 커서 위치에 오도록 좌상단 좌표 계산
            double actualWidth = _copiedObject.IsHorizontal ? _copiedObject.Width : _copiedObject.Length;
            double actualLength = _copiedObject.IsHorizontal ? _copiedObject.Length : _copiedObject.Width;

            double newX = pastePosition.X - (actualWidth / 2.0);
            double newY = pastePosition.Y - (actualLength / 2.0);

            // 계산된 좌상단 좌표를 그리드에 맞춤
            Point2D snappedPosition = SnapToGrid(new Point2D(newX, newY));

            // 새 객체 생성 및 속성 복사
            var newObj = _viewModel.DrawingService.AddStoreObject(_copiedObject.Type, snappedPosition);
            newObj.Width = _copiedObject.Width;
            newObj.Length = _copiedObject.Length;
            newObj.Height = _copiedObject.Height;
            newObj.Layers = _copiedObject.Layers;
            newObj.IsHorizontal = _copiedObject.IsHorizontal;
            newObj.Temperature = _copiedObject.Temperature;
            newObj.CategoryCode = _copiedObject.CategoryCode;

            SelectObject(newObj);
            RedrawAll();
            _viewModel.StatusText = $"{newObj.GetDisplayName()} 붙여넣기 완료.";
        }

        // ... (이하 다른 메서드들은 이전과 동일하게 유지) ...

        // *** 수정된 키보드 이벤트 핸들러 (복사/붙여넣기 로직 제거) ***
        private void Canvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelWallDrawing();
                CancelObjectDrawing();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Delete && _selectedObject != null)
            {
                _viewModel.DrawingService.RemoveStoreObject(_selectedObject);
                SelectObject(null);
                RedrawAll();
                e.Handled = true;
                return;
            }
        }

        // ... (나머지 코드는 이전 답변과 동일하게 유지) ...
        #region Other Methods
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.CurrentTool))
            {
                UpdateMousePointerVisibility();
                if (_isDrawingWall && _viewModel.CurrentTool != "WallStraight") CancelWallDrawing();
                if (_isDrawingObject && _viewModel.CurrentTool != "PlaceObject") CancelObjectDrawing();
            }
        }

        public int GetDetectedObjectsCount() => _detectedObjects.Count;
        public bool IsDetectedObjectConverted(DetectedObject obj) => obj.ConvertedStoreObject != null;

        public void ConvertAllDetectedObjects()
        {
            foreach (var obj in _detectedObjects.Where(o => !IsDetectedObjectConverted(o) && o.Type != DetectedObjectType.Unknown))
            {
                var storeObject = obj.ToStoreObject();
                _viewModel.DrawingService.AddStoreObject(storeObject.Type, new Point2D(obj.Bounds.Left, obj.Bounds.Top));
                var addedObject = _viewModel.DrawingService.StoreObjects.Last();
                addedObject.Width = obj.Bounds.Width;
                addedObject.Length = obj.Bounds.Height;
                obj.ConvertedStoreObject = addedObject;
                obj.IsSelected = true;
                if (obj.OverlayShape != null)
                {
                    obj.OverlayShape.Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0));
                    obj.OverlayShape.Stroke = Brushes.Green;
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

        public void DetectObjectsInFloorPlan()
        {
            if (_loadedFloorPlan == null || _backgroundImage == null) return;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                ClearDetectedObjects();
                var analyzer = new FloorPlanAnalyzer();
                var bounds = analyzer.FindFloorPlanBounds(_loadedFloorPlan);
                if (bounds != null)
                {
                    double imageLeft = Canvas.GetLeft(_backgroundImage), imageTop = Canvas.GetTop(_backgroundImage);
                    double scaleX = _backgroundImage.Width / _loadedFloorPlan.PixelWidth, scaleY = _backgroundImage.Height / _loadedFloorPlan.PixelHeight;
                    var detectedObjects = analyzer.DetectFloorPlanObjects(_loadedFloorPlan, bounds);
                    foreach (var obj in detectedObjects)
                    {
                        obj.Bounds = new Rect(imageLeft + obj.Bounds.Left * scaleX, imageTop + obj.Bounds.Top * scaleY, obj.Bounds.Width * scaleX, obj.Bounds.Height * scaleY);
                        CreateDetectedObjectOverlay(obj);
                        _detectedObjects.Add(obj);
                    }
                    _viewModel.StatusText = $"{_detectedObjects.Count}개의 객체가 감지되었습니다.";
                }
            }
            catch (Exception ex) { MessageBox.Show($"객체 감지 중 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { Mouse.OverrideCursor = null; }
        }

        private void CreateDetectedObjectOverlay(DetectedObject obj)
        {
            var overlay = new Rectangle { Width = obj.Bounds.Width, Height = obj.Bounds.Height, Fill = Brushes.Transparent, Stroke = Brushes.Transparent, StrokeThickness = 2, Tag = obj, Cursor = Cursors.Hand };
            Canvas.SetLeft(overlay, obj.Bounds.Left);
            Canvas.SetTop(overlay, obj.Bounds.Top);
            overlay.MouseEnter += DetectedObject_MouseEnter;
            overlay.MouseLeave += DetectedObject_MouseLeave;
            overlay.MouseLeftButtonDown += DetectedObject_MouseLeftButtonDown;
            obj.OverlayShape = overlay;
            _detectedObjectsCanvas.Children.Add(overlay);
        }

        private void DetectedObject_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is DetectedObject obj && !obj.IsSelected)
            {
                _hoveredDetectedObject = obj;
                obj.IsHovered = true;
                rect.Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 255));
                rect.Stroke = Brushes.Blue;
                rect.ToolTip = new ToolTip { Content = $"클릭하여 객체 타입 선택\n추측: {obj.GetTypeName()}" };
            }
        }

        private void DetectedObject_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is DetectedObject obj && !obj.IsSelected)
            {
                _hoveredDetectedObject = null;
                obj.IsHovered = false;
                rect.Fill = Brushes.Transparent;
                rect.Stroke = Brushes.Transparent;
            }
        }

        private void DetectedObject_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is DetectedObject obj)
            {
                ShowObjectTypeSelectionDialog(obj);
                e.Handled = true;
            }
        }

        private void ShowObjectTypeSelectionDialog(DetectedObject obj)
        {
            var dialog = new ObjectTypeSelectionDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                obj.Type = dialog.SelectedType;
                var storeType = ObjectTypeSelectionDialog.ConvertToObjectType(dialog.SelectedType);
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
                _viewModel.DrawingService.StoreObjects.Add(storeObject);
                obj.ConvertedStoreObject = storeObject;
                obj.IsSelected = true;
                obj.OverlayShape.Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0));
                obj.OverlayShape.Stroke = Brushes.Green;
                RedrawAll();
                if (_viewModel.CurrentViewMode == ViewMode.View3D) (Window.GetWindow(this) as MainWindow)?.Viewer3DControl?.UpdateAll3DModels();
                _viewModel.StatusText = $"{obj.GetTypeName()}이(가) 추가되었습니다.";
            }
        }

        private void ClearDetectedObjects()
        {
            _detectedObjectsCanvas.Children.Clear();
            _detectedObjects.Clear();
            _hoveredDetectedObject = null;
        }

        private void DrawingCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel?.CurrentTool == "Select") UpdateCrosshair(Mouse.GetPosition(this));
        }

        public void SetBackgroundImage(string imagePath)
        {
            try
            {
                if (_backgroundImage != null) MainCanvas.Children.Remove(_backgroundImage);
                var bitmap = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                _loadedFloorPlan = bitmap;
                double margin = 100, maxImageWidth = MainCanvas.Width - (margin * 2), maxImageHeight = MainCanvas.Height - (margin * 2);
                double scale = Math.Min(maxImageWidth / bitmap.PixelWidth, maxImageHeight / bitmap.PixelHeight);
                _backgroundImage = new Image { Source = bitmap, Width = bitmap.PixelWidth * scale, Height = bitmap.PixelHeight * scale, Stretch = Stretch.Uniform, Opacity = 0.8 };
                Canvas.SetLeft(_backgroundImage, (MainCanvas.Width - _backgroundImage.Width) / 2);
                Canvas.SetTop(_backgroundImage, (MainCanvas.Height - _backgroundImage.Height) / 2);
                Canvas.SetZIndex(_backgroundImage, -1);
                MainCanvas.Children.Insert(0, _backgroundImage);
                if (MainWindow != null)
                {
                    MainWindow.GetType().GetField("_backgroundImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(MainWindow, _backgroundImage);
                    MainWindow.GetType().GetField("_loadedFloorPlan", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(MainWindow, _loadedFloorPlan);
                }
            }
            catch (Exception ex) { MessageBox.Show($"이미지 로드 실패: {ex.Message}"); }
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
            for (double x = 0; x <= width; x += GridSize)
                GridCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, Stroke = Brushes.LightGray, StrokeThickness = x % (GridSize * 12) == 0 ? 1 : 0.5, Opacity = 0.5 });
            for (double y = 0; y <= height; y += GridSize)
                GridCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = Brushes.LightGray, StrokeThickness = y % (GridSize * 12) == 0 ? 1 : 0.5, Opacity = 0.5 });
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            var snappedPosition = SnapToGrid(new Point2D(e.GetPosition(MainCanvas).X, e.GetPosition(MainCanvas).Y));
            switch (_viewModel?.CurrentTool)
            {
                case "WallStraight": HandleWallTool(snappedPosition); break;
                case "PlaceObject": HandlePlaceObjectStart(snappedPosition); break;
                case "Select": HandleSelectTool(snappedPosition, e); break;
            }
            UpdateCrosshair();
        }

        private void HandleWallTool(Point2D position)
        {
            if (!_isDrawingWall)
            {
                _tempStartPoint = position;
                _isDrawingWall = true;
                _viewModel.StatusText = "직선 벽 그리기: 끝점을 클릭하세요";
                StartPointIndicator.Visibility = Visibility.Visible;
                UpdateStartPointIndicatorPosition();
                _previewWall = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(100, 200, 200, 255)), Stroke = Brushes.Blue, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 5, 5 } };
                TempCanvas.Children.Add(_previewWall);
            }
            else
            {
                _viewModel.DrawingService.AddWall(_tempStartPoint, position);
                _isDrawingWall = false;
                _viewModel.StatusText = "직선 벽 그리기: 시작점을 클릭하세요";
                TempCanvas.Children.Remove(_previewWall);
                _previewWall = null;
                StartPointIndicator.Visibility = Visibility.Collapsed;
                RedrawAll();
            }
        }

        private void HandlePlaceObjectStart(Point2D position)
        {
            if (!_isDrawingObject)
            {
                _objectStartPoint = position;
                _isDrawingObject = true;
                _objectPreview = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 255)), Stroke = Brushes.Blue, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 5, 5 } };
                Canvas.SetLeft(_objectPreview, position.X);
                Canvas.SetTop(_objectPreview, position.Y);
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
                if (width > 10 && height > 10)
                {
                    var objectTypeStr = MainWindow?.GetCurrentObjectTool();
                    if (!string.IsNullOrEmpty(objectTypeStr) && Enum.TryParse(objectTypeStr, out ObjectType type))
                    {
                        var topLeft = new Point2D(Math.Min(_objectStartPoint.X, position.X), Math.Min(_objectStartPoint.Y, position.Y));
                        var obj = _viewModel.DrawingService.AddStoreObject(type, topLeft);
                        obj.Width = width;
                        obj.Length = height;
                        MainWindow?.OnObjectPlaced(obj);
                        RedrawAll();
                    }
                }
                TempCanvas.Children.Remove(_objectPreview);
                _objectPreview = null;
                _isDrawingObject = false;
            }
        }

        private void HandleSelectTool(Point2D position, MouseButtonEventArgs e)
        {
            var obj = _viewModel.DrawingService.GetObjectAt(position);
            if (obj != null)
            {
                SelectObject(obj);
                _isDraggingObject = true;
                _dragOffset = new Point2D(position.X - obj.Position.X, position.Y - obj.Position.Y);
                MainCanvas.CaptureMouse();
            }
            else
            {
                SelectObject(null);
                _isPanning = true;
                _lastPanPoint = e.GetPosition(CanvasScrollViewer);
                MainCanvas.CaptureMouse();
            }
        }

        private void SelectObject(StoreObject obj)
        {
            if (_selectedObject != null) _selectedObject.IsSelected = false;
            _selectedObject = obj;
            if (obj != null)
            {
                obj.IsSelected = true;
                _viewModel.StatusText = $"{obj.GetDisplayName()} 선택됨";
            }
            MainWindow?.SelectObject(obj);
            RedrawAll();
        }

        private void CancelWallDrawing()
        {
            if (_isDrawingWall)
            {
                _isDrawingWall = false;
                if (_previewWall != null) { TempCanvas.Children.Remove(_previewWall); _previewWall = null; }
                StartPointIndicator.Visibility = Visibility.Collapsed;
                if (_viewModel != null) _viewModel.StatusText = _viewModel.CurrentTool == "WallStraight" ? "직선 벽 그리기: 시작점을 클릭하세요" : "도구를 선택하세요";
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
            return new Point2D(Math.Round(point.X / GridSize) * GridSize, Math.Round(point.Y / GridSize) * GridSize);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var mousePos = e.GetPosition(MainCanvas);
            var snappedPosition = SnapToGrid(new Point2D(mousePos.X, mousePos.Y));
            var screenPosition = e.GetPosition(this);

            if (_viewModel?.CurrentTool == "WallStraight" || _viewModel?.CurrentTool == "PlaceObject")
            {
                Canvas.SetLeft(MousePointer, screenPosition.X - 4);
                Canvas.SetTop(MousePointer, screenPosition.Y - 4);
                MousePointer.Visibility = Visibility.Visible;
            }
            else MousePointer.Visibility = Visibility.Collapsed;

            if (_isDrawingWall && _previewWall != null) UpdatePreviewWall(_tempStartPoint, snappedPosition);
            if (_isDrawingObject && _objectPreview != null) UpdateObjectPreview(snappedPosition);

            if (_isDraggingObject && _selectedObject != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var newPosition = new Point2D(snappedPosition.X - _dragOffset.X, snappedPosition.Y - _dragOffset.Y);
                _selectedObject.MoveTo(newPosition);
                RedrawAll();
            }

            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(CanvasScrollViewer);
                var delta = new Point(currentPoint.X - _lastPanPoint.X, currentPoint.Y - _lastPanPoint.Y);
                CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - delta.X);
                CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset - delta.Y);
                _lastPanPoint = currentPoint;
            }

            if (_viewModel?.CurrentTool == "Select" && !_isDraggingObject) UpdateObjectHover(new Point2D(mousePos.X, mousePos.Y));
            UpdateCrosshair(screenPosition);
        }

        private void UpdateObjectPreview(Point2D currentPos)
        {
            if (_objectPreview != null)
            {
                var rect = new Rect(_objectStartPoint.ToPoint(), currentPos.ToPoint());
                Canvas.SetLeft(_objectPreview, rect.Left);
                Canvas.SetTop(_objectPreview, rect.Top);
                _objectPreview.Width = rect.Width;
                _objectPreview.Height = rect.Height;
            }
        }

        private void UpdateObjectHover(Point2D position)
        {
            var obj = _viewModel.DrawingService.GetObjectAt(position);
            if (obj != _hoveredObject)
            {
                if (_hoverHighlight != null) { TempCanvas.Children.Remove(_hoverHighlight); _hoverHighlight = null; }
                _hoveredObject = obj;
                if (_hoveredObject != null && _hoveredObject != _selectedObject)
                {
                    var (min, max) = _hoveredObject.GetBoundingBox();
                    _hoverHighlight = new Rectangle { Width = max.X - min.X + 6, Height = max.Y - min.Y + 6, Fill = Brushes.Transparent, Stroke = Brushes.Blue, StrokeThickness = 2, Opacity = 0.5 };
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
                CrosshairH.X1 = 0; CrosshairH.X2 = ActualWidth; CrosshairH.Y1 = CrosshairH.Y2 = mousePos.Y;
                CrosshairV.Y1 = 0; CrosshairV.Y2 = ActualHeight; CrosshairV.X1 = CrosshairV.X2 = mousePos.X;
                CrosshairH.Visibility = CrosshairV.Visibility = Visibility.Visible;
            }
            else CrosshairH.Visibility = CrosshairV.Visibility = Visibility.Collapsed;
        }

        private void UpdateStartPointIndicatorPosition()
        {
            if (_isDrawingWall && StartPointIndicator.Visibility == Visibility.Visible)
            {
                var transformedPoint = MainCanvas.TransformToAncestor(this).Transform(_tempStartPoint.ToPoint());
                Canvas.SetLeft(StartPointIndicator, transformedPoint.X - 5);
                Canvas.SetTop(StartPointIndicator, transformedPoint.Y - 5);
            }
        }

        private void UpdatePreviewWall(Point2D startPoint, Point2D endPoint)
        {
            if (_previewWall == null) return;
            var thickness = 10.0;
            var rect = new Rect(startPoint.ToPoint(), endPoint.ToPoint());
            rect.Inflate(thickness / 2, thickness / 2);
            Canvas.SetLeft(_previewWall, rect.Left);
            Canvas.SetTop(_previewWall, rect.Top);
            _previewWall.Width = rect.Width;
            _previewWall.Height = rect.Height;
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
            var mousePosBefore = e.GetPosition(MainCanvas);
            var delta = e.Delta > 0 ? 1.1 : 0.9;
            _zoomFactor = Math.Max(0.1, Math.Min(_zoomFactor * delta, 10.0));
            MainCanvas.RenderTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
            var mousePosAfter = e.GetPosition(MainCanvas);
            var offset = new Point((mousePosAfter.X - mousePosBefore.X) * _zoomFactor, (mousePosAfter.Y - mousePosBefore.Y) * _zoomFactor);
            CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - offset.X);
            CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset - offset.Y);
            MainCanvas.Width = 2000 * _zoomFactor;
            MainCanvas.Height = 2000 * _zoomFactor;
            UpdateStartPointIndicatorPosition();
            if (_viewModel?.CurrentTool == "Select") UpdateCrosshair(e.GetPosition(this));
            e.Handled = true;
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
            if (_isPanning) { _isPanning = false; MainCanvas.ReleaseMouseCapture(); }
            if (_isDraggingObject) { _isDraggingObject = false; MainCanvas.ReleaseMouseCapture(); }
            if (_isDrawingObject)
            {
                var snappedPosition = SnapToGrid(new Point2D(e.GetPosition(MainCanvas).X, e.GetPosition(MainCanvas).Y));
                HandlePlaceObjectEnd(snappedPosition);
            }
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            CrosshairH.Visibility = CrosshairV.Visibility = Visibility.Collapsed;
            MousePointer.Visibility = Visibility.Collapsed;
            if (_hoverHighlight != null) { TempCanvas.Children.Remove(_hoverHighlight); _hoverHighlight = null; _hoveredObject = null; }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (_viewModel?.CurrentTool == "Select") UpdateCrosshair(e.GetPosition(this));
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
            foreach (var room in _viewModel.DrawingService.Rooms.Where(r => r.IsClosedRoom()))
            {
                var points = GetRoomPoints(room);
                if (points.Count < 3) continue;
                var polygon = new Polygon { Fill = new SolidColorBrush(Color.FromArgb(50, 200, 200, 200)), StrokeThickness = 0 };
                foreach (var point in points) polygon.Points.Add(point.ToPoint());
                RoomCanvas.Children.Add(polygon);
            }
        }

        private void DrawStoreObjects()
        {
            foreach (var obj in _viewModel.DrawingService.StoreObjects) DrawStoreObject(obj);
        }

        private void DrawStoreObject(StoreObject obj)
        {
            double actualWidth = obj.IsHorizontal ? obj.Width : obj.Length;
            double actualLength = obj.IsHorizontal ? obj.Length : obj.Width;
            var rect = new Rectangle { Width = actualWidth, Height = actualLength, Fill = obj.Fill, Stroke = obj.IsSelected ? Brushes.Red : obj.Stroke, StrokeThickness = obj.IsSelected ? 3 : 1 };
            Canvas.SetLeft(rect, obj.Position.X);
            Canvas.SetTop(rect, obj.Position.Y);
            WallCanvas.Children.Add(rect);
            var label = new TextBlock { Text = obj.GetDisplayName(), FontSize = 10, Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), Padding = new Thickness(2) };
            Canvas.SetLeft(label, obj.Position.X + actualWidth / 2 - 20);
            Canvas.SetTop(label, obj.Position.Y + actualLength / 2 - 8);
            LabelCanvas.Children.Add(label);
            if (obj.Layers > 1)
            {
                var layersText = new TextBlock { Text = $"{obj.Layers}층", FontSize = 9, Foreground = Brushes.White, Background = Brushes.Black, Padding = new Thickness(2) };
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
                    if (wall.StartPoint.Equals(current)) { nextWall = wall; nextPoint = wall.EndPoint; break; }
                    else if (wall.EndPoint.Equals(current)) { nextWall = wall; nextPoint = wall.StartPoint; break; }
                }
                if (nextWall == null) break;
                usedWalls.Add(nextWall);
                if (!nextPoint.Equals(points.First())) { points.Add(nextPoint); current = nextPoint; }
                else break;
            }
            return points;
        }

        private void DrawWalls()
        {
            foreach (var wall in _viewModel.DrawingService.Walls) DrawWall(wall);
        }

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
            var wallPolygon = new Polygon { Fill = wall.Fill, Stroke = wall.Stroke, StrokeThickness = 1 };
            wallPolygon.Points.Add(new Point(startPoint.X + perpX, startPoint.Y + perpY));
            wallPolygon.Points.Add(new Point(endPoint.X + perpX, endPoint.Y + perpY));
            wallPolygon.Points.Add(new Point(endPoint.X - perpX, endPoint.Y - perpY));
            wallPolygon.Points.Add(new Point(startPoint.X - perpX, startPoint.Y - perpY));
            WallCanvas.Children.Add(wallPolygon);
            string lengthText = wall.RealLengthInInches.HasValue ? $"{wall.RealLengthInInches.Value / 12.0:F0}'-{wall.RealLengthInInches.Value % 12.0:F0}\"" : wall.LengthDisplay;
            var lengthLabel = new TextBlock { Text = lengthText, FontSize = 12, Background = Brushes.White, Padding = new Thickness(2) };
            bool isHorizontal = Math.Abs(dx) > Math.Abs(dy);
            var formattedText = new FormattedText(lengthText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double textWidth = formattedText.Width, textHeight = formattedText.Height;
            double offsetDistance = 20;
            double labelX = isHorizontal ? wall.MidPoint.X - textWidth / 2 : wall.MidPoint.X - offsetDistance - textWidth;
            double labelY = isHorizontal ? wall.MidPoint.Y - offsetDistance - textHeight / 2 : wall.MidPoint.Y - textHeight / 2;
            Canvas.SetLeft(lengthLabel, labelX);
            Canvas.SetTop(lengthLabel, labelY);
            LabelCanvas.Children.Add(lengthLabel);
            if (isHorizontal)
            {
                var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                lengthLabel.RenderTransformOrigin = new Point(0.5, 0.5);
                lengthLabel.RenderTransform = new RotateTransform(angle > 90 || angle < -90 ? angle + 180 : angle);
            }
        }
    }

    public static class Point2DExtensions
    {
        public static Point ToPoint(this Point2D point2D)
        {
            return new Point(point2D.X, point2D.Y);
        }
    }
    #endregion
}