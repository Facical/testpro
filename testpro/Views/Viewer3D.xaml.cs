using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using testpro.Models;
using testpro.ViewModels;
using HelixToolkit.Wpf;

namespace testpro.Views
{
    public partial class Viewer3D : UserControl
    {
        private MainViewModel _viewModel;

        // RFP에 맞춘 벽 높이와 두께 설정 (더 자연스럽게 조정)
        private readonly double WallHeight = 96.0; // 벽 높이 (8ft = 96인치)
        private readonly double WallThicknessMultiplier = 1.2; // 벽 두께 배율 (자연스럽게 줄임)

        // 마우스 상호작용을 위한 변수들
        private bool _isRotating = false;
        private bool _isPanning = false;
        private Point _lastMousePos;
        private double _rotationX = 30;
        private double _rotationY = -45;
        private double _zoom = 150;
        private Point3D _lookAtPoint = new Point3D(0, 0, 0);

        // 3D 객체 컨테이너
        private ModelVisual3D _storeObjectsContainer;

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
                    UpdateAll3DModels();
                }
            }
        }

        public Viewer3D()
        {
            InitializeComponent();
            SetupViewport();
            CreateFloorGrid();
            UpdateCamera();

            // 매장 객체 컨테이너 초기화
            _storeObjectsContainer = new ModelVisual3D();
            MainViewport.Children.Add(_storeObjectsContainer);

            // 이벤트 구독
            Unloaded += OnViewerUnloaded;

            // 마우스 캡처를 위한 설정
            Focusable = true;
            Focus();
        }

        private void OnViewerUnloaded(object sender, RoutedEventArgs e)
        {
            // 리소스 정리
            if (_isRotating || _isPanning)
            {
                ReleaseMouseCapture();
            }
        }

        private void SetupViewport()
        {
            // 초기 카메라 설정
            UpdateCamera();
            UpdateCameraInfo();
        }

        private void CreateFloorGrid()
        {
            var gridModel = new Model3DGroup();

            // 무한처럼 보이는 넓은 그리드 생성
            var gridSize = 1000; // 매우 큰 그리드 (1000피트)
            var gridStep = 12;   // 1피트 간격
            var majorStep = 120; // 10피트 주요 라인

            // 그리드가 멀어질수록 희미해지도록 거리별로 생성
            CreateGridSection(gridModel, -100, 100, gridStep, majorStep, 1.0);   // 중앙 영역 (진하게)
            CreateGridSection(gridModel, -300, -100, gridStep * 2, majorStep, 0.5); // 중간 영역 (희미하게)
            CreateGridSection(gridModel, 100, 300, gridStep * 2, majorStep, 0.5);
            CreateGridSection(gridModel, -gridSize, -300, gridStep * 4, majorStep, 0.2); // 외곽 영역 (매우 희미하게)
            CreateGridSection(gridModel, 300, gridSize, gridStep * 4, majorStep, 0.2);

            // 좌표축 추가 (매트한 색상으로)
            var xAxis = CreateLine3D(new Point3D(0, 0, 0), new Point3D(10, 0, 0), Color.FromRgb(200, 100, 100), 0.1);
            if (xAxis != null) gridModel.Children.Add(xAxis);

            var yAxis = CreateLine3D(new Point3D(0, 0, 0), new Point3D(0, 10, 0), Color.FromRgb(100, 200, 100), 0.1);
            if (yAxis != null) gridModel.Children.Add(yAxis);

            var zAxis = CreateLine3D(new Point3D(0, 0, 0), new Point3D(0, 0, 10), Color.FromRgb(100, 100, 200), 0.1);
            if (zAxis != null) gridModel.Children.Add(zAxis);

            FloorGridVisual.Content = gridModel;
        }

        private void CreateGridSection(Model3DGroup gridModel, int start, int end, int step, int majorStep, double opacity)
        {
            var color = Color.FromArgb((byte)(255 * opacity), 128, 128, 128);
            var majorColor = Color.FromArgb((byte)(255 * opacity), 96, 96, 96);

            for (int i = start; i <= end; i += step)
            {
                if (Math.Abs(i) <= end)
                {
                    // 수직 라인
                    var verticalLine = CreateLine3D(
                        new Point3D(i / 12.0, start / 12.0, 0),
                        new Point3D(i / 12.0, end / 12.0, 0),
                        i % majorStep == 0 ? majorColor : color,
                        i % majorStep == 0 ? 0.02 : 0.01);
                    if (verticalLine != null) gridModel.Children.Add(verticalLine);

                    // 수평 라인
                    var horizontalLine = CreateLine3D(
                        new Point3D(start / 12.0, i / 12.0, 0),
                        new Point3D(end / 12.0, i / 12.0, 0),
                        i % majorStep == 0 ? majorColor : color,
                        i % majorStep == 0 ? 0.02 : 0.01);
                    if (horizontalLine != null) gridModel.Children.Add(horizontalLine);
                }
            }
        }

        private GeometryModel3D CreateLine3D(Point3D start, Point3D end, Color color, double thickness)
        {
            try
            {
                var mesh = new MeshGeometry3D();

                var direction = end - start;
                var length = direction.Length;
                if (length < 0.001) return null;

                // 간단한 라인 메시 생성 (박스 형태)
                var halfThickness = thickness / 2;

                mesh.Positions.Add(new Point3D(start.X - halfThickness, start.Y - halfThickness, start.Z));
                mesh.Positions.Add(new Point3D(start.X + halfThickness, start.Y - halfThickness, start.Z));
                mesh.Positions.Add(new Point3D(end.X + halfThickness, end.Y - halfThickness, end.Z));
                mesh.Positions.Add(new Point3D(end.X - halfThickness, end.Y - halfThickness, end.Z));

                mesh.Positions.Add(new Point3D(start.X - halfThickness, start.Y + halfThickness, start.Z));
                mesh.Positions.Add(new Point3D(start.X + halfThickness, start.Y + halfThickness, start.Z));
                mesh.Positions.Add(new Point3D(end.X + halfThickness, end.Y + halfThickness, end.Z));
                mesh.Positions.Add(new Point3D(end.X - halfThickness, end.Y + halfThickness, end.Z));

                // 삼각형 인덱스
                int[] indices = { 0, 1, 2, 2, 3, 0, 4, 6, 5, 4, 7, 6, 0, 3, 7, 0, 7, 4, 1, 5, 6, 1, 6, 2, 0, 4, 5, 0, 5, 1, 3, 2, 6, 3, 6, 7 };
                foreach (var idx in indices)
                    mesh.TriangleIndices.Add(idx);

                // 매트한 재질 (광택 없음)
                var material = new DiffuseMaterial(new SolidColorBrush(color));
                return new GeometryModel3D(mesh, material);
            }
            catch
            {
                return null;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.DrawingService))
            {
                UpdateAll3DModels();
            }
        }

        public void UpdateAll3DModels()
        {
            if (_viewModel?.DrawingService == null) return;

            try
            {
                ClearAll3DModels();
                Create3DWalls();
                Create3DFloors();
                Create3DStoreObjects();
                UpdatePerformanceInfo();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"3D 모델 업데이트 오류: {ex.Message}");
            }
        }

        private void ClearAll3DModels()
        {
            WallsContainer.Children.Clear();
            FloorsContainer.Children.Clear();
            _storeObjectsContainer.Children.Clear();
        }

        private void Create3DWalls()
        {
            var wallsModel = new Model3DGroup();

            foreach (var wall in _viewModel.DrawingService.Walls)
            {
                var wall3D = CreateWall3D(wall);
                if (wall3D != null)
                {
                    wallsModel.Children.Add(wall3D);
                }
            }

            WallsContainer.Content = wallsModel;
        }

        private void Create3DFloors()
        {
            var floorsModel = new Model3DGroup();

            foreach (var room in _viewModel.DrawingService.Rooms)
            {
                if (room.IsClosedRoom())
                {
                    var floor3D = CreateFloor3D(room);
                    if (floor3D != null)
                    {
                        floorsModel.Children.Add(floor3D);
                    }
                }
            }

            FloorsContainer.Content = floorsModel;
        }

        private void Create3DStoreObjects()
        {
            var objectsModel = new Model3DGroup();

            foreach (var obj in _viewModel.DrawingService.StoreObjects)
            {
                var object3D = CreateStoreObject3D(obj);
                if (object3D != null)
                {
                    objectsModel.Children.Add(object3D);
                }
            }

            _storeObjectsContainer.Content = objectsModel;
        }

        // 객체 생성 로직 통합
        private Model3D CreateStoreObject3D(StoreObject obj)
        {
            // 1. OBJ 모델 로드 시도
            if (!string.IsNullOrEmpty(obj.ModelBasePath))
            {
                var loadedModel = TryLoadObjModel(obj);
                if (loadedModel != null)
                {
                    return loadedModel;
                }
                System.Diagnostics.Debug.WriteLine($"OBJ 모델 로드 실패, 기본 박스로 대체: {obj.ModelBasePath}");
            }

            // 2. OBJ 로드 실패 시 기본 박스 형태로 대체
            return CreateDefaultStoreObject3D(obj);
        }

        // *** 수정된 핵심 로직 ***
        private Model3D TryLoadObjModel(StoreObject obj)
        {
            try
            {
                string basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string baseModelPath = Path.Combine(basePath, obj.ModelBasePath);

                if (!File.Exists(baseModelPath))
                {
                    System.Diagnostics.Debug.WriteLine($"모델 파일을 찾을 수 없음: {baseModelPath}");
                    return null;
                }

                var objReader = new ObjReader();
                var finalModelGroup = new Model3DGroup();

                // 1. 기본 모델 로드
                var baseModel = objReader.Read(baseModelPath);
                if (baseModel == null) return null;

                Rect3D originalBounds = baseModel.Bounds;
                if (originalBounds.IsEmpty || originalBounds.SizeX == 0 || originalBounds.SizeY == 0 || originalBounds.SizeZ == 0)
                {
                    originalBounds = new Rect3D(-0.5, -0.5, -0.5, 1, 1, 1); // Fallback
                }
                finalModelGroup.Children.Add(baseModel);

                // 2. 선반 모델 로드 및 배치 (선반 로직은 유지)
                string shelfModelPath = !string.IsNullOrEmpty(obj.ShelfModelPath) ? Path.Combine(basePath, obj.ShelfModelPath) : null;
                if (obj.HasLayerSupport && obj.Layers > 0 && File.Exists(shelfModelPath))
                {
                    var shelfReader = new ObjReader();
                    var shelfModelTemplate = shelfReader.Read(shelfModelPath);
                    if (shelfModelTemplate != null)
                    {
                        double usableHeight = originalBounds.SizeZ * 0.9;
                        double layerSpacing = usableHeight / obj.Layers;
                        double startZ = originalBounds.Z + (originalBounds.SizeZ * 0.05);

                        for (int i = 0; i < obj.Layers; i++)
                        {
                            var shelfInstance = shelfModelTemplate.Clone();
                            double zPos = startZ + (i * layerSpacing);
                            double yPos = originalBounds.Y + originalBounds.SizeY * 0.4;
                            shelfInstance.Transform = new TranslateTransform3D(0, yPos, zPos);
                            finalModelGroup.Children.Add(shelfInstance);
                        }
                    }
                }

                // 3. 최종 변환 그룹 생성
                var transformGroup = new Transform3DGroup();

                // 3-A. 2D 도면상의 목표 크기 (피트 단위)
                double desiredWidthFt = (obj.IsHorizontal ? obj.Width : obj.Length) / 12.0;
                double desiredLengthFt = (obj.IsHorizontal ? obj.Length : obj.Width) / 12.0;
                double desiredHeightFt = obj.Height / 12.0;

                // 3-B. 각 축의 스케일 팩터 계산 (Non-Uniform Scale)
                double scaleX = desiredWidthFt / originalBounds.SizeX;
                double scaleY = desiredLengthFt / originalBounds.SizeY;
                double scaleZ = desiredHeightFt / originalBounds.SizeZ;

                // 3-C. 모델의 원본 중심점과 최종 목적지 중심점 계산
                Point3D originalCenter = new Point3D(
                    originalBounds.X + originalBounds.SizeX / 2.0,
                    originalBounds.Y + originalBounds.SizeY / 2.0,
                    originalBounds.Z + originalBounds.SizeZ / 2.0
                );
                double finalCenterX = (obj.Position.X / 12.0) + (desiredWidthFt / 2.0);
                double finalCenterY = (obj.Position.Y / 12.0) + (desiredLengthFt / 2.0);

                // 3-D. 변환 적용 (순서 중요!)
                // Step 1: 모델의 원본 중심을 월드 원점(0,0,0)으로 이동
                transformGroup.Children.Add(new TranslateTransform3D(-originalCenter.X, -originalCenter.Y, -originalCenter.Z));

                // Step 2: 원점을 기준으로 모델 크기 조절
                transformGroup.Children.Add(new ScaleTransform3D(scaleX, scaleY, scaleZ));

                // Step 3: 원점을 기준으로 회전
                if (!obj.IsHorizontal)
                {
                    transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 90)));
                }

                // Step 4: 변환된 모델을 최종 목적지 중심으로 이동
                transformGroup.Children.Add(new TranslateTransform3D(finalCenterX, finalCenterY, 0));

                finalModelGroup.Transform = transformGroup;

                return finalModelGroup;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OBJ 로드 실패: {ex.Message}");
                return null;
            }
        }


        private GeometryModel3D CreateDefaultStoreObject3D(StoreObject obj)
        {
            try
            {
                // 위치와 크기를 피트 단위로 변환
                var position = new Point3D(
                    obj.Position.X / 12.0,
                    obj.Position.Y / 12.0,
                    0);

                double width = (obj.IsHorizontal ? obj.Width : obj.Length) / 12.0;
                double length = (obj.IsHorizontal ? obj.Length : obj.Width) / 12.0;
                double height = obj.Height / 12.0;

                var meshBuilder = new MeshBuilder(false, false);
                var box = new Rect3D(position.X, position.Y, 0, width, length, height);
                meshBuilder.AddBox(box);

                var mesh = meshBuilder.ToMesh();

                // 객체 타입별 색상 설정
                Color objColor = Colors.Gray;
                switch (obj.Type)
                {
                    case ObjectType.Shelf:
                        objColor = Color.FromRgb(160, 82, 45); // 시에나 브라운
                        break;
                    case ObjectType.Refrigerator:
                        objColor = Color.FromRgb(230, 230, 250); // 라벤더
                        break;
                    case ObjectType.Checkout:
                        objColor = Color.FromRgb(192, 192, 192); // 실버
                        break;
                    case ObjectType.DisplayStand:
                        objColor = Color.FromRgb(245, 222, 179); // 밀색
                        break;
                }

                // 매트한 재질
                var material = new DiffuseMaterial(new SolidColorBrush(objColor));

                var model = new GeometryModel3D(mesh, material);
                model.BackMaterial = material;

                return model;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"매장 객체 3D 생성 오류: {ex.Message}");
                return null;
            }
        }

        private GeometryModel3D CreateWall3D(Wall wall)
        {
            try
            {
                var startPoint = new Point3D(
                    wall.StartPoint.X / 12.0,
                    wall.StartPoint.Y / 12.0,
                    0);

                var endPoint = new Point3D(
                    wall.EndPoint.X / 12.0,
                    wall.EndPoint.Y / 12.0,
                    0);

                var direction = endPoint - startPoint;
                var length = direction.Length;
                var height = WallHeight / 12.0;
                var thickness = (wall.Thickness * WallThicknessMultiplier) / 12.0; // 더 얇게

                if (length < 0.01) return null;

                var wallGeometry = CreateWallGeometry(startPoint, endPoint, height, thickness);

                // 매트한 흰색 재질 (광택 제거)
                var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(250, 250, 250)));

                var wallModel = new GeometryModel3D(wallGeometry, material);
                wallModel.BackMaterial = material;

                return wallModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"벽 3D 생성 오류: {ex.Message}");
                return null;
            }
        }

        private MeshGeometry3D CreateWallGeometry(Point3D start, Point3D end, double height, double thickness)
        {
            var mesh = new MeshGeometry3D();
            var halfThickness = thickness / 2.0;

            var direction = end - start;
            direction.Normalize();

            var perpendicular = new Vector3D(-direction.Y, direction.X, 0);
            perpendicular.Normalize();
            perpendicular *= halfThickness;

            // 벽의 8개 정점
            mesh.Positions.Add(start - perpendicular);                    // 0
            mesh.Positions.Add(start + perpendicular);                    // 1
            mesh.Positions.Add(end + perpendicular);                      // 2
            mesh.Positions.Add(end - perpendicular);                      // 3
            mesh.Positions.Add(start - perpendicular + new Vector3D(0, 0, height));  // 4
            mesh.Positions.Add(start + perpendicular + new Vector3D(0, 0, height));  // 5
            mesh.Positions.Add(end + perpendicular + new Vector3D(0, 0, height));    // 6
            mesh.Positions.Add(end - perpendicular + new Vector3D(0, 0, height));    // 7

            // 삼각형 인덱스
            int[] indices = {
                0,2,1, 0,3,2,  // 바닥
                4,5,6, 4,6,7,  // 천장
                0,1,5, 0,5,4,  // 앞면
                2,3,7, 2,7,6,  // 뒷면
                0,4,7, 0,7,3,  // 왼쪽
                1,2,6, 1,6,5   // 오른쪽
            };

            foreach (var idx in indices)
                mesh.TriangleIndices.Add(idx);

            return mesh;
        }

        private GeometryModel3D CreateFloor3D(Room room)
        {
            try
            {
                var roomPoints = GetRoomPoints3D(room);
                if (roomPoints.Count < 3) return null;

                var floorGeometry = CreateFloorGeometry(roomPoints);

                // 고정된 옅은 회색 사용 (3D용은 약간 더 불투명하게)
                var floorColor = Color.FromArgb(100, 200, 200, 200); // 알파값 100

                var material = new DiffuseMaterial(new SolidColorBrush(floorColor));

                var floorModel = new GeometryModel3D(floorGeometry, material);
                floorModel.BackMaterial = material;

                return floorModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"바닥 3D 생성 오류: {ex.Message}");
                return null;
            }
        }

        private List<Point3D> GetRoomPoints3D(Room room)
        {
            var points2D = GetRoomPoints2D(room);
            var points3D = new List<Point3D>();

            foreach (var point2D in points2D)
            {
                points3D.Add(new Point3D(
                    point2D.X / 12.0,
                    point2D.Y / 12.0,
                    0.01)); // 바닥 높이
            }

            return points3D;
        }

        private List<Point2D> GetRoomPoints2D(Room room)
        {
            var points = new List<Point2D>();
            if (room.Walls.Count == 0) return points;

            var current = room.Walls[0].StartPoint;
            points.Add(current);
            var usedWalls = new HashSet<Wall>();

            while (usedWalls.Count < room.Walls.Count)
            {
                Wall nextWall = null;
                Point2D nextPoint = null;

                foreach (var wall in room.Walls)
                {
                    if (usedWalls.Contains(wall)) continue;

                    if (Math.Abs(wall.StartPoint.X - current.X) < 1 &&
                        Math.Abs(wall.StartPoint.Y - current.Y) < 1)
                    {
                        nextWall = wall;
                        nextPoint = wall.EndPoint;
                        break;
                    }
                    else if (Math.Abs(wall.EndPoint.X - current.X) < 1 &&
                             Math.Abs(wall.EndPoint.Y - current.Y) < 1)
                    {
                        nextWall = wall;
                        nextPoint = wall.StartPoint;
                        break;
                    }
                }

                if (nextWall == null) break;

                usedWalls.Add(nextWall);
                if (Math.Abs(nextPoint.X - points[0].X) > 1 ||
                    Math.Abs(nextPoint.Y - points[0].Y) > 1)
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

        private MeshGeometry3D CreateFloorGeometry(List<Point3D> points)
        {
            var mesh = new MeshGeometry3D();

            if (points.Count < 3) return mesh;

            foreach (var point in points)
            {
                mesh.Positions.Add(point);
            }

            for (int i = 1; i < points.Count - 1; i++)
            {
                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(i);
                mesh.TriangleIndices.Add(i + 1);
            }

            return mesh;
        }

        private void UpdatePerformanceInfo()
        {
            if (_viewModel?.DrawingService != null)
            {
                var wallCount = _viewModel.DrawingService.Walls.Count;
                var roomCount = _viewModel.DrawingService.Rooms.Count;
                var objectCount = _viewModel.DrawingService.StoreObjects.Count;

                PerformanceText.Text = $"벽: {wallCount}개, 방: {roomCount}개, 객체: {objectCount}개";
            }
        }

        // 개선된 마우스 이벤트 처리
        private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning)
            {
                _isRotating = true;
                _lastMousePos = e.GetPosition((UIElement)sender);  // sender 기준 위치
                ((UIElement)sender).CaptureMouse();                // sender 기준 캡처
                e.Handled = true;
            }
        }

        private void Viewport_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isRotating)
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition((UIElement)sender);  // sender 기준으로 위치 계산
                ((UIElement)sender).CaptureMouse();                // sender 기준으로 캡처
                e.Handled = true;
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRotating && !_isPanning) return;

            var currentPos = e.GetPosition(this);
            var deltaX = currentPos.X - _lastMousePos.X;
            var deltaY = currentPos.Y - _lastMousePos.Y;

            if (_isRotating && e.LeftButton == MouseButtonState.Pressed)
            {
                // 회전
                _rotationY += deltaX * 0.5;
                _rotationX += deltaY * 0.5;
                _rotationX = Math.Max(-89, Math.Min(89, _rotationX));

                UpdateCamera();
            }
            else if (_isPanning && e.RightButton == MouseButtonState.Pressed)
            {
                // 이동
                var moveSpeed = _zoom * 0.002;
                var radY = _rotationY * Math.PI / 180;

                _lookAtPoint.X -= (Math.Cos(radY) * deltaX + Math.Sin(radY) * deltaY) * moveSpeed;
                _lookAtPoint.Y -= (-Math.Sin(radY) * deltaX + Math.Cos(radY) * deltaY) * moveSpeed;

                UpdateCamera();
            }

            _lastMousePos = currentPos;
            e.Handled = true;
        }

        private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isRotating)
            {
                _isRotating = false;
                ((UIElement)sender).ReleaseMouseCapture();         // sender 기준 해제
                e.Handled = true;
            }
        }

        private void Viewport_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ((UIElement)sender).ReleaseMouseCapture();         // sender 기준으로 해제
                e.Handled = true;
            }
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var zoomFactor = e.Delta > 0 ? 0.9 : 1.1;
            _zoom = Math.Max(10, Math.Min(500, _zoom * zoomFactor));

            UpdateCamera();
            e.Handled = true;
        }

        private void UpdateCamera()
        {
            var radX = _rotationX * Math.PI / 180;
            var radY = _rotationY * Math.PI / 180;

            var x = _zoom * Math.Cos(radX) * Math.Sin(radY);
            var y = _zoom * Math.Cos(radX) * Math.Cos(radY);
            var z = _zoom * Math.Sin(radX);

            MainCamera.Position = new Point3D(_lookAtPoint.X + x, _lookAtPoint.Y + y, _lookAtPoint.Z + z);
            MainCamera.LookDirection = new Vector3D(-x, -y, -z);
            MainCamera.UpDirection = new Vector3D(0, 0, 1);

            UpdateCameraInfo();
        }

        private void UpdateCameraInfo()
        {
            var pos = MainCamera.Position;
            CameraInfoText.Text = $"카메라: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})";
            LookDirectionText.Text = $"회전: X={_rotationX:F0}° Y={_rotationY:F0}°";
            ZoomLevelText.Text = $"줌: {(_zoom / 150 * 100):F0}%";
        }

        // 뷰 프리셋
        private void FrontView_Click(object sender, RoutedEventArgs e)
        {
            _rotationX = 0;
            _rotationY = 0;
            _zoom = 150;
            _lookAtPoint = CalculateSceneCenter();
            UpdateCamera();
        }

        private void SideView_Click(object sender, RoutedEventArgs e)
        {
            _rotationX = 0;
            _rotationY = 90;
            _zoom = 150;
            _lookAtPoint = CalculateSceneCenter();
            UpdateCamera();
        }

        private void TopView_Click(object sender, RoutedEventArgs e)
        {
            _rotationX = 89;
            _rotationY = 0;
            _zoom = 150;
            _lookAtPoint = CalculateSceneCenter();
            UpdateCamera();
        }

        private void IsometricView_Click(object sender, RoutedEventArgs e)
        {
            _rotationX = 30;
            _rotationY = -45;
            _zoom = 150;
            _lookAtPoint = CalculateSceneCenter();
            UpdateCamera();
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            _rotationX = 30;
            _rotationY = -45;
            _zoom = 150;
            _lookAtPoint = CalculateSceneCenter();
            UpdateCamera();
        }

        private void ZoomExtents_Click(object sender, RoutedEventArgs e)
        {
            ZoomExtents();
        }

        private void ZoomExtents()
        {
            var center = CalculateSceneCenter();
            var bounds = CalculateSceneBounds();

            _lookAtPoint = center;
            _zoom = Math.Max(bounds * 2.0, 100);
            UpdateCamera();
        }

        private Point3D CalculateSceneCenter()
        {
            if (_viewModel?.DrawingService == null)
                return new Point3D(0, 0, WallHeight / 24.0);

            var minX = double.MaxValue;
            var maxX = double.MinValue;
            var minY = double.MaxValue;
            var maxY = double.MinValue;

            // 벽 경계 계산
            foreach (var wall in _viewModel.DrawingService.Walls)
            {
                minX = Math.Min(minX, Math.Min(wall.StartPoint.X, wall.EndPoint.X));
                maxX = Math.Max(maxX, Math.Max(wall.StartPoint.X, wall.EndPoint.X));
                minY = Math.Min(minY, Math.Min(wall.StartPoint.Y, wall.EndPoint.Y));
                maxY = Math.Max(maxY, Math.Max(wall.StartPoint.Y, wall.EndPoint.Y));
            }

            // 매장 객체 경계 계산
            foreach (var obj in _viewModel.DrawingService.StoreObjects)
            {
                var (min, max) = obj.GetBoundingBox();
                minX = Math.Min(minX, min.X);
                maxX = Math.Max(maxX, max.X);
                minY = Math.Min(minY, min.Y);
                maxY = Math.Max(maxY, max.Y);
            }

            if (minX == double.MaxValue) // 아무것도 없는 경우
                return new Point3D(0, 0, WallHeight / 24.0);

            return new Point3D((minX + maxX) / 24.0, (minY + maxY) / 24.0, WallHeight / 24.0);
        }

        private double CalculateSceneBounds()
        {
            if (_viewModel?.DrawingService == null)
                return 100;

            var minX = double.MaxValue;
            var maxX = double.MinValue;
            var minY = double.MaxValue;
            var maxY = double.MinValue;

            // 벽 경계 계산
            foreach (var wall in _viewModel.DrawingService.Walls)
            {
                minX = Math.Min(minX, Math.Min(wall.StartPoint.X, wall.EndPoint.X));
                maxX = Math.Max(maxX, Math.Max(wall.StartPoint.X, wall.EndPoint.X));
                minY = Math.Min(minY, Math.Min(wall.StartPoint.Y, wall.EndPoint.Y));
                maxY = Math.Max(maxY, Math.Max(wall.StartPoint.Y, wall.EndPoint.Y));
            }

            // 매장 객체 경계 계산
            foreach (var obj in _viewModel.DrawingService.StoreObjects)
            {
                var (min, max) = obj.GetBoundingBox();
                minX = Math.Min(minX, min.X);
                maxX = Math.Max(maxX, max.X);
                minY = Math.Min(minY, min.Y);
                maxY = Math.Max(maxY, max.Y);
            }

            if (minX == double.MaxValue) // 아무것도 없는 경우
                return 100;

            var width = (maxX - minX) / 12.0;
            var height = (maxY - minY) / 12.0;

            return Math.Max(width, Math.Max(height, WallHeight / 12.0));
        }

        public void FocusOn3DModel()
        {
            ZoomExtents();
        }
    }
}