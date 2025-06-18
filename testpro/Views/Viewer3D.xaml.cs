using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using testpro.Models;
using testpro.ViewModels;
using testpro.Dialogs;
using HelixToolkit.Wpf;

namespace testpro.Views
{
    public partial class Viewer3D : UserControl
    {
        private MainViewModel _viewModel;
        private readonly double WallHeight = 96.0;
        private readonly double WallThicknessMultiplier = 1.2;

        private bool _isRotating = false;
        private bool _isPanning = false;
        private Point _lastMousePos;
        private double _rotationX = 30;
        private double _rotationY = -45;
        private double _zoom = 150;
        private Point3D _lookAtPoint = new Point3D(0, 0, 0);

        private readonly Dictionary<GeometryModel3D, StoreObject> _geometryMap;
        private readonly ModelVisual3D _storeObjectsContainer;
        private readonly ModelVisual3D _wallsContainer;
        private readonly ModelVisual3D _floorsContainer;
        private readonly ModelVisual3D _gridContainer;

        public MainViewModel ViewModel
        {
            get => _viewModel;
            set
            {
                if (_viewModel != null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
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

            _geometryMap = new Dictionary<GeometryModel3D, StoreObject>();
            _storeObjectsContainer = new ModelVisual3D();
            _wallsContainer = new ModelVisual3D();
            _floorsContainer = new ModelVisual3D();
            _gridContainer = new ModelVisual3D();

            SetupViewport();
            UpdateCamera();

            this.KeyDown += Viewer3D_KeyDown;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.DrawingService))
            {
                UpdateAll3DModels();
            }
        }

        private void SetupViewport()
        {
            MainViewport.Children.Clear();
            MainViewport.Children.Add(new DefaultLights());
            MainViewport.Children.Add(_gridContainer);
            MainViewport.Children.Add(_wallsContainer);
            MainViewport.Children.Add(_floorsContainer);
            MainViewport.Children.Add(_storeObjectsContainer);

            _gridContainer.Content = CreateFloorGridModel();
        }

