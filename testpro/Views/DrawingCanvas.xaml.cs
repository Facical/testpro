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
using System.IO;

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
        private Rectangle _hoverHighlight; // 호버 하이라이트 Rectangle
        private bool _isDraggingObject = false;
        private Point2D _dragOffset; // Point2D로 변경 (오류방지)

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

            // 호버 하이라이트 초기화 및 캔버스에 추가
            _hoverHighlight = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)),
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed, // 초기에는 숨김
                IsHitTestVisible = false // 마우스 이벤트는 받지 않음
            };
            DesignCanvas.Children.Add(_hoverHighlight); // DesignCanvas에 추가

            // 그리드 설정
            // DrawGrid(); // 초기 SetupCanvas에서는 주석 처리
        }

        private void SetupEventHandlers()
        {
            DesignCanvas.MouseLeftButtonDown += DesignCanvas_MouseLeftButtonDown;
            DesignCanvas.MouseMove += DesignCanvas_MouseMove;
            DesignCanvas.MouseLeftButtonUp += DesignCanvas_MouseLeftButtonUp;
            DesignCanvas.MouseRightButtonDown += DesignCanvas_MouseRightButtonDown;
            DesignCanvas.MouseWheel += DesignCanvas_MouseWheel;
            DesignCanvas.MouseLeave += DesignCanvas_MouseLeave; // 캔버스 전체의 MouseLeave
        }

        private void DrawGrid()
        {
            GridCanvas.Children.Clear();
            var gridBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230));

            // 캔버스의 현재 크기를 기준으로 그리드를 그립니다.
            // Canvas.Width와 Canvas.Height는 ApplyZoom 또는 LoadFloorPlan에서 설정됩니다.
            for (double x = 0; x < DesignCanvas.Width; x += GridSize * _zoomFactor) // 줌 팩터 고려
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = DesignCanvas.Height,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5 / _zoomFactor // 줌인 시 두께 유지
                };
                GridCanvas.Children.Add(line);
            }

            for (double y = 0; y < DesignCanvas.Height; y += GridSize * _zoomFactor) // 줌 팩터 고려
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = DesignCanvas.Width,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5 / _zoomFactor // 줌인 시 두께 유지
                };
                GridCanvas.Children.Add(line);
            }
        }

        // 객체 그리기
        private void DrawStoreObject(StoreObject obj)
        {
            var container = new Grid
            {
                // 객체 크기를 캔버스 스케일에 맞춰 조정
                Width = (obj.IsHorizontal ? obj.Width : obj.Length) * _zoomFactor,
                Height = (obj.IsHorizontal ? obj.Length : obj.Width) * _zoomFactor,
                Tag = obj,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            // 배경 사각형
            var rect = new Rectangle
            {
                Fill = obj.Fill,
                Stroke = obj.IsSelected ? Brushes.Orange : obj.Stroke,
                StrokeThickness = obj.IsSelected ? 3 / _zoomFactor : 1 / _zoomFactor // 줌인 시 두께 유지
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
                    FontSize = 24 / _zoomFactor, // 줌인 시 크기 유지
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
                    FontSize = 10 / _zoomFactor, // 줌인 시 크기 유지
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 5 / _zoomFactor), // 줌인 시 마진 유지
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
                    FontSize = 10 / _zoomFactor, // 줌인 시 크기 유지
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 5 / _zoomFactor, 5 / _zoomFactor, 0), // 줌인 시 마진 유지
                    Foreground = Brushes.Black,
                    Background = Brushes.White,
                    Padding = new Thickness(2 / _zoomFactor) // 줌인 시 패딩 유지
                };
                container.Children.Add(layerLabel);
            }

            // 회전 적용
            if (obj.Rotation != 0)
            {
                container.RenderTransform = new RotateTransform(obj.Rotation);
            }

            // 캔버스에 배치
            Canvas.SetLeft(container, obj.Position.X * _zoomFactor);
            Canvas.SetTop(container, obj.Position.Y * _zoomFactor);
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

            RedrawAll(); // 선택 상태 변경 시 전체 다시 그리기
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

            // 호버 하이라이트 숨김 (객체 다시 그릴 때 기존 하이라이트는 무의미)
            _hoverHighlight.Visibility = Visibility.Collapsed;
        }

        private void RedrawWalls()
        {
            // 기존 벽 제거
            var wallsToRemove = DesignCanvas.Children.OfType<Line>()
                .Where(l => l.Tag?.ToString() == "Wall").ToList();

            foreach (var wall in wallsToRemove)
            {
                DesignCanvas.Children.Remove(wall);
            }

            // ViewModel의 벽 다시 그리기
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
                X1 = wall.Start.X * _zoomFactor, // 줌 팩터 적용
                Y1 = wall.Start.Y * _zoomFactor, // 줌 팩터 적용
                X2 = wall.End.X * _zoomFactor,   // 줌 팩터 적용
                Y2 = wall.End.Y * _zoomFactor,   // 줌 팩터 적용
                Stroke = Brushes.Black,
                StrokeThickness = 3 / _zoomFactor, // 줌인 시 두께 유지
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
            var snappedPos = SnapToGrid(position); // 캔버스 좌표계에서 스냅

            if (_viewModel.CurrentTool == "PlaceObject" && _viewModel.SelectedObjectType != null)
            {
                // 새 객체 배치: 저장될 객체 위치는 실제 좌표계 (픽셀 / 줌 팩터)
                var newObject = new StoreObject(
                    _viewModel.SelectedObjectType.Value,
                    new Point2D(snappedPos.X / _zoomFactor, snappedPos.Y / _zoomFactor));

                _viewModel.StoreObjects.Add(newObject);
                SelectObject(newObject);
            }
            else if (_viewModel.CurrentTool == "WallStraight")
            {
                _isDrawingWall = true;
                // 벽 시작점도 실제 좌표계로 저장
                _tempStartPoint = new Point2D(snappedPos.X / _zoomFactor, snappedPos.Y / _zoomFactor);

                _previewWall = new Rectangle
                {
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 3 / _zoomFactor, // 줌인 시 두께 유지
                    StrokeDashArray = new DoubleCollection { 5 / _zoomFactor, 5 / _zoomFactor }, // 줌인 시 대시 패턴 유지
                    Fill = Brushes.Transparent
                };
                // 미리보기 벽은 캔버스 좌표계에 직접 그려져야 함
                Canvas.SetLeft(_previewWall, snappedPos.X);
                Canvas.SetTop(_previewWall, snappedPos.Y);
                DesignCanvas.Children.Add(_previewWall);
            }
            else
            {
                // 선택 모드: 클릭 위치에 객체가 있는지 확인
                var clickedObjects = DesignCanvas.Children.OfType<Grid>()
                    .Where(g => g.Tag is StoreObject) // 모든 StoreObject 태그된 Grid 확인
                    .Where(g =>
                    {
                        // 캔버스 좌표계에서 Grid의 경계 확인
                        double left = Canvas.GetLeft(g);
                        double top = Canvas.GetTop(g);
                        double width = g.ActualWidth; // RenderTransform이 적용된 후의 실제 렌더링 크기
                        double height = g.ActualHeight;
                        Rect bounds = new Rect(left, top, width, height);

                        return bounds.Contains(position); // 클릭 위치가 Grid 내부에 있는지 확인
                    })
                    .Select(g => g.Tag as StoreObject)
                    .ToList();

                if (clickedObjects.Any())
                {
                    // 가장 위에 있는 (가장 나중에 추가된) 객체 선택
                    // ZIndex는 중요하지만, 여기서는 목록의 마지막이 가장 위로 간주
                    SelectObject(clickedObjects.Last());
                    var obj = _selectedObject; // 선택된 객체 가져오기

                    // 드래그 시작 시 오프셋 계산 (캔버스 좌표계)
                    _dragOffset = new Point2D(
                        position.X - (obj.Position.X * _zoomFactor),
                        position.Y - (obj.Position.Y * _zoomFactor));
                    _isDraggingObject = true;
                }
                else
                {
                    SelectObject(null); // 아무 객체도 선택되지 않음
                }
            }

            e.Handled = true;
        }

        private void DesignCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(DesignCanvas); // 현재 마우스 캔버스 좌표
            var snappedPos = SnapToGrid(position); // 캔버스 좌표계에서 스냅

            // 호버 하이라이트 업데이트 (선택 모드일 때만, 드래그 중 아닐 때)
            if (!_isDraggingObject && _viewModel?.CurrentTool == "Select")
            {
                // 현재 마우스 위치에 있는 객체 찾기
                var currentHoveredObjects = DesignCanvas.Children.OfType<Grid>()
                    .Where(g => g.Tag is StoreObject)
                    .Where(g =>
                    {
                        // 캔버스 좌표계에서 Grid의 경계 확인
                        double left = Canvas.GetLeft(g);
                        double top = Canvas.GetTop(g);
                        double width = g.ActualWidth;
                        double height = g.ActualHeight;
                        Rect bounds = new Rect(left, top, width, height);
                        return bounds.Contains(position);
                    })
                    .Select(g => g.Tag as StoreObject)
                    .ToList();

                StoreObject newHoveredObject = currentHoveredObjects.LastOrDefault(); // 가장 위에 있는 객체

                // 호버 상태 변화 감지
                if (newHoveredObject != _hoveredObject)
                {
                    _hoveredObject = newHoveredObject;
                    if (_hoveredObject != null && _hoveredObject != _selectedObject) // 선택되지 않은 객체만 호버 하이라이트
                    {
                        Mouse.OverrideCursor = Cursors.Hand;
                        // 호버 하이라이트 Rect의 위치와 크기 설정 (캔버스 좌표계, 줌 팩터 적용)
                        _hoverHighlight.Width = (_hoveredObject.IsHorizontal ? _hoveredObject.Width : _hoveredObject.Length) * _zoomFactor;
                        _hoverHighlight.Height = (_hoveredObject.IsHorizontal ? _hoveredObject.Length : _hoveredObject.Width) * _zoomFactor;
                        Canvas.SetLeft(_hoverHighlight, _hoveredObject.Position.X * _zoomFactor);
                        Canvas.SetTop(_hoverHighlight, _hoveredObject.Position.Y * _zoomFactor);
                        _hoverHighlight.Visibility = Visibility.Visible;
                        Canvas.SetZIndex(_hoverHighlight, 2); // 객체 위에 표시되도록 ZIndex 설정
                    }
                    else
                    {
                        Mouse.OverrideCursor = null;
                        _hoverHighlight.Visibility = Visibility.Collapsed;
                    }
                }
                else if (_hoveredObject == _selectedObject) // 현재 호버된 객체가 이미 선택된 객체라면 하이라이트 숨김
                {
                    _hoverHighlight.Visibility = Visibility.Collapsed;
                }
            }
            else if (_isDraggingObject && _selectedObject != null)
            {
                // 드래그 중인 객체의 위치는 줌 팩터가 적용된 캔버스 좌표계 기준으로 이동
                // 실제 모델의 위치는 역변환하여 반영
                _selectedObject.Position = new Point2D(
                    (position.X - _dragOffset.X) / _zoomFactor,
                    (position.Y - _dragOffset.Y) / _zoomFactor);
                RedrawAll(); // 객체 위치 업데이트 후 다시 그리기
            }
            else if (_isDrawingWall && _previewWall != null)
            {
                UpdateWallPreview(snappedPos); // 미리보기 벽 업데이트 (스냅된 캔버스 좌표 사용)
            }


            // 좌표 표시
            if (_viewModel != null)
            {
                // 스냅된 캔버스 좌표를 실제 좌표계로 역변환하여 표시
                _viewModel.StatusText = $"X: {snappedPos.X / _zoomFactor:F0}, Y: {snappedPos.Y / _zoomFactor:F0}";
            }
        }

        private void DesignCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawingWall && _previewWall != null)
            {
                var position = e.GetPosition(DesignCanvas);
                var snappedPos = SnapToGrid(position);
                var endPoint = new Point2D(snappedPos.X / _zoomFactor, snappedPos.Y / _zoomFactor); // 실제 좌표계로 역변환

                // 벽 그리기 제한 (직선 벽만 허용)
                Point2D actualStart = _tempStartPoint; // 이미 실제 좌표계

                // X 또는 Y 축에 스냅 (Shift 키와 유사한 기능)
                if (Math.Abs(endPoint.X - actualStart.X) < Math.Abs(endPoint.Y - actualStart.Y))
                {
                    // 세로 선으로 간주
                    endPoint = new Point2D(actualStart.X, endPoint.Y);
                }
                else
                {
                    // 가로 선으로 간주
                    endPoint = new Point2D(endPoint.X, actualStart.Y);
                }


                if (Point2D.Distance(actualStart, endPoint) > GridSize / _zoomFactor / 2) // 최소 길이 이상일 때만 벽 생성
                {
                    var wall = new Wall(actualStart, endPoint);
                    _viewModel.Walls.Add(wall);
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
            RedrawAll(); // 모든 변경사항 반영
        }

        private void DesignCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.CurrentTool == "WallStraight")
            {
                // 벽 그리기 취소
                if (_isDrawingWall && _previewWall != null)
                {
                    DesignCanvas.Children.Remove(_previewWall);
                    _previewWall = null;
                    _isDrawingWall = false;
                }
                _viewModel.CurrentTool = "Select"; // 도구 선택으로 변경
            }
            // 그 외 모드에서는 컨텍스트 메뉴 등을 표시
            // StoreObject_MouseRightButtonDown에서 객체 컨텍스트 메뉴 처리
        }

        private void DesignCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                _zoomFactor = Math.Min(_zoomFactor * 1.1, 3.0); // 줌 인
            }
            else
            {
                _zoomFactor = Math.Max(_zoomFactor / 1.1, 0.5); // 줌 아웃
            }
            ApplyZoom();
            e.Handled = true;
        }

        private void DesignCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _hoveredObject = null;
            Mouse.OverrideCursor = null;
            _hoverHighlight.Visibility = Visibility.Collapsed; // 캔버스 벗어나면 하이라이트 숨김
        }

        // 객체 이벤트 핸들러 (개별 객체의 MouseEnter/Leave/MouseDown)
        private void StoreObject_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid container && container.Tag is StoreObject obj)
            {
                SelectObject(obj); // 객체 선택

                var position = e.GetPosition(DesignCanvas); // 캔버스 좌표
                // 드래그 오프셋 계산 (캔버스 좌표계)
                _dragOffset = new Point2D(
                    position.X - (obj.Position.X * _zoomFactor),
                    position.Y - (obj.Position.Y * _zoomFactor));
                _isDraggingObject = true;

                e.Handled = true; // 이 이벤트를 처리했음을 알림 (DesignCanvas_MouseLeftButtonDown으로 전파 방지)
            }
        }

        private void StoreObject_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid container && container.Tag is StoreObject obj)
            {
                SelectObject(obj);
                var position = e.GetPosition(DesignCanvas);
                ShowObjectContextMenu(obj, position);
                e.Handled = true; // 이 이벤트를 처리했음을 알림
            }
        }

        private void StoreObject_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Grid container && container.Tag is StoreObject obj)
            {
                // 이미 선택된 객체는 하이라이트하지 않음 (선택 하이라이트가 있으므로)
                if (obj != _selectedObject)
                {
                    _hoveredObject = obj; // 현재 호버된 객체 저장
                    Mouse.OverrideCursor = Cursors.Hand;

                    // 호버 하이라이트 Rect의 위치와 크기 설정 (캔버스 좌표계, 줌 팩터 적용)
                    _hoverHighlight.Width = (obj.IsHorizontal ? obj.Width : obj.Length) * _zoomFactor;
                    _hoverHighlight.Height = (obj.IsHorizontal ? obj.Length : obj.Width) * _zoomFactor;
                    Canvas.SetLeft(_hoverHighlight, obj.Position.X * _zoomFactor);
                    Canvas.SetTop(_hoverHighlight, obj.Position.Y * _zoomFactor);
                    _hoverHighlight.Visibility = Visibility.Visible;
                    Canvas.SetZIndex(_hoverHighlight, 2); // 객체 위에 표시되도록 ZIndex 설정
                }
            }
        }

        private void StoreObject_MouseLeave(object sender, MouseEventArgs e)
        {
            // 이 MouseLeave 이벤트는 개별 객체에서 발생합니다.
            // MouseMove에서 새로운 객체 위에 올라탔는지 확인하고 _hoverHighlight를 제어하는 것이 더 정확합니다.
            // 여기서는 단순히 하이라이트를 숨깁니다.
            _hoverHighlight.Visibility = Visibility.Collapsed;
            _hoveredObject = null; // 호버 상태 해제
            Mouse.OverrideCursor = null;
        }

        // 유틸리티 메서드
        private Point SnapToGrid(Point point)
        {
            // 스냅은 줌이 적용된 캔버스 좌표계에서 수행
            return new Point(
                Math.Round(point.X / (GridSize * _zoomFactor)) * (GridSize * _zoomFactor),
                Math.Round(point.Y / (GridSize * _zoomFactor)) * (GridSize * _zoomFactor));
        }

        private void UpdateWallPreview(Point currentPos)
        {
            // 미리보기 벽의 시작점은 이미 실제 좌표계로 _tempStartPoint에 저장되어 있습니다.
            // _previewWall의 Left/Top/Width/Height는 캔버스 좌표계에서 설정됩니다.

            double startXCanvas = _tempStartPoint.X * _zoomFactor;
            double startYCanvas = _tempStartPoint.Y * _zoomFactor;

            // 현재 마우스 위치(currentPos)는 캔버스 좌표계입니다.
            double currentXCanvas = currentPos.X;
            double currentYCanvas = currentPos.Y;

            // 가로 또는 세로로만 그릴 수 있도록 제한
            if (Math.Abs(currentXCanvas - startXCanvas) < Math.Abs(currentYCanvas - startYCanvas))
            {
                // 세로 선
                Canvas.SetLeft(_previewWall, startXCanvas - (3 / _zoomFactor) / 2); // 중심 맞추기
                Canvas.SetTop(_previewWall, Math.Min(startYCanvas, currentYCanvas));
                _previewWall.Width = 3 / _zoomFactor; // 벽 두께 고정 (줌에 따라 시각적 두께 유지)
                _previewWall.Height = Math.Abs(currentYCanvas - startYCanvas);
            }
            else
            {
                // 가로 선
                Canvas.SetLeft(_previewWall, Math.Min(startXCanvas, currentXCanvas));
                Canvas.SetTop(_previewWall, startYCanvas - (3 / _zoomFactor) / 2); // 중심 맞추기
                _previewWall.Width = Math.Abs(currentXCanvas - startXCanvas);
                _previewWall.Height = 3 / _zoomFactor; // 벽 두께 고정 (줌에 따라 시각적 두께 유지)
            }
        }


        // ViewModel 이벤트 핸들러
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentTool")
            {
                UpdateCursor();
                // 도구 변경 시 하이라이트 초기화
                _hoverHighlight.Visibility = Visibility.Collapsed;
                _isDrawingWall = false; // 도구 변경 시 벽 그리기 상태 해제
                if (_previewWall != null)
                {
                    DesignCanvas.Children.Remove(_previewWall);
                    _previewWall = null;
                }
            }
            else if (e.PropertyName == "SelectedObject")
            {
                RedrawAll(); // 선택 객체 변경 시 전체 다시 그리기
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
                Stretch = Stretch.None, // 이미지를 늘리지 않고 원본 크기 유지
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Opacity = 0.8
            };

            Canvas.SetZIndex(_backgroundImage, -1);
            DesignCanvas.Children.Insert(0, _backgroundImage);

            // 이미지 로드 후 캔버스 크기를 이미지 크기에 맞게 조정
            // 여기서 초기 줌 팩터를 고려하여 Canvas 크기를 설정합니다.
            DesignCanvas.Width = image.PixelWidth; // Canvas는 원래 픽셀 크기
            DesignCanvas.Height = image.PixelHeight;
            GridCanvas.Width = image.PixelWidth;
            GridCanvas.Height = image.PixelHeight;

            ApplyZoom(); // 로드 후 초기 줌 팩터 적용 및 그리드/객체 다시 그리기
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

            // 배경 이미지 제거 시 캔버스 크기를 기본값으로 되돌리거나,
            // MainCanvas의 ActualWidth/Height를 사용하도록 할 수 있습니다.
            // 여기서는 임시로 기본 크기를 설정합니다.
            DesignCanvas.Width = 1200; // 적절한 기본값 설정
            DesignCanvas.Height = 800; // 적절한 기본값 설정
            GridCanvas.Width = 1200;
            GridCanvas.Height = 800;

            ApplyZoom(); // 그리드와 객체 다시 그리기 (기본 크기로)
        }

        // 객체 감지
        public void DetectObjectsInFloorPlan()
        {
            _detectedObjects.Clear();
            _detectedObjectsCanvas.Children.Clear();

            if (_loadedFloorPlan == null)
            {
                MessageBox.Show("도면 이미지를 먼저 로드해주세요.", "객체 감지 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // BitmapImage를 System.Drawing.Bitmap으로 변환
            System.Drawing.Bitmap bitmap;
            try
            {
                using (MemoryStream outStream = new MemoryStream())
                {
                    BitmapEncoder enc = new BmpBitmapEncoder(); // PNG나 BMP 인코더 사용
                    enc.Frames.Add(BitmapFrame.Create(_loadedFloorPlan));
                    enc.Save(outStream);
                    bitmap = new System.Drawing.Bitmap(outStream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 변환 중 오류 발생: {ex.Message}", "객체 감지 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            // FloorPlanAnalyzer 인스턴스 생성
            var analyzer = new FloorPlanAnalyzer();

            // 도면 경계 찾기
            var bounds = analyzer.FindFloorPlanBounds(_loadedFloorPlan); // BitmapImage 사용
            if (bounds == null)
            {
                MessageBox.Show("도면 경계를 찾을 수 없습니다. 이미지 품질을 확인해주세요.", "객체 감지 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 객체 감지
            try
            {
                _detectedObjects = analyzer.DetectFloorPlanObjects(_loadedFloorPlan, bounds); // BitmapImage 사용
            }
            catch (Exception ex)
            {
                MessageBox.Show($"객체 감지 로직 실행 중 오류 발생: {ex.Message}", "객체 감지 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"객체 감지 로직 오류: {ex.Message}");
                return;
            }

            // 감지된 객체 그리기
            foreach (var detObj in _detectedObjects)
            {
                DrawDetectedObject(detObj);
            }

            var count = _detectedObjects.Count;
            // DetectedObjectsCountText 업데이트는 MainWindow에서 수행되므로 여기서는 생략.
            // 하지만 MainViewModel을 통해 업데이트할 수는 있습니다.
            _viewModel.StatusText = $"{count}개의 객체가 감지되었습니다.";
        }

        private void DrawDetectedObject(DetectedObject detObj)
        {
            var rect = new Rectangle
            {
                Width = detObj.Bounds.Width * _zoomFactor,
                Height = detObj.Bounds.Height * _zoomFactor,
                Stroke = Brushes.Blue,
                StrokeThickness = 2 / _zoomFactor, // 줌인 시 두께 유지
                StrokeDashArray = new DoubleCollection { 5 / _zoomFactor, 5 / _zoomFactor }, // 줌인 시 대시 패턴 유지
                Fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)),
                Tag = detObj,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(rect, detObj.Bounds.Left * _zoomFactor);
            Canvas.SetTop(rect, detObj.Bounds.Top * _zoomFactor);

            rect.MouseEnter += DetectedObject_MouseEnter;
            rect.MouseLeave += DetectedObject_MouseLeave;
            rect.MouseLeftButtonDown += DetectedObject_MouseLeftButtonDown;

            _detectedObjectsCanvas.Children.Add(rect);
        }

        private void DetectedObject_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is DetectedObject detObj)
            {
                rect.Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)); // 호버 색상 변경
                _hoveredDetectedObject = detObj;
            }
        }

        private void DetectedObject_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Rectangle rect)
            {
                rect.Fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)); // 원래 색상으로 복원
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

                // 감지된 객체의 크기를 초기값으로 대화상자에 전달
                // 픽셀을 피트(ft)로 변환하기 위한 임의의 스케일 (예: 100 픽셀 = 1피트)
                // 이 스케일은 사용자가 직접 입력받는 것이 정확합니다.
                // 현재는 예시 값으로 픽셀을 인치로 바로 전달합니다. (Blender 모델 기준 픽셀 = 인치 단위일 경우)
                dialog.ObjectWidth = detObj.Bounds.Width; // 픽셀 (인치로 가정)
                dialog.ObjectLength = detObj.Bounds.Height; // 픽셀 (2D에서의 Length, 인치로 가정)
                dialog.ObjectHeight = 72; // 기본 높이 6피트 (72인치)
                dialog.ObjectLayers = 3;
                dialog.IsHorizontal = true;


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

                    // StoreObject의 위치는 감지된 객체의 픽셀 좌표를 실제 좌표계로 저장
                    storeObj.Position = new Point2D(detObj.Bounds.Left, detObj.Bounds.Top);


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
            _zoomFactor = Math.Min(_zoomFactor * 1.1, 3.0); // 줌 팩터 조정 (원래는 1.2였으나 좀 더 부드럽게)
            ApplyZoom();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = Math.Max(_zoomFactor / 1.1, 0.5); // 줌 팩터 조정
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            var scaleTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
            // RenderTransform을 Canvas 전체에 적용하여 그리기 및 HitTest에 영향을 줍니다.
            DesignCanvas.RenderTransform = scaleTransform;
            GridCanvas.RenderTransform = scaleTransform;

            // Canvas의 Width와 Height는 이미지의 실제 픽셀 크기로 고정합니다.
            // 줌 스케일은 RenderTransform이 담당합니다.
            if (_loadedFloorPlan != null)
            {
                DesignCanvas.Width = _loadedFloorPlan.PixelWidth;
                DesignCanvas.Height = _loadedFloorPlan.PixelHeight;
                GridCanvas.Width = _loadedFloorPlan.PixelWidth;
                GridCanvas.Height = _loadedFloorPlan.PixelHeight;
            }
            else // 배경 이미지가 없을 경우를 대비하여 기본 크기 설정
            {
                DesignCanvas.Width = 1200;
                DesignCanvas.Height = 800;
                GridCanvas.Width = 1200;
                GridCanvas.Height = 800;
            }

            // 그리드 다시 그리기 (줌 팩터가 DrawGrid 내부에서 고려됨)
            DrawGrid();

            // 모든 기존 객체를 다시 그려 줌 팩터를 반영합니다.
            RedrawAll();

            // 줌 레벨 표시 업데이트
            if (ZoomLevelText != null)
            {
                ZoomLevelText.Text = $"{_zoomFactor * 100:F0}%";
            }
        }
    }
}