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
using System.Diagnostics;
using testpro.Services;
using testpro.ViewModels;
using testpro.Dialogs;

namespace testpro.Views
{
    public partial class DrawingCanvas : UserControl
    {
        // --- 기존 필드 선언 (변경 없음) ---
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
        private Point2D _dragStartPosition;

        // --- 객체 감지 관련 필드 (변경 없음) ---
        private List<DetectedObject> _detectedObjects = new List<DetectedObject>();
        private Canvas _detectedObjectsCanvas;
        private DetectedObject _hoveredDetectedObject;
        private StoreObject _copiedObject = null;

        // --- public 속성 (수정됨) ---
        public MainWindow MainWindow { get; set; }
        public Image BackgroundImage => _backgroundImage; // MainWindow에서 접근 가능하도록 추가

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
            // --- _detectedObjectsCanvas를 MainCanvas에 추가하는 로직으로 변경 ---
            _detectedObjectsCanvas = new Canvas { IsHitTestVisible = true };
            // XAML에 명시적으로 MainCanvas가 있으므로 코드에서 직접 추가합니다.
            // MainCanvas가 로드된 후 추가하는 것이 더 안정적일 수 있으나, 생성자에서 추가해도 일반적으로 동작합니다.
            var mainCanvas = this.FindName("MainCanvas") as Canvas;
            if (mainCanvas != null)
            {
                mainCanvas.Children.Add(_detectedObjectsCanvas);
                Canvas.SetZIndex(_detectedObjectsCanvas, 10); // 다른 요소들 위에 오도록 Z-Index 설정
            }

            Loaded += DrawingCanvas_Loaded;
            Focusable = true;
            MouseEnter += (s, e) => Focus();

        }