        public void UpdateAll3DModels()
        {
            if (_viewModel?.DrawingService == null) return;
            try
            {
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

        private void Create3DStoreObjects()
        {
            var objectsModelGroup = new Model3DGroup();
            _geometryMap.Clear();

            if (_viewModel?.DrawingService?.StoreObjects == null) return;

            foreach (var obj in _viewModel.DrawingService.StoreObjects)
            {
                var objectModel = TryLoadObjModel(obj);
                if (objectModel != null)
                {
                    objectsModelGroup.Children.Add(objectModel);
                    MapGeometries(objectModel, obj);
                }
            }
            _storeObjectsContainer.Content = objectsModelGroup;
        }

        private void MapGeometries(Model3D model, StoreObject storeObject)
        {
            if (model is GeometryModel3D geometryModel)
            {
                if (!_geometryMap.ContainsKey(geometryModel)) _geometryMap.Add(geometryModel, storeObject);
            }
            else if (model is Model3DGroup group)
            {
                foreach (var child in group.Children) MapGeometries(child, storeObject);
            }
        }

        // *** 3D 객체 클릭 이벤트 핸들러 수정 ***
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            Point mousePosition = e.GetPosition(MainViewport);

            var result = VisualTreeHelper.HitTest(MainViewport, mousePosition) as RayMeshGeometry3DHitTestResult;

            if (result != null && result.ModelHit is GeometryModel3D hitGeometry && _geometryMap.TryGetValue(hitGeometry, out StoreObject clickedObject))
            {
                // 편집 모드의 속성 설정창 열기
                var dialog = new ObjectTypeSelectionDialog(clickedObject);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    // 다이얼로그에서 변경된 속성으로 객체 업데이트
                    _viewModel.DrawingService.UpdateStoreObject(
                        clickedObject,
                        dialog.ObjectWidth,
                        dialog.ObjectLength,
                        dialog.ObjectHeight,
                        dialog.ObjectLayers,
                        dialog.IsHorizontal,
                        dialog.Temperature,
                        dialog.CategoryCode
                    );
                }
                e.Handled = true;
            }
        }


private Model3D TryLoadObjModel(StoreObject obj)
        {
            try
            {
                string basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string baseModelPath = Path.Combine(basePath, obj.ModelBasePath);
                if (!File.Exists(baseModelPath)) return null;

                var objReader = new ObjReader();
                var finalModelGroup = new Model3DGroup();
                var baseModel = objReader.Read(baseModelPath);
                if (baseModel == null) return null;

                Rect3D originalBounds = baseModel.Bounds;
                if (originalBounds.IsEmpty || originalBounds.SizeX == 0 || originalBounds.SizeY == 0 || originalBounds.SizeZ == 0)
                {
                    originalBounds = new Rect3D(-0.5, -0.5, -0.5, 1, 1, 1);
                }
                finalModelGroup.Children.Add(baseModel);

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

                var transformGroup = new Transform3DGroup();

                // 1. 모델의 '기하학적 중심'을 (0,0,0) 원점으로 이동시킵니다.
                double initialOffsetX = originalBounds.X + originalBounds.SizeX / 2.0;
                double initialOffsetY = originalBounds.Y + originalBounds.SizeY / 2.0;
                double initialOffsetZ = originalBounds.Z + originalBounds.SizeZ / 2.0;
                transformGroup.Children.Add(new TranslateTransform3D(-initialOffsetX, -initialOffsetY, -initialOffsetZ));

                // --- 스케일링 및 회전 변수 설정 ---
                double desiredWidthFt, desiredLengthFt, desiredHeightFt;
                double scaleX, scaleY, scaleZ;

                // ==================================================================
                // ===== ▼ Freezer만 분리하기 위해 switch 문으로 변경 ▼ =====
                // ==================================================================
                switch (obj.Type)
                {
                    case ObjectType.Refrigerator:
                        desiredWidthFt = (obj.IsHorizontal ? obj.Width : obj.Length) / 12.0;
                        desiredLengthFt = (obj.IsHorizontal ? obj.Length : obj.Width) / 12.0;
                        desiredHeightFt = obj.Height / 12.0;

                        scaleX = desiredWidthFt / originalBounds.SizeX;
                        scaleY = desiredLengthFt / originalBounds.SizeY;
                        scaleZ = desiredHeightFt / originalBounds.SizeZ;

                        transformGroup.Children.Add(new ScaleTransform3D(scaleX, scaleY, scaleZ));

                        if (!obj.IsHorizontal)
                        {
                            transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 90)));
                        }
                        break;

                    case ObjectType.Freezer: // Freezer 로직 분리
                        desiredWidthFt = obj.Width / 12.0;
                        desiredLengthFt = obj.Length / 12.0;
                        desiredHeightFt = obj.Height / 12.0;

                        scaleX = desiredWidthFt / originalBounds.SizeX;
                        scaleY = desiredLengthFt / originalBounds.SizeZ;
                        scaleZ = desiredHeightFt / originalBounds.SizeY;

