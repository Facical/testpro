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
        private Point2D _dragStartPosition; // 이동 시작 위치 기록

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

        // DrawingService의 내부 데이터가 변경될 때마다 RedrawAll() 호출
        private void DrawingService_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(RedrawAll);
        }

        public void CopySelectedObject()
        {
            if (_selectedObject != null)
            {
                _copiedObject = _selectedObject;
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
            var height = Math.Abs(position.Y - _objectStartPoint.Y);

            TempCanvas.Children.Remove(_objectPreview);
            _objectPreview = null;
            _isDrawingObject = false;

            if (width > 10 && height > 10)
            {
                var objectTypeStr = MainWindow?.GetCurrentObjectTool();
                if (!string.IsNullOrEmpty(objectTypeStr) && Enum.TryParse(objectTypeStr, out ObjectType type))
                {
                    var topLeft = new Point2D(Math.Min(_objectStartPoint.X, position.X), Math.Min(_objectStartPoint.Y, position.Y));
                    // AddStoreObject 호출 시 크기 정보도 함께 전달
                    _viewModel.DrawingService.AddStoreObject(type, topLeft, width, height);
                }
            }
        }

        private void HandleSelectTool(Point2D position, MouseButtonEventArgs e)
        {
            var obj = _viewModel.DrawingService.GetObjectAt(position);
            SelectObject(obj);

            if (obj != null)
            {
                _isDraggingObject = true;
                _dragOffset = new Point2D(position.X - obj.Position.X, position.Y - obj.Position.Y);
                _dragStartPosition = obj.Position; // 이동 시작 위치 기록
                MainCanvas.CaptureMouse();
            }
            else
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(this);
                MainCanvas.CaptureMouse();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var mousePos = e.GetPosition(MainCanvas);
            var snappedPosition = SnapToGrid(new Point2D(mousePos.X, mousePos.Y));

            if (_isDrawingObject && _objectPreview != null)
            {
                UpdateObjectPreview(snappedPosition);
            }
            else if (_isDraggingObject && _selectedObject != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var newPosition = new Point2D(snappedPosition.X - _dragOffset.X, snappedPosition.Y - _dragOffset.Y);
                // 실시간으로 위치만 변경 (Undo/Redo 기록은 마우스를 놓을 때)
                _selectedObject.Position = newPosition;
                RedrawAll(); // 실시간으로 끌기 표시
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
                UpdateObjectHover(mousePos);
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
                // 드래그가 끝났을 때만 이동 기록
                if (_selectedObject.Position.DistanceTo(_dragStartPosition) > 1) // 실제로 움직였을 때만
                {
                    _viewModel.DrawingService.MoveStoreObject(_selectedObject, _dragStartPosition, _selectedObject.Position);
                }
                _isDraggingObject = false;
                MainCanvas.ReleaseMouseCapture();
            }
            else if (_isDrawingObject)
            {
                var snappedPosition = SnapToGrid(new Point2D(e.GetPosition(MainCanvas).X, e.GetPosition(MainCanvas).Y));
                HandlePlaceObjectEnd(snappedPosition);
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

        // --- 이하 다른 메서드는 이전과 동일 (생략) ---
        #region Other Unchanged Methods

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

        private void DrawingCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            DrawGrid();
            Focus();
            UpdateMousePointerVisibility();
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
            }
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

        #endregion
    }

    public static class Point2DExtensions
    {
        public static Point ToPoint(this Point2D point2D)
        {
            return new Point(point2D.X, point2D.Y);
        }
    }
}