        // --- ViewModel 및 DrawingService 이벤트 핸들러 (변경 없음) ---
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentTool))
            {
                // UpdateMousePointerVisibility(); // 이 메서드는 현재 구현되지 않았으므로 주석 처리하거나 필요 시 구현
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

        // --- 복사/붙여넣기 메서드 (변경 없음) ---
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
                _copiedObject.Type, snappedPosition, _copiedObject.Width, _copiedObject.Length, _copiedObject.Height,
                _copiedObject.Layers, _copiedObject.IsHorizontal, _copiedObject.Temperature, _copiedObject.CategoryCode
            );
        }

        // --- 마우스 이벤트 핸들러 (변경 없음) ---
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            var mainCanvas = this.FindName("MainCanvas") as IInputElement;
            if (mainCanvas == null) return;
            var position = e.GetPosition(mainCanvas);
            var snappedPosition = SnapToGrid(new Point2D(position.X, position.Y));

            switch (_viewModel?.CurrentTool)
            {
                case "PlaceObject":
                    HandlePlaceObjectStart(snappedPosition);
                    break;
                case "Select":
                    HandleSelectTool(snappedPosition, e);
                    break;
                case "WallStraight":
                    HandleWallTool(snappedPosition);
                    break;
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            var mainCanvas = sender as IInputElement;
            if (mainCanvas == null) return;
            var position = e.GetPosition(mainCanvas);
            var snappedPosition = SnapToGrid(new Point2D(position.X, position.Y));

            switch (_viewModel?.CurrentTool)
            {
                case "PlaceObject":
                    HandlePlaceObjectStart(snappedPosition);
                    break;
                case "Select":
                    HandleSelectTool(snappedPosition, e);
                    break;
                case "WallStraight":
                    HandleWallTool(snappedPosition);
                    break;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var mainCanvas = sender as IInputElement;
            if (mainCanvas == null) return;
            var mousePos = e.GetPosition(mainCanvas);
            var snappedPosition = SnapToGrid(new Point2D(mousePos.X, mousePos.Y));

            if (_isDrawingWall && _previewWall != null)
            {
                UpdatePreviewWall(_tempStartPoint, snappedPosition);
            }
            else if (_isDrawingObject && _objectPreview != null)
            {
                UpdateObjectPreview(snappedPosition);
            }
            else if (_isDraggingObject && _selectedObject != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var newPosition = new Point2D(snappedPosition.X - _dragOffset.X, snappedPosition.Y - _dragOffset.Y);
                _selectedObject.Position = newPosition;
                RedrawAll();
            }
            else if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var scrollViewer = this.FindName("CanvasScrollViewer") as ScrollViewer;
                var currentPoint = e.GetPosition(this);
                var delta = currentPoint - _lastPanPoint;
                scrollViewer?.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - delta.X);
                scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset - delta.Y);
                _lastPanPoint = currentPoint;
            }
            else if (_viewModel?.CurrentTool == "Select")
            {
                UpdateObjectHover(new Point2D(mousePos.X, mousePos.Y));
            }
        }

        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var mainCanvas = sender as UIElement;
            if (mainCanvas == null) return;

            if (_isPanning)
            {
                _isPanning = false;
                mainCanvas.ReleaseMouseCapture();
            }
            else if (_isDraggingObject && _selectedObject != null)
            {
                _isDraggingObject = false;
                mainCanvas.ReleaseMouseCapture();
            }
            else if (_isDrawingObject)
            {
                var snappedPosition = SnapToGrid(new Point2D(e.GetPosition(mainCanvas).X, e.GetPosition(mainCanvas).Y));
                HandlePlaceObjectEnd(snappedPosition);
            }
        }


        // --- 이하 모든 메서드는 기존 코드와 동일하거나, 이전 버전의 코드를 복원한 것입니다. ---

        // =================================================================
        // ===== 여기서부터 객체 감지 및 상호작용 관련 로직 (복원된 코드) =====
        // =================================================================

        private void UpdatePreviewWall(Point2D startPoint, Point2D endPoint)
        {
            if (_previewWall == null) return;

            // 참고: 예전 코드에서는 벽 두께를 고려하여 미리보기 영역을 계산했습니다.
            // 이는 벽을 나타내는 사각형이므로 정상적인 로직입니다.
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

        public void DetectObjectsInFloorPlan()
        {
            if (_loadedFloorPlan == null || _backgroundImage == null)
            {
                MessageBox.Show("먼저 도면 이미지를 불러와주세요.");
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            ClearDetectedObjects(); // 이전 감지 결과 제거

            var analyzer = new FloorPlanAnalyzer();
            var bounds = analyzer.FindFloorPlanBounds(_loadedFloorPlan);
            if (bounds != null)
            {
                var detected = analyzer.DetectFloorPlanObjects(_loadedFloorPlan, bounds);

                // 이미지 좌표를 캔버스 좌표로 변환
                double imageLeft = Canvas.GetLeft(_backgroundImage);
                double imageTop = Canvas.GetTop(_backgroundImage);
                double scaleX = _backgroundImage.Width / _loadedFloorPlan.PixelWidth;
                double scaleY = _backgroundImage.Height / _loadedFloorPlan.PixelHeight;

                foreach (var obj in detected)
                {
                    var canvasRect = new Rect(
                        imageLeft + obj.Bounds.Left * scaleX,
                        imageTop + obj.Bounds.Top * scaleY,
                        obj.Bounds.Width * scaleX,
                        obj.Bounds.Height * scaleY
                    );
                    obj.Bounds = canvasRect;
                    _detectedObjects.Add(obj);
                    CreateDetectedObjectOverlay(obj);
                }
            }
            else
            {
                MessageBox.Show("도면에서 경계를 찾을 수 없습니다.");
            }

            Mouse.OverrideCursor = null;
        }

        private void ClearDetectedObjects()
        {
            _detectedObjectsCanvas.Children.Clear();
            _detectedObjects.Clear();
            _hoveredDetectedObject = null;
        }

        private void CreateDetectedObjectOverlay(DetectedObject obj)
        {
            var overlay = new Rectangle
            {
                Width = obj.Bounds.Width,
                Height = obj.Bounds.Height,
                Fill = Brushes.Transparent, // 기본적으로 투명
                Stroke = Brushes.Transparent,
                StrokeThickness = 2,
                Tag = obj,
                Cursor = Cursors.Hand
            };

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
                rect.Fill = new SolidColorBrush(Color.FromArgb(80, 0, 120, 255)); // 파란색 하이라이트
                rect.Stroke = Brushes.DodgerBlue;
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
                e.Handled = true; // 이벤트가 다른 곳으로 전파되지 않도록 처리
            }
        }

        private void ShowObjectTypeSelectionDialog(DetectedObject detectedObj)
        {
            // 이미 변환된 객체는 다이얼로그를 다시 띄우지 않음
            if (detectedObj.ConvertedStoreObject != null) return;

            var dialog = new ObjectTypeSelectionDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                var storeType = ObjectTypeSelectionDialog.ConvertToObjectType(dialog.SelectedType);
                var position = new Point2D(detectedObj.Bounds.Left, detectedObj.Bounds.Top);

                // DrawingService의 AddStoreObject 메서드를 사용하여 객체 추가
                var newStoreObject = _viewModel.DrawingService.AddStoreObject(
                    storeType, position, dialog.ObjectWidth, dialog.ObjectLength, dialog.ObjectHeight,
                    dialog.ObjectLayers, dialog.IsHorizontal, dialog.Temperature, dialog.CategoryCode
                );

                detectedObj.ConvertedStoreObject = newStoreObject;
                detectedObj.IsSelected = true;

                // 오버레이 스타일 변경 (녹색으로)
                if (detectedObj.OverlayShape is Rectangle rect)
                {
                    rect.Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 120));
                    rect.Stroke = Brushes.Green;
                }
            }
        }

        public int GetDetectedObjectsCount()
        {
            return _detectedObjects?.Count ?? 0;
        }

        // =================================================================
        // ===== 이하 나머지 메서드 (기존 코드와 동일) ======================
        // =================================================================

        private void HandlePlaceObjectEnd(Point2D position)
        {
            var tempCanvas = this.FindName("TempCanvas") as Canvas;
            if (!_isDrawingObject || _objectPreview == null || tempCanvas == null) return;

            var width = Math.Abs(position.X - _objectStartPoint.X);
            var height = Math.Abs(position.Y - _objectStartPoint.Y);

            tempCanvas.Children.Remove(_objectPreview);
            _objectPreview = null;
            _isDrawingObject = false;

            if (width > 10 && height > 10)
            {
                var objectTypeStr = MainWindow?.GetCurrentObjectTool();
                if (!string.IsNullOrEmpty(objectTypeStr) && Enum.TryParse(objectTypeStr, out ObjectType type))
                {
                    var topLeft = new Point2D(Math.Min(_objectStartPoint.X, position.X), Math.Min(_objectStartPoint.Y, position.Y));
                    _viewModel.DrawingService.AddStoreObject(type, topLeft, width, height); // 수정된 AddStoreObject 호출
                }
            }
        }

        private void HandleSelectTool(Point2D position, MouseButtonEventArgs e)
        {
            var mainCanvas = this.FindName("MainCanvas") as UIElement;
            var obj = _viewModel.DrawingService.GetObjectAt(position);
            SelectObject(obj);

            if (obj != null)
            {
                _isDraggingObject = true;
                _dragOffset = new Point2D(position.X - obj.Position.X, position.Y - obj.Position.Y);
                _dragStartPosition = obj.Position;
                mainCanvas?.CaptureMouse();
            }
            else
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(this);
                mainCanvas?.CaptureMouse();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var mainCanvas = this.FindName("MainCanvas") as IInputElement;
            if (mainCanvas == null) return;
            var mousePos = e.GetPosition(mainCanvas);
            var snappedPosition = SnapToGrid(new Point2D(mousePos.X, mousePos.Y));

            // ▼▼▼ 이 if 문이 핵심입니다. ▼▼▼
            if (_isDrawingWall && _previewWall != null)
            {
                // 벽을 그리는 중이면 벽 미리보기를 업데이트합니다.
                UpdatePreviewWall(_tempStartPoint, snappedPosition);
            }
            else if (_isDrawingObject && _objectPreview != null)
            {
                UpdateObjectPreview(snappedPosition);
            }
            else if (_isDraggingObject && _selectedObject != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var newPosition = new Point2D(snappedPosition.X - _dragOffset.X, snappedPosition.Y - _dragOffset.Y);
                _selectedObject.Position = newPosition;
                RedrawAll();
            }
            else if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var scrollViewer = this.FindName("CanvasScrollViewer") as ScrollViewer;
                var currentPoint = e.GetPosition(this);
                var delta = currentPoint - _lastPanPoint;
                scrollViewer?.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - delta.X);
                scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset - delta.Y);
                _lastPanPoint = currentPoint;
            }
            else if (_viewModel?.CurrentTool == "Select")
            {
                UpdateObjectHover(new Point2D(mousePos.X, mousePos.Y));
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            var mainCanvas = this.FindName("MainCanvas") as UIElement;
            if (mainCanvas == null) return;

            if (_isPanning)
            {
                _isPanning = false;
                mainCanvas.ReleaseMouseCapture();
            }
            else if (_isDraggingObject && _selectedObject != null)
            {
                _isDraggingObject = false;
                mainCanvas.ReleaseMouseCapture();
            }
            else if (_isDrawingObject)
            {
                var snappedPosition = SnapToGrid(new Point2D(e.GetPosition(mainCanvas).X, e.GetPosition(mainCanvas).Y));
                HandlePlaceObjectEnd(snappedPosition);
            }
        }

        public void RedrawAll()
        {
            var wallCanvas = this.FindName("WallCanvas") as Canvas;
            var labelCanvas = this.FindName("LabelCanvas") as Canvas;
            var roomCanvas = this.FindName("RoomCanvas") as Canvas;
            if (wallCanvas == null || labelCanvas == null || roomCanvas == null || _viewModel?.DrawingService == null) return;

            wallCanvas.Children.Clear();
            labelCanvas.Children.Clear();
            roomCanvas.Children.Clear();
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

        private Point2D SnapToGrid(Point2D point)
        {
            return new Point2D(Math.Round(point.X / GridSize) * GridSize, Math.Round(point.Y / GridSize) * GridSize);
        }

        public void SetBackgroundImage(string imagePath)
        {
            var backgroundCanvas = this.FindName("BackgroundCanvas") as Canvas;
            var scrollViewer = this.FindName("CanvasScrollViewer") as ScrollViewer;
            var mainCanvas = this.FindName("MainCanvas") as Canvas;
            if (backgroundCanvas == null || scrollViewer == null || mainCanvas == null) return;

            try
            {
                _loadedFloorPlan = new BitmapImage();
                _loadedFloorPlan.BeginInit();
                _loadedFloorPlan.UriSource = new Uri(imagePath);
                _loadedFloorPlan.CacheOption = BitmapCacheOption.OnLoad;
                _loadedFloorPlan.EndInit();

                if (_backgroundImage == null)
                {
                    _backgroundImage = new Image { Opacity = 0.5 };
                    backgroundCanvas.Children.Add(_backgroundImage);
                }
                _backgroundImage.Source = _loadedFloorPlan;

                double viewWidth = scrollViewer.ActualWidth;
                double viewHeight = scrollViewer.ActualHeight;
                double imgWidth = _loadedFloorPlan.PixelWidth;
                double imgHeight = _loadedFloorPlan.PixelHeight;
                double scaleX = viewWidth / imgWidth;
                double scaleY = viewHeight / imgHeight;
                double finalScale = Math.Min(scaleX, scaleY) * 0.95;

                _backgroundImage.Width = imgWidth * finalScale;
                _backgroundImage.Height = imgHeight * finalScale;

                Canvas.SetLeft(_backgroundImage, (mainCanvas.Width - _backgroundImage.Width) / 2);
                Canvas.SetTop(_backgroundImage, (mainCanvas.Height - _backgroundImage.Height) / 2);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"배경 이미지 로딩 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        public void ClearBackgroundImage()
        {
            var backgroundCanvas = this.FindName("BackgroundCanvas") as Canvas;
            if (backgroundCanvas != null && _backgroundImage != null)
            {
                backgroundCanvas.Children.Remove(_backgroundImage);
                _backgroundImage = null;
                _loadedFloorPlan = null;
                ClearDetectedObjects(); // 배경 제거 시 감지된 객체도 제거
            }
        }

        private void SelectObject(StoreObject obj)
        {
            if (_selectedObject != null) _selectedObject.IsSelected = false;
            _selectedObject = obj;
            if (obj != null)
            {
                obj.IsSelected = true;
                if (_viewModel != null) _viewModel.StatusText = $"{obj.GetDisplayName()} 선택됨";
            }
            MainWindow?.SelectObject(obj);
            RedrawAll();
        }

        private void HandlePlaceObjectStart(Point2D position)
        {
            var tempCanvas = this.FindName("TempCanvas") as Canvas;
            if (tempCanvas == null) return;

            if (!_isDrawingObject)
            {
                _objectStartPoint = position;
                _isDrawingObject = true;
                _objectPreview = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 255)), Stroke = Brushes.Blue, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 5, 5 } };
                Canvas.SetLeft(_objectPreview, position.X);
                Canvas.SetTop(_objectPreview, position.Y);
                tempCanvas.Children.Add(_objectPreview);
                if (_viewModel != null) _viewModel.StatusText = "영역을 드래그하여 크기를 지정하세요";
            }
        }

        private void UpdateObjectPreview(Point2D currentPos)
        {
            if (_objectPreview != null)
            {
                var rect = new Rect(new Point(_objectStartPoint.X, _objectStartPoint.Y), new Point(currentPos.X, currentPos.Y));
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
        }

        private void DrawGrid()
        {
            var gridCanvas = this.FindName("GridCanvas") as Canvas;
            var mainCanvas = this.FindName("MainCanvas") as Canvas;
            if (gridCanvas == null || mainCanvas == null) return;

            gridCanvas.Children.Clear();
            var width = mainCanvas.Width;
            var height = mainCanvas.Height;
            for (double x = 0; x <= width; x += GridSize)
                gridCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, Stroke = Brushes.LightGray, StrokeThickness = x % (GridSize * 12) == 0 ? 1 : 0.5, Opacity = 0.5 });
            for (double y = 0; y <= height; y += GridSize)
                gridCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = Brushes.LightGray, StrokeThickness = y % (GridSize * 12) == 0 ? 1 : 0.5, Opacity = 0.5 });
        }

        private void HandleWallTool(Point2D position)
        {
            var tempCanvas = this.FindName("TempCanvas") as Canvas;
            var startPointIndicator = this.FindName("StartPointIndicator") as UIElement;
            if (tempCanvas == null || startPointIndicator == null) return;

            if (!_isDrawingWall)
            {
                _tempStartPoint = position;
                _isDrawingWall = true;
                if (_viewModel != null) _viewModel.StatusText = "직선 벽 그리기: 끝점을 클릭하세요";
                startPointIndicator.Visibility = Visibility.Visible;
                _previewWall = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(100, 200, 200, 255)), Stroke = Brushes.Blue, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 5, 5 } };
                tempCanvas.Children.Add(_previewWall);
            }
            else
            {
                _viewModel.DrawingService.AddWall(_tempStartPoint, position);
                _isDrawingWall = false;
                if (_viewModel != null) _viewModel.StatusText = "직선 벽 그리기: 시작점을 클릭하세요";
                tempCanvas.Children.Remove(_previewWall);
                _previewWall = null;
                startPointIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void DrawRooms()
        {
            var roomCanvas = this.FindName("RoomCanvas") as Canvas;
            if (roomCanvas == null || _viewModel?.DrawingService?.Rooms == null) return;

            foreach (var room in _viewModel.DrawingService.Rooms.Where(r => r.IsClosedRoom()))
            {
                var points = GetRoomPoints(room);
                if (points.Count < 3) continue;
                var polygon = new Polygon { Fill = new SolidColorBrush(Color.FromArgb(50, 200, 200, 200)), StrokeThickness = 0 };
                foreach (var point in points) polygon.Points.Add(new Point(point.X, point.Y));
                roomCanvas.Children.Add(polygon);
            }
        }
        private void DrawStoreObject(StoreObject obj)
        {
            var wallCanvas = this.FindName("WallCanvas") as Canvas;
            var labelCanvas = this.FindName("LabelCanvas") as Canvas;
            if (wallCanvas == null || labelCanvas == null) return;

            double actualWidth = obj.IsHorizontal ? obj.Width : obj.Length;
            double actualLength = obj.IsHorizontal ? obj.Length : obj.Width;
            var rect = new Rectangle { Width = actualWidth, Height = actualLength, Fill = obj.Fill, Stroke = obj.IsSelected ? Brushes.Red : obj.Stroke, StrokeThickness = obj.IsSelected ? 3 : 1 };
            Canvas.SetLeft(rect, obj.Position.X);
            Canvas.SetTop(rect, obj.Position.Y);
            wallCanvas.Children.Add(rect);
            var label = new TextBlock { Text = obj.GetDisplayName(), FontSize = 10, Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), Padding = new Thickness(2) };
            Canvas.SetLeft(label, obj.Position.X + actualWidth / 2 - 20);
            Canvas.SetTop(label, obj.Position.Y + actualLength / 2 - 8);
            labelCanvas.Children.Add(label);
            if (obj.Layers > 1)
            {
                var layersText = new TextBlock { Text = $"{obj.Layers}층", FontSize = 9, Foreground = Brushes.White, Background = Brushes.Black, Padding = new Thickness(2) };
                Canvas.SetLeft(layersText, obj.Position.X + 2);
                Canvas.SetTop(layersText, obj.Position.Y + 2);
                labelCanvas.Children.Add(layersText);
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
            var wallCanvas = this.FindName("WallCanvas") as Canvas;
            var labelCanvas = this.FindName("LabelCanvas") as Canvas;
            if (wallCanvas == null || labelCanvas == null) return;

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
            wallCanvas.Children.Add(wallPolygon);
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
            labelCanvas.Children.Add(lengthLabel);
            if (isHorizontal)
            {
                var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                lengthLabel.RenderTransformOrigin = new Point(0.5, 0.5);
                lengthLabel.RenderTransform = new RotateTransform(angle > 90 || angle < -90 ? angle + 180 : angle);
            }
        }
        private void UpdateObjectHover(Point2D position)
        {
            var tempCanvas = this.FindName("TempCanvas") as Canvas;
            if (tempCanvas == null || _viewModel?.DrawingService == null) return;

            var obj = _viewModel.DrawingService.GetObjectAt(position);
            if (obj != _hoveredObject)
            {
                if (_hoverHighlight != null) { tempCanvas.Children.Remove(_hoverHighlight); _hoverHighlight = null; }
                _hoveredObject = obj;
                if (_hoveredObject != null && _hoveredObject != _selectedObject)
                {
                    var (min, max) = _hoveredObject.GetBoundingBox();
                    _hoverHighlight = new Rectangle { Width = max.X - min.X + 6, Height = max.Y - min.Y + 6, Fill = Brushes.Transparent, Stroke = Brushes.Blue, StrokeThickness = 2, Opacity = 0.5 };
                    Canvas.SetLeft(_hoverHighlight, min.X - 3);
                    Canvas.SetTop(_hoverHighlight, min.Y - 3);
                    tempCanvas.Children.Add(_hoverHighlight);
                }
            }
        }
    }
}