                        transformGroup.Children.Add(new ScaleTransform3D(scaleX, scaleY, scaleZ));

                        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 90)));
                        // Yaw 회전 시 180도를 더해 앞뒤를 바꿔줍니다.
                        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), obj.Rotation + 180)));
                        break;

                    default: // Shelf, Checkout 등 나머지 모든 객체
                        desiredWidthFt = obj.Width / 12.0;
                        desiredLengthFt = obj.Length / 12.0;
                        desiredHeightFt = obj.Height / 12.0;

                        scaleX = desiredWidthFt / originalBounds.SizeX;
                        scaleY = desiredLengthFt / originalBounds.SizeZ;
                        scaleZ = desiredHeightFt / originalBounds.SizeY;

                        transformGroup.Children.Add(new ScaleTransform3D(scaleX, scaleY, scaleZ));

                        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0)));
                        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), obj.Rotation)));
                        break;
                }
                // ==================================================================
                // ===== ▲ 여기가 최종 수정 부분입니다 ▲ =====
                // ==================================================================

                // --- 최종 위치 이동 ---
                double finalCenterX = (obj.Position.X / 12.0) + (desiredWidthFt / 2.0);
                double finalCenterY = (obj.Position.Y / 12.0) + (desiredLengthFt / 2.0);
                double finalOffsetZ = desiredHeightFt / 2.0;

                transformGroup.Children.Add(new TranslateTransform3D(finalCenterX, finalCenterY, finalOffsetZ));

                finalModelGroup.Transform = transformGroup;
                return finalModelGroup;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OBJ 로드 실패({obj.ModelBasePath}): {ex.Message}");
                return null;
            }
        }

        private void Viewer3D_KeyDown(object sender, KeyEventArgs e)
        {
            double moveSpeed = _zoom * 0.01;

            var forward = MainCamera.LookDirection;
            forward.Z = 0;
            forward.Normalize();

            var right = Vector3D.CrossProduct(forward, new Vector3D(0, 0, 1));

            bool moved = true;
            Vector3D moveVector = new Vector3D();

            switch (e.Key)
            {
                case Key.W: moveVector = forward * moveSpeed; break;
                case Key.S: moveVector = -forward * moveSpeed; break;
                case Key.A: moveVector = -right * moveSpeed; break;
                case Key.D: moveVector = right * moveSpeed; break;
                default: moved = false; break;
            }

            if (moved)
            {
                MainCamera.Position += moveVector;
                _lookAtPoint += moveVector;
                UpdateCameraInfo();
                e.Handled = true;
            }
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            Focus();
            _isRotating = true;
            _lastMousePos = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            _isRotating = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Middle)
            {
                Focus();
                _isPanning = true;
                _lastMousePos = e.GetPosition(this);
                CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!IsMouseCaptured) return;

            var currentPos = e.GetPosition(this);
            var delta = currentPos - _lastMousePos;

            if (_isRotating)
            {
                _rotationY += delta.X * 0.5;
                _rotationX = Math.Max(-89, Math.Min(89, _rotationX - delta.Y * 0.5));
                UpdateCamera();
            }
            else if (_isPanning)
            {
                var moveSpeed = _zoom * 0.002;
                var radY = _rotationY * Math.PI / 180.0;
                var sinY = Math.Sin(radY);
                var cosY = Math.Cos(radY);
                var moveVector = -(cosY * delta.X - sinY * delta.Y) * new Vector3D(cosY, sinY, 0)
                                 - (sinY * delta.X + cosY * delta.Y) * new Vector3D(-sinY, cosY, 0);

                _lookAtPoint += moveVector * moveSpeed;
                UpdateCamera();
            }

            _lastMousePos = currentPos;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            _zoom *= e.Delta > 0 ? 0.9 : 1.1;
            _zoom = Math.Max(10, Math.Min(1000, _zoom));
            UpdateCamera();
        }

        private void Create3DWalls()
        {
            var wallsModel = new Model3DGroup();
            if (_viewModel?.DrawingService == null) return;
            foreach (var wall in _viewModel.DrawingService.Walls)
            {
                var wall3D = CreateWall3D(wall);
                if (wall3D != null) wallsModel.Children.Add(wall3D);
            }
            _wallsContainer.Content = wallsModel;
        }

        private void Create3DFloors()
        {
            var floorsModel = new Model3DGroup();
            if (_viewModel?.DrawingService == null) return;
            foreach (var room in _viewModel.DrawingService.Rooms)
            {
                if (room.IsClosedRoom())
                {
                    var floor3D = CreateFloor3D(room);
                    if (floor3D != null) floorsModel.Children.Add(floor3D);
                }
            }
            _floorsContainer.Content = floorsModel;
        }

        private Model3D CreateFloorGridModel()
        {
            var gridModel = new Model3DGroup();
            CreateGridSection(gridModel, -100, 100, 12, 120, 1.0);
            CreateGridSection(gridModel, -300, -100, 24, 120, 0.5);
            CreateGridSection(gridModel, 100, 300, 24, 120, 0.5);
            CreateGridSection(gridModel, -1000, -300, 48, 120, 0.2);
            CreateGridSection(gridModel, 300, 1000, 48, 120, 0.2);
            return gridModel;
        }

        private void CreateGridSection(Model3DGroup gridModel, int start, int end, int step, int majorStep, double opacity)
        {
            var color = Color.FromArgb((byte)(255 * opacity), 128, 128, 128);
            var majorColor = Color.FromArgb((byte)(255 * opacity), 96, 96, 96);
            for (int i = start; i <= end; i += step)
            {
                var vLine = CreateLine(new Point3D(i / 12.0, start / 12.0, 0), new Point3D(i / 12.0, end / 12.0, 0), i % majorStep == 0 ? majorColor : color, i % majorStep == 0 ? 0.02 : 0.01);
                if (vLine != null) gridModel.Children.Add(vLine);
                var hLine = CreateLine(new Point3D(start / 12.0, i / 12.0, 0), new Point3D(end / 12.0, i / 12.0, 0), i % majorStep == 0 ? majorColor : color, i % majorStep == 0 ? 0.02 : 0.01);
                if (hLine != null) gridModel.Children.Add(hLine);
            }
        }

        private GeometryModel3D CreateLine(Point3D start, Point3D end, Color color, double thickness)
        {
            var mb = new MeshBuilder(false, false);
            mb.AddPipe(start, end, 0, thickness, 4);
            return new GeometryModel3D(mb.ToMesh(), new DiffuseMaterial(new SolidColorBrush(color)));
        }

        private GeometryModel3D CreateWall3D(Wall wall)
        {
            var startPoint = new Point3D(wall.StartPoint.X / 12.0, wall.StartPoint.Y / 12.0, 0);
            var endPoint = new Point3D(wall.EndPoint.X / 12.0, wall.EndPoint.Y / 12.0, 0);
            var length = (endPoint - startPoint).Length;
            if (length < 0.01) return null;
            var height = WallHeight / 12.0;
            var thickness = (wall.Thickness * WallThicknessMultiplier) / 12.0;
            var mb = new MeshBuilder(false, false);
            var p1 = startPoint;
            var p2 = endPoint;
            var dir = p2 - p1;
            var p3 = p2 + new Vector3D(0, 0, height);
            var p4 = p1 + new Vector3D(0, 0, height);
            var normal = Vector3D.CrossProduct(dir, new Vector3D(0, 0, 1));
            normal.Normalize();
            var offset = normal * thickness * 0.5;
            mb.AddPolygon(new[] { p1 - offset, p2 - offset, p2 + offset, p1 + offset });
            mb.AddPolygon(new[] { p4 - offset, p1 - offset, p1 + offset, p4 + offset });
            mb.AddPolygon(new[] { p3 - offset, p4 - offset, p4 + offset, p3 + offset });
            mb.AddPolygon(new[] { p2 - offset, p3 - offset, p3 + offset, p2 + offset });
            mb.AddPolygon(new[] { p1 + offset, p2 + offset, p3 + offset, p4 + offset });
            mb.AddPolygon(new[] { p2 - offset, p1 - offset, p4 - offset, p3 - offset });
            return new GeometryModel3D(mb.ToMesh(), Materials.White);
        }

        private GeometryModel3D CreateFloor3D(Room room)
        {
            var roomPoints = GetRoomPoints2D(room).Select(p => new Point3D(p.X / 12.0, p.Y / 12.0, 0.01)).ToList();
            if (roomPoints.Count < 3) return null;
            var mb = new MeshBuilder(false, false);
            mb.AddPolygon(roomPoints);
            return new GeometryModel3D(mb.ToMesh(), new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(220, 220, 220))));
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
                foreach (var wall in room.Walls.Where(w => !usedWalls.Contains(w)))
                {
                    if (Math.Abs(wall.StartPoint.X - current.X) < 1 && Math.Abs(wall.StartPoint.Y - current.Y) < 1) { nextWall = wall; nextPoint = wall.EndPoint; break; }
                    else if (Math.Abs(wall.EndPoint.X - current.X) < 1 && Math.Abs(wall.EndPoint.Y - current.Y) < 1) { nextWall = wall; nextPoint = wall.StartPoint; break; }
                }
                if (nextWall == null) break;
                usedWalls.Add(nextWall);
                if (Math.Abs(nextPoint.X - points[0].X) > 1 || Math.Abs(nextPoint.Y - points[0].Y) > 1) { points.Add(nextPoint); current = nextPoint; }
                else break;
            }
            return points;
        }

        private void UpdatePerformanceInfo()
        {
            if (_viewModel?.DrawingService != null)
            {
                PerformanceText.Text = $"벽: {_viewModel.DrawingService.Walls.Count}개, 방: {_viewModel.DrawingService.Rooms.Count}개, 객체: {_viewModel.DrawingService.StoreObjects.Count}개";
            }
        }

        private void UpdateCamera()
        {
            var radX = _rotationX * Math.PI / 180;
            var radY = _rotationY * Math.PI / 180;
            var x = -(_zoom * Math.Cos(radX) * Math.Sin(radY));
            var y = _zoom * Math.Cos(radX) * Math.Cos(radY);
            var z = _zoom * Math.Sin(radX);
            MainCamera.Position = _lookAtPoint + new Vector3D(x, y, z);
            MainCamera.LookDirection = -new Vector3D(x, y, z);
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

        private void FrontView_Click(object sender, RoutedEventArgs e) { SetViewPreset(0, 0); }
        private void SideView_Click(object sender, RoutedEventArgs e) { SetViewPreset(0, 90); }
        private void TopView_Click(object sender, RoutedEventArgs e) { SetViewPreset(89, 0); }
        private void IsometricView_Click(object sender, RoutedEventArgs e) { SetViewPreset(30, -45); }
        private void ResetView_Click(object sender, RoutedEventArgs e) { SetViewPreset(30, -45); }

        private void SetViewPreset(double rotX, double rotY)
        {
            _rotationX = rotX;
            _rotationY = rotY;
            _zoom = 150;
            _lookAtPoint = CalculateSceneCenter();
            UpdateCamera();
        }

        private void ZoomExtents_Click(object sender, RoutedEventArgs e) => ZoomExtents();

        private void ZoomExtents()
        {
            _lookAtPoint = CalculateSceneCenter();
            _zoom = Math.Max(CalculateSceneBounds() * 2.0, 100);
            UpdateCamera();
        }

        private Point3D CalculateSceneCenter()
        {
            if (_viewModel?.DrawingService == null || (!_viewModel.DrawingService.Walls.Any() && !_viewModel.DrawingService.StoreObjects.Any()))
                return new Point3D(0, 0, WallHeight / 24.0);

            var allPoints = _viewModel.DrawingService.Walls.SelectMany(w => new[] { w.StartPoint, w.EndPoint })
                .Concat(_viewModel.DrawingService.StoreObjects.Select(o => o.GetBoundingBox().min))
                .Concat(_viewModel.DrawingService.StoreObjects.Select(o => o.GetBoundingBox().max));

            if (!allPoints.Any()) return new Point3D(0, 0, WallHeight / 24.0);

            double minX = allPoints.Min(p => p.X), maxX = allPoints.Max(p => p.X);
            double minY = allPoints.Min(p => p.Y), maxY = allPoints.Max(p => p.Y);
            return new Point3D((minX + maxX) / 24.0, (minY + maxY) / 24.0, WallHeight / 24.0);
        }

        private double CalculateSceneBounds()
        {
            if (_viewModel?.DrawingService == null || (!_viewModel.DrawingService.Walls.Any() && !_viewModel.DrawingService.StoreObjects.Any()))
                return 100;

            var allPoints = _viewModel.DrawingService.Walls.SelectMany(w => new[] { w.StartPoint, w.EndPoint })
                .Concat(_viewModel.DrawingService.StoreObjects.Select(o => o.GetBoundingBox().min))
                .Concat(_viewModel.DrawingService.StoreObjects.Select(o => o.GetBoundingBox().max));

            if (!allPoints.Any()) return 100;

            double minX = allPoints.Min(p => p.X), maxX = allPoints.Max(p => p.X);
            double minY = allPoints.Min(p => p.Y), maxY = allPoints.Max(p => p.Y);
            return Math.Max((maxX - minX) / 12.0, Math.Max((maxY - minY) / 12.0, WallHeight / 12.0));
        }

        public void FocusOn3DModel() => ZoomExtents();

    }
}