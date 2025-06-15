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
        private Line _previewWall;
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
        private Point2D _dragStartPosition;

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
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    if (_viewModel.DrawingService != null)
                    {
                        _viewModel.DrawingService.PropertyChanged -= DrawingService_PropertyChanged;
                    }
                }

                _viewModel = value;
                DataContext = _viewModel;

                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                    if (_viewModel.DrawingService != null)
                    {
                        _viewModel.DrawingService.PropertyChanged += DrawingService_PropertyChanged;
                    }
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
            Focusable = true;
            MouseEnter += (s, e) => Focus();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentTool))
            {
                UpdateMousePointerVisibility();
            }
            else if (e.PropertyName == nameof(MainViewModel.DrawingService))
            {
                if (_viewModel.DrawingService != null)
                {
                    _viewModel.DrawingService.PropertyChanged += DrawingService_PropertyChanged;
                }
                RedrawAll();
            }
        }

        private void DrawingService_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(RedrawAll);
        }

        public void CopySelectedObject()
        {
            if (_selectedObject != null)
            {
                _copiedObject = _selectedObject.Clone();
                if (_viewModel != null) _viewModel.StatusText = $"{_copiedObject.GetDisplayName()} 복사됨.";
            }
        }

        public void PasteCopiedObject(Point pastePosition)
        {
            if (_copiedObject == null) return;
            double actualWidth = _copiedObject.IsHorizontal ? _copiedObject.Width : _copiedObject.Length;
            double actualLength = _copiedObject.IsHorizontal ? _copiedObject.Length : _copiedObject.Width;

            double newX = pastePosition.X - (actualWidth / 2.0);
            double newY = pastePosition.Y - (actualLength / 2.0);

            Point2D snappedPosition = SnapToGrid(new Point2D(newX, newY));

            _viewModel.DrawingService.AddStoreObject(
                _copiedObject.Type,
                snappedPosition,
                _copiedObject.Width,
                _copiedObject.Length,
                _copiedObject.Height,
                _copiedObject.Layers,
                _copiedObject.IsHorizontal,
                _copiedObject.Temperature,
                _copiedObject.CategoryCode
            );
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
        }

        private void HandlePlaceObjectEnd(Point2D position)
        {
            if (!_isDrawingObject || _objectPreview == null) return;

            var width = Math.Abs(position.X - _objectStartPoint.X);
            var length = Math.Abs(position.Y - _objectStartPoint.Y);

            TempCanvas.Children.Remove(_objectPreview);
            _objectPreview = null;
            _isDrawingObject = false;
            Mouse.Capture(null);

            if (width > 10 && length > 10)
            {
                var objectTypeStr = MainWindow?.GetCurrentObjectTool();
                if (!string.IsNullOrEmpty(objectTypeStr) && Enum.TryParse(objectTypeStr, out ObjectType type))
                {
                    var topLeft = new Point2D(Math.Min(_objectStartPoint.X, position.X), Math.Min(_objectStartPoint.Y, position.Y));
                    _viewModel.DrawingService.AddStoreObject(type, topLeft, width, length);
                }
            }
        }

        private void HandleSelectTool(Point2D position, MouseButtonEventArgs e)
        {
            // 먼저 감지된 객체를 선택하는지 확인
            var detectedObj = GetDetectedObjectAt(position.ToPoint());
            if (detectedObj != null)
            {
                // 감지된 객체를 실제 StoreObject로 변환
                ConvertDetectedObject(detectedObj);
                return;
            }

            var obj = _viewModel.DrawingService.GetObjectAt(position);
            SelectObject(obj);

            if (obj != null)
            {
                _isDraggingObject = true;
                _dragOffset = new Point2D(position.X - obj.Position.X, position.Y - obj.Position.Y);
                _dragStartPosition = obj.Position;
                MainCanvas.CaptureMouse();
            }
            else
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(this);
                MainCanvas.CaptureMouse();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                CancelWallDrawing();
                CancelObjectDrawing();
                SelectObject(null);
                _viewModel.CurrentTool = "Select";
            }
        }


        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var mousePos = e.GetPosition(MainCanvas);
            Point2D snappedPosition = mousePos.ToPoint2D();

            if (_isDrawingWall && _previewWall != null)
            {
                UpdateWallPreview(snappedPosition);
            }
            else if (_isDrawingObject && _objectPreview != null)
            {
                UpdateObjectPreview(snappedPosition);
            }
            else if (_isDraggingObject && _selectedObject != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var newPos = new Point2D(mousePos.X - _dragOffset.X, mousePos.Y - _dragOffset.Y);
                _selectedObject.Position = SnapToGrid(newPos);
                RedrawAll();
            }
            else if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                var delta = currentPoint - _lastPanPoint;
                CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - delta.X);
                CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset - delta.Y);
                _lastPanPoint = currentPoint;
            }
            else if (_viewModel?.CurrentTool == "Select")
            {
                UpdateObjectHover(mousePos.ToPoint2D());
            }
            else if (_viewModel.CurrentTool == "WallStraight" && !_isDrawingWall)
            {
                snappedPosition = SnapToGrid(mousePos.ToPoint2D());
                MousePointer.Visibility = Visibility.Visible;
                Canvas.SetLeft(MousePointer, snappedPosition.X - MousePointer.Width / 2);
                Canvas.SetTop(MousePointer, snappedPosition.Y - MousePointer.Height / 2);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isPanning)
            {
                _isPanning = false;
                MainCanvas.ReleaseMouseCapture();
            }
            else if (_isDraggingObject && _selectedObject != null)
            {
                _isDraggingObject = false;
                MainCanvas.ReleaseMouseCapture();
                var finalPosition = SnapToGrid(_selectedObject.Position);
                _selectedObject.Position = finalPosition; // 최종 위치 스냅

                if (_selectedObject.Position.DistanceTo(_dragStartPosition) > 1)
                {
                    _viewModel.DrawingService.MoveStoreObject(_selectedObject, _dragStartPosition, finalPosition);
                }
                RedrawAll();
            }
            else if (_isDrawingObject)
            {
                var snappedPosition = new Point2D(e.GetPosition(MainCanvas).X, e.GetPosition(MainCanvas).Y);
                HandlePlaceObjectEnd(snappedPosition);
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double zoom = e.Delta > 0 ? 1.1 : 1 / 1.1;
                _zoomFactor *= zoom;
                MainCanvas.LayoutTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
                e.Handled = true;
            }
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

        private void DrawStoreObjects()
        {
            if (_viewModel?.DrawingService?.StoreObjects == null) return;
            foreach (var obj in _viewModel.DrawingService.StoreObjects)
            {
                DrawStoreObject(obj);
            }
        }

        private void ConvertDetectedObject(DetectedObject detectedObject)
        {
            if (detectedObject.ConvertedStoreObject != null)
            {
                SelectObject(detectedObject.ConvertedStoreObject);
                return;
            }

            var dialog = new ObjectTypeSelectionDialog();
            dialog.Owner = MainWindow;
            if (dialog.ShowDialog() == true)
            {
                var newObj = detectedObject.ToStoreObjectWithProperties(
                    dialog.ObjectWidth, dialog.ObjectHeight, dialog.ObjectLength,
                    dialog.ObjectLayers, dialog.IsHorizontal, dialog.Temperature, dialog.CategoryCode);

                newObj.Position = new Point2D(detectedObject.Bounds.Left, detectedObject.Bounds.Top);

                // AddStoreObject의 올바른 오버로드를 사용
                var addedObj = _viewModel.DrawingService.AddStoreObject(
                    newObj.Type,
                    newObj.Position,
                    newObj.Width,
                    newObj.Length,
                    newObj.Height,
                    newObj.Layers,
                    newObj.IsHorizontal,
                    newObj.Temperature,
                    newObj.CategoryCode);

                detectedObject.ConvertedStoreObject = addedObj;
                _detectedObjectsCanvas.Children.Remove(detectedObject.OverlayShape);
                _detectedObjectsCanvas.Children.Remove(detectedObject.SelectionBorder);

                SelectObject(addedObj);
            }
        }


        #region Other Methods

        public void SetBackgroundImage(string imagePath)
        {
            try
            {
                _loadedFloorPlan = new BitmapImage(new Uri(imagePath));
                if (_backgroundImage == null)
                {
                    _backgroundImage = new Image { Opacity = 0.5 };
                    BackgroundCanvas.Children.Add(_backgroundImage);
                    Canvas.SetZIndex(_backgroundImage, -1);
                }
                _backgroundImage.Source = _loadedFloorPlan;
                _backgroundImage.Width = MainCanvas.Width;
                _backgroundImage.Height = MainCanvas.Height;
                ClearDetectedObjects();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"배경 이미지 설정 오류: {ex.Message}");
            }
        }

        public void ClearBackgroundImage()
        {
            if (_backgroundImage != null)
            {
                BackgroundCanvas.Children.Remove(_backgroundImage);
                _backgroundImage = null;
            }
            _loadedFloorPlan = null;
            ClearDetectedObjects();
        }

        public void DetectObjectsInFloorPlan()
        {
            if (_loadedFloorPlan == null) return;
            var analyzer = new FloorPlanAnalyzer();
            var bounds = analyzer.FindFloorPlanBounds(_loadedFloorPlan);
            if (bounds == null)
            {
                MessageBox.Show("도면 경계를 찾을 수 없습니다.");
                return;
            }

            _detectedObjects = analyzer.DetectFloorPlanObjects(_loadedFloorPlan, bounds);
            DrawDetectedObjects();
        }

        private DetectedObject GetDetectedObjectAt(Point p)
        {
            return _detectedObjects.FirstOrDefault(d => d.Bounds.Contains(p) && d.ConvertedStoreObject == null);
        }


        public int GetDetectedObjectsCount()
        {
            return _detectedObjects?.Count ?? 0;
        }

        private void UpdateMousePointerVisibility()
        {
            if (_viewModel.CurrentTool == "WallStraight" || _viewModel.CurrentTool == "PlaceObject")
            {
                MousePointer.Visibility = Visibility.Visible;
            }
            else
            {
                MousePointer.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateStartPointIndicatorPosition()
        {
            // 이 메서드는 StartPointIndicator라는 UI 요소가 XAML에 필요합니다.
            // 현재는 해당 요소가 없으므로 이 메서드는 비워둡니다.
            // 예: Canvas.SetLeft(StartPointIndicator, _tempStartPoint.X - StartPointIndicator.Width / 2);
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
                Mouse.Capture(MainCanvas);
                _viewModel.StatusText = "영역을 드래그하여 크기를 지정하세요";
            }
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

        private void UpdateWallPreview(Point2D currentPos)
        {
            if (_previewWall != null)
            {
                _previewWall.X1 = _tempStartPoint.X;
                _previewWall.Y1 = _tempStartPoint.Y;
                _previewWall.X2 = currentPos.X;
                _previewWall.Y2 = currentPos.Y;
            }
        }

        // 커서 모양을 업데이트하는 헬퍼 메서드
        private void UpdateCursorBasedOnTool()
        {
            if (_viewModel == null) return;
            switch (_viewModel.CurrentTool)
            {
                case "WallStraight":
                case "PlaceObject":
                    this.Cursor = Cursors.Cross;
                    break;
                case "Select":
                default:
                    this.Cursor = Cursors.Arrow;
                    break;
            }
        }

        private void DrawingCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            DrawGrid();
            Focus();
            // ViewModel 속성이 변경될 때 커서가 업데이트되도록 수정
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(MainViewModel.CurrentTool))
                    {
                        UpdateCursorBasedOnTool();
                    }
                };
            }
            UpdateCursorBasedOnTool();
        }

        private void DrawDetectedObjects()
        {
            ClearDetectedObjects();
            foreach (var obj in _detectedObjects)
            {
                var rect = new Rectangle
                {
                    Width = obj.Bounds.Width,
                    Height = obj.Bounds.Height,
                    Fill = new SolidColorBrush(Color.FromArgb(80, 255, 165, 0)),
                    Stroke = Brushes.OrangeRed,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(rect, obj.Bounds.Left);
                Canvas.SetTop(rect, obj.Bounds.Top);
                obj.OverlayShape = rect;
                _detectedObjectsCanvas.Children.Add(rect);
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

        private void DrawRooms()
        {
            if (_viewModel?.DrawingService?.Rooms == null) return;
            foreach (var room in _viewModel.DrawingService.Rooms.Where(r => r.IsClosedRoom()))
            {
                var points = GetRoomPoints(room);
                if (points.Count < 3) continue;
                var polygon = new Polygon { Fill = new SolidColorBrush(Color.FromArgb(50, 200, 200, 200)), StrokeThickness = 0 };
                foreach (var point in points) polygon.Points.Add(point.ToPoint());
                RoomCanvas.Children.Add(polygon);
            }
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
            if (_viewModel?.DrawingService?.Walls == null) return;
            foreach (var wall in _viewModel.DrawingService.Walls) DrawWall(wall);
        }

        private void DrawWall(Wall wall)
        {
            var line = new Line
            {
                X1 = wall.StartPoint.X,
                Y1 = wall.StartPoint.Y,
                X2 = wall.EndPoint.X,
                Y2 = wall.EndPoint.Y,
                Stroke = wall.Stroke,
                StrokeThickness = wall.Thickness
            };
            WallCanvas.Children.Add(line);
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
                    _hoverHighlight = new Rectangle { Width = max.X - min.X, Height = max.Y - min.Y, Fill = Brushes.Transparent, Stroke = Brushes.Blue, StrokeThickness = 2, Opacity = 0.5 };
                    Canvas.SetLeft(_hoverHighlight, min.X);
                    Canvas.SetTop(_hoverHighlight, min.Y);
                    TempCanvas.Children.Add(_hoverHighlight);
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
                Mouse.Capture(null);
            }
        }

        private Point2D SnapToGrid(Point2D point)
        {
            return new Point2D(Math.Round(point.X / GridSize) * GridSize, Math.Round(point.Y / GridSize) * GridSize);
        }

        private void ClearDetectedObjects()
        {
            _detectedObjectsCanvas.Children.Clear();
            _detectedObjects.Clear();
            _hoveredDetectedObject = null;
        }

        private void HandleWallTool(Point2D position)
        {
            if (!_isDrawingWall)
            {
                _tempStartPoint = position;
                _isDrawingWall = true;
                _viewModel.StatusText = "직선 벽 그리기: 끝점을 클릭하세요";
                _previewWall = new Line { Stroke = Brushes.Blue, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 5, 5 } };
                TempCanvas.Children.Add(_previewWall);
            }
            else
            {
                _viewModel.DrawingService.AddWall(_tempStartPoint, position);
                _isDrawingWall = false;
                _viewModel.StatusText = "직선 벽 그리기: 시작점을 클릭하세요";
                TempCanvas.Children.Remove(_previewWall);
                _previewWall = null;
            }
        }

        private void CancelWallDrawing()
        {
            if (_isDrawingWall)
            {
                _isDrawingWall = false;
                if (_previewWall != null) { TempCanvas.Children.Remove(_previewWall); _previewWall = null; }
                if (_viewModel != null) _viewModel.StatusText = _viewModel.CurrentTool == "WallStraight" ? "직선 벽 그리기: 시작점을 클릭하세요" : "도구를 선택하세요";
            }
        }

        #endregion
    }

    public static class PointExtensions
    {
        public static Point ToPoint(this Point2D point2D)
        {
            return new Point(point2D.X, point2D.Y);
        }

        public static Point2D ToPoint2D(this Point point)
        {
            return new Point2D(point.X, point.Y);
        }
    }
}