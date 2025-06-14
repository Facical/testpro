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
using System.Diagnostics;

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

        // 배경 이미지 관련
        private Image _backgroundImage;
        private BitmapImage _loadedFloorPlan;

        // 객체 배치 관련
        private bool _isDrawingObject = false;
        private Point2D _objectStartPoint;
        private Rectangle _objectPreview;
        private StoreObject _selectedObject;
        private StoreObject _hoveredObject;
        private Rectangle _hoverHighlight;
        private bool _isDraggingObject = false;
        private Point2D _dragOffset;

        // 객체 감지 관련
        private List<DetectedObject> _detectedObjects = new List<DetectedObject>();
        private Canvas _detectedObjectsCanvas;
        private DetectedObject _hoveredDetectedObject;

        private Stopwatch _inputStopwatch = new Stopwatch();
        private List<double> _inputDelayMeasurements = new List<double>();

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
                    _viewModel.StoreObjects.CollectionChanged += StoreObjects_CollectionChanged;
                    RedrawAll();
                }
            }
        }

        public DrawingCanvas()
        {
            InitializeComponent();
            SetupCanvas();
            SetupEventHandlers();
        }

        private void SetupCanvas()
        {
            // 감지된 객체 레이어
            _detectedObjectsCanvas = new Canvas
            {
                IsHitTestVisible = true,
                ClipToBounds = true
            };
            DesignCanvas.Children.Add(_detectedObjectsCanvas);

            // 호버 하이라이트
            _hoverHighlight = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)),
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            DesignCanvas.Children.Add(_hoverHighlight);

            // 그리드 설정
            DrawGrid();
        }

        private void SetupEventHandlers()
        {
            DesignCanvas.MouseLeftButtonDown += DesignCanvas_MouseLeftButtonDown;
            DesignCanvas.MouseMove += DesignCanvas_MouseMove;
            DesignCanvas.MouseLeftButtonUp += DesignCanvas_MouseLeftButtonUp;
            DesignCanvas.MouseRightButtonDown += DesignCanvas_MouseRightButtonDown;
            DesignCanvas.MouseWheel += DesignCanvas_MouseWheel;
            DesignCanvas.MouseLeave += DesignCanvas_MouseLeave;
        }

        private void DrawGrid()
        {
            GridCanvas.Children.Clear();
            var gridBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230));

            // 그리드 라인 그리기
            for (double x = 0; x < DesignCanvas.ActualWidth; x += GridSize)
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = DesignCanvas.ActualHeight,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                };
                GridCanvas.Children.Add(line);
            }

            for (double y = 0; y < DesignCanvas.ActualHeight; y += GridSize)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = DesignCanvas.ActualWidth,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                };
                GridCanvas.Children.Add(line);
            }
        }

        // 객체 그리기
        private void DrawStoreObject(StoreObject obj)
        {
            var container = new Grid
            {
                Width = obj.IsHorizontal ? obj.Width : obj.Length,
                Height = obj.IsHorizontal ? obj.Length : obj.Width,
                Tag = obj,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            // 배경 사각형
            var rect = new Rectangle
            {
                Fill = obj.Fill,
                Stroke = obj.IsSelected ? Brushes.Orange : obj.Stroke,
                StrokeThickness = obj.IsSelected ? 3 : 1
            };
            container.Children.Add(rect);

            // 타입별 아이콘 및 라벨
            var iconText = "";
            var showTemp = false;

            switch (obj.Type)
            {
                case ObjectType.Refrigerator:
                    iconText = "❄️";
                    showTemp = true;
                    break;
                case ObjectType.Freezer:
                    iconText = "🧊";
                    showTemp = true;
                    break;
                case ObjectType.Shelf:
                    iconText = "📦";
                    break;
                case ObjectType.Checkout:
                    iconText = "💳";
                    break;
                case ObjectType.DisplayStand:
                    iconText = "🏪";
                    break;
                case ObjectType.Pillar:
                    iconText = "🏛️";
                    break;
            }

            // 아이콘
            if (!string.IsNullOrEmpty(iconText))
            {
                var icon = new TextBlock
                {
                    Text = iconText,
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White
                };
                container.Children.Add(icon);
            }

            // 온도 표시 (냉장고/냉동고)
            if (showTemp)
            {
                var tempLabel = new TextBlock
                {
                    Text = $"{obj.Temperature}°C",
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 5),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold
                };
                container.Children.Add(tempLabel);
            }

            // 층수 표시 (선반류)
            if (obj.HasLayerSupport && obj.Layers > 1)
            {
                var layerLabel = new TextBlock
                {
                    Text = $"{obj.Layers}층",
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 5, 5, 0),
                    Foreground = Brushes.Black,
                    Background = Brushes.White,
                    Padding = new Thickness(2)
                };
                container.Children.Add(layerLabel);
            }

            // 회전 적용
            if (obj.Rotation != 0)
            {
                container.RenderTransform = new RotateTransform(obj.Rotation);
            }

            // 캔버스에 배치
            Canvas.SetLeft(container, obj.Position.X);
            Canvas.SetTop(container, obj.Position.Y);
            Canvas.SetZIndex(container, 1);

            // 이벤트 핸들러
            container.MouseLeftButtonDown += StoreObject_MouseLeftButtonDown;
            container.MouseRightButtonDown += StoreObject_MouseRightButtonDown;
            container.MouseEnter += StoreObject_MouseEnter;
            container.MouseLeave += StoreObject_MouseLeave;

            DesignCanvas.Children.Add(container);
        }

        // 객체 선택
        private void SelectObject(StoreObject obj)
        {
            if (_viewModel == null) return;

            // 기존 선택 해제
            foreach (var o in _viewModel.StoreObjects)
            {
                o.IsSelected = false;
            }

            // 새 객체 선택
            if (obj != null)
            {
                obj.IsSelected = true;
                _selectedObject = obj;
                _viewModel.SelectedObject = obj;
            }
            else
            {
                _selectedObject = null;
                _viewModel.SelectedObject = null;
            }

            RedrawAll();
        }

        // 전체 다시 그리기 (public으로 변경)
        public void RedrawAll()
        {
            // 객체 레이어만 지우기 (배경과 그리드는 유지)
            var objectsToRemove = DesignCanvas.Children.OfType<Grid>()
                .Where(g => g.Tag is StoreObject).ToList();

            foreach (var obj in objectsToRemove)
            {
                DesignCanvas.Children.Remove(obj);
            }

            // 벽 다시 그리기
            RedrawWalls();

            // 객체 다시 그리기
            if (_viewModel?.StoreObjects != null)
            {
                foreach (var obj in _viewModel.StoreObjects)
                {
                    DrawStoreObject(obj);
                }
            }
        }

        private void RedrawWalls()
        {
            var wallsToRemove = DesignCanvas.Children.OfType<Line>()
                .Where(l => l.Tag?.ToString() == "Wall").ToList();

            foreach (var wall in wallsToRemove)
            {
                DesignCanvas.Children.Remove(wall);
            }

            if (_viewModel?.Walls != null)
            {
                foreach (var wall in _viewModel.Walls)
                {
                    DrawWall(wall);
                }
            }
        }

        private void DrawWall(Wall wall)
        {
            var line = new Line
            {
                X1 = wall.Start.X,
                Y1 = wall.Start.Y,
                X2 = wall.End.X,
                Y2 = wall.End.Y,
                Stroke = Brushes.Black,
                StrokeThickness = 3,
                Tag = "Wall"
            };
            DesignCanvas.Children.Add(line);
            Canvas.SetZIndex(line, 0);
        }

        // 객체 속성 대화상자
        private void ShowPropertyDialog(StoreObject obj)
        {
            var dialog = new PropertyEditDialog(obj)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                obj.ModifiedAt = DateTime.Now;
                RedrawAll();
                _viewModel?.Update3DView();
            }
        }

        // 컨텍스트 메뉴
        private void ShowObjectContextMenu(StoreObject obj, Point position)
        {
            var contextMenu = new ContextMenu();

            var editItem = new MenuItem { Header = "속성 편집" };
            editItem.Click += (s, e) => ShowPropertyDialog(obj);
            contextMenu.Items.Add(editItem);

            var copyItem = new MenuItem { Header = "복사" };
            copyItem.Click += (s, e) => CopyObject(obj);
            contextMenu.Items.Add(copyItem);

            contextMenu.Items.Add(new Separator());

            var rotateItem = new MenuItem { Header = "90도 회전" };
            rotateItem.Click += (s, e) => RotateObject(obj, 90);
            contextMenu.Items.Add(rotateItem);

            var flipItem = new MenuItem { Header = "방향 전환" };
            flipItem.Click += (s, e) => FlipObject(obj);
            contextMenu.Items.Add(flipItem);

            contextMenu.Items.Add(new Separator());

            var deleteItem = new MenuItem { Header = "삭제" };
            deleteItem.Click += (s, e) => DeleteObject(obj);
            contextMenu.Items.Add(deleteItem);

            contextMenu.IsOpen = true;
        }

        private void CopyObject(StoreObject obj)
        {
            var clone = obj.Clone();
            _viewModel.StoreObjects.Add(clone);
            SelectObject(clone);
        }

        private void DeleteObject(StoreObject obj)
        {
            _viewModel.StoreObjects.Remove(obj);
            if (_selectedObject == obj)
            {
                SelectObject(null);
            }
        }

        private void RotateObject(StoreObject obj, double angle)
        {
            obj.Rotation = (obj.Rotation + angle) % 360;
            RedrawAll();
            _viewModel?.Update3DView();
        }

        private void FlipObject(StoreObject obj)
        {
            obj.IsHorizontal = !obj.IsHorizontal;
            RedrawAll();
            _viewModel?.Update3DView();
        }

        // 마우스 이벤트 핸들러들
        private void DesignCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _inputStopwatch.Restart();
            var position = e.GetPosition(DesignCanvas);
            var snappedPos = SnapToGrid(position);

            if (_viewModel.CurrentTool == "PlaceObject" && _viewModel.SelectedObjectType != null)
            {
                // 새 객체 배치
                var newObject = new StoreObject(
                    _viewModel.SelectedObjectType.Value,
                    new Point2D(snappedPos.X, snappedPos.Y));

                _viewModel.StoreObjects.Add(newObject);
                SelectObject(newObject);
            }
            else if (_viewModel.CurrentTool == "WallStraight")
            {
                _isDrawingWall = true;
                _tempStartPoint = new Point2D(snappedPos.X, snappedPos.Y);

                _previewWall = new Rectangle
                {
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 3,
                    StrokeDashArray = new DoubleCollection { 5, 5 },
                    Fill = Brushes.Transparent
                };
                DesignCanvas.Children.Add(_previewWall);
            }
            else
            {
                // 선택 모드
                SelectObject(null);
            }

            e.Handled = true;
        }

        private void DesignCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(DesignCanvas);
            var snappedPos = SnapToGrid(position);

            if (_isDrawingWall && _previewWall != null)
            {
                UpdateWallPreview(snappedPos);
            }
            else if (_isDraggingObject && _selectedObject != null)
            {
                _selectedObject.Position = new Point2D(
                    snappedPos.X - _dragOffset.X,
                    snappedPos.Y - _dragOffset.Y);
                RedrawAll();
            }

            // 좌표 표시
            if (_viewModel != null)
            {
                _viewModel.StatusText = $"X: {snappedPos.X:F0}, Y: {snappedPos.Y:F0}";
            }
        }

        private void DesignCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawingWall && _previewWall != null)
            {
                var position = e.GetPosition(DesignCanvas);
                var snappedPos = SnapToGrid(position);
                var endPoint = new Point2D(snappedPos.X, snappedPos.Y);

                if (Point2D.Distance(_tempStartPoint, endPoint) > GridSize)
                {
                    var wall = new Wall(_tempStartPoint, endPoint);
                    _viewModel.Walls.Add(wall);
                    DrawWall(wall);
                }

                DesignCanvas.Children.Remove(_previewWall);
                _previewWall = null;
                _isDrawingWall = false;
            }

            _isDraggingObject = false;

            // 입력 지연 측정
            if (_inputStopwatch.IsRunning)
            {
                _inputStopwatch.Stop();
                _inputDelayMeasurements.Add(_inputStopwatch.ElapsedMilliseconds);

                if (_inputDelayMeasurements.Count >= 10)
                {
                    var avgDelay = _inputDelayMeasurements.Average();
                    System.Diagnostics.Debug.WriteLine($"평균 입력 지연: {avgDelay:F1}ms");
                    _inputDelayMeasurements.Clear();
                }
            }
        }

        private void DesignCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.CurrentTool == "WallStraight")
            {
                _viewModel.CurrentTool = "Select";
            }
        }

        private void DesignCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 줌 기능 (추후 구현)
        }

        private void DesignCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _hoverHighlight.Visibility = Visibility.Collapsed;
        }

        // 객체 이벤트 핸들러
        private void StoreObject_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid container && container.Tag is StoreObject obj)
            {
                SelectObject(obj);

                var position = e.GetPosition(DesignCanvas);
                _dragOffset = new Point2D(
                    position.X - obj.Position.X,
                    position.Y - obj.Position.Y);
                _isDraggingObject = true;

                e.Handled = true;
            }
        }

        private void StoreObject_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid container && container.Tag is StoreObject obj)
            {
                SelectObject(obj);
                var position = e.GetPosition(DesignCanvas);
                ShowObjectContextMenu(obj, position);
                e.Handled = true;
            }
        }

        private void StoreObject_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Grid container && container.Tag is StoreObject obj)
            {
                _hoveredObject = obj;
                Mouse.OverrideCursor = Cursors.Hand;
            }
        }

        private void StoreObject_MouseLeave(object sender, MouseEventArgs e)
        {
            _hoveredObject = null;
            Mouse.OverrideCursor = null;
        }

        // 유틸리티 메서드
        private Point SnapToGrid(Point point)
        {
            return new Point(
                Math.Round(point.X / GridSize) * GridSize,
                Math.Round(point.Y / GridSize) * GridSize);
        }

        private void UpdateWallPreview(Point currentPos)
        {
            var width = Math.Abs(currentPos.X - _tempStartPoint.X);
            var height = Math.Abs(currentPos.Y - _tempStartPoint.Y);

            Canvas.SetLeft(_previewWall, Math.Min(_tempStartPoint.X, currentPos.X));
            Canvas.SetTop(_previewWall, Math.Min(_tempStartPoint.Y, currentPos.Y));

            _previewWall.Width = Math.Max(width, 1);
            _previewWall.Height = Math.Max(height, 1);
        }

        // ViewModel 이벤트 핸들러
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentTool")
            {
                UpdateCursor();
            }
        }

        private void StoreObjects_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RedrawAll();
        }

        private void UpdateCursor()
        {
            if (_viewModel == null) return;

            switch (_viewModel.CurrentTool)
            {
                case "WallStraight":
                    Mouse.OverrideCursor = Cursors.Cross;
                    break;
                case "PlaceObject":
                    Mouse.OverrideCursor = Cursors.Cross;
                    break;
                default:
                    Mouse.OverrideCursor = null;
                    break;
            }
        }

        // 배경 이미지 관련
        public void LoadFloorPlan(BitmapImage image)
        {
            _loadedFloorPlan = image;

            if (_backgroundImage != null)
            {
                DesignCanvas.Children.Remove(_backgroundImage);
            }

            _backgroundImage = new Image
            {
                Source = image,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Opacity = 0.8
            };

            Canvas.SetZIndex(_backgroundImage, -1);
            DesignCanvas.Children.Insert(0, _backgroundImage);
        }

        // 배경 이미지 설정 메서드
        public void SetBackgroundImage(BitmapImage image)
        {
            LoadFloorPlan(image);
        }

        // 배경 이미지 제거 메서드
        public void ClearBackgroundImage()
        {
            if (_backgroundImage != null)
            {
                DesignCanvas.Children.Remove(_backgroundImage);
                _backgroundImage = null;
            }
            _loadedFloorPlan = null;
        }

        // 객체 감지 (간단한 구현)
        public void DetectObjectsInFloorPlan()
        {
            // 실제로는 OpenCV 등을 사용하여 구현
            // 여기서는 테스트용 더미 객체 생성
            _detectedObjects.Clear();
            _detectedObjectsCanvas.Children.Clear();

            // 테스트용 감지된 객체
            var testObject = new DetectedObject
            {
                Type = DetectedObjectType.Refrigerator,
                Bounds = new Rect(100, 100, 48, 36),
                Confidence = 0.95
            };

            _detectedObjects.Add(testObject);
            DrawDetectedObject(testObject);
        }

        private void DrawDetectedObject(DetectedObject detObj)
        {
            var rect = new Rectangle
            {
                Width = detObj.Bounds.Width,
                Height = detObj.Bounds.Height,
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 5 },
                Fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)),
                Tag = detObj,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(rect, detObj.Bounds.Left);
            Canvas.SetTop(rect, detObj.Bounds.Top);

            rect.MouseEnter += DetectedObject_MouseEnter;
            rect.MouseLeave += DetectedObject_MouseLeave;
            rect.MouseLeftButtonDown += DetectedObject_MouseLeftButtonDown;

            _detectedObjectsCanvas.Children.Add(rect);
        }

        private void DetectedObject_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is DetectedObject detObj)
            {
                rect.Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215));
                _hoveredDetectedObject = detObj;
            }
        }

        private void DetectedObject_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Rectangle rect)
            {
                rect.Fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
                _hoveredDetectedObject = null;
            }
        }

        private void DetectedObject_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is DetectedObject detObj)
            {
                var dialog = new ObjectTypeSelectionDialog
                {
                    Owner = Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    var storeObj = detObj.ToStoreObjectWithProperties(
                        dialog.ObjectWidth,
                        dialog.ObjectHeight,
                        dialog.ObjectLength,
                        dialog.ObjectLayers,
                        dialog.IsHorizontal,
                        dialog.Temperature,
                        dialog.CategoryCode);

                    _viewModel.StoreObjects.Add(storeObj);

                    // 감지된 객체 제거
                    _detectedObjectsCanvas.Children.Remove(rect);
                    _detectedObjects.Remove(detObj);

                    e.Handled = true;
                }
            }
        }

        public int GetDetectedObjectsCount()
        {
            return _detectedObjects.Count;
        }

        // 줌 관련 메서드
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = Math.Min(_zoomFactor * 1.2, 3.0);
            ApplyZoom();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = Math.Max(_zoomFactor / 1.2, 0.5);
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            var scaleTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
            DesignCanvas.RenderTransform = scaleTransform;
            GridCanvas.RenderTransform = scaleTransform;

            // 캔버스 크기 조정
            DesignCanvas.Width = 1200 * _zoomFactor;
            DesignCanvas.Height = 800 * _zoomFactor;
            GridCanvas.Width = 1200 * _zoomFactor;
            GridCanvas.Height = 800 * _zoomFactor;

            // 줌 레벨 표시 업데이트
            if (ZoomLevelText != null)
            {
                ZoomLevelText.Text = $"{_zoomFactor * 100:F0}%";
            }

            // 그리드 다시 그리기
            DrawGrid();
        }
    }
}