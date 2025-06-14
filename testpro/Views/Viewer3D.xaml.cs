using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using HelixToolkit.Wpf; // ObjReader를 사용하기 위해 필요합니다.
using testpro.Models; // 이 부분과 ViewModel이 필수
using testpro.ViewModels; // 이 부분과 Model이 필수
// using HelixToolkit.Wpf.SharpDX.Cameras; // SharpDX를 사용하지 않으므로 제거합니다.

namespace testpro.Views
{
    public partial class Viewer3D : UserControl
    {
        private MainViewModel _viewModel;
        private ModelVisual3D _modelsContainer;
        private DirectionalLight _directionalLight;
        // OBJ 모델을 캐시하기 위해 Model3DGroup 대신 Model3D로 변경
        private Dictionary<string, Model3D> _modelCache = new Dictionary<string, Model3D>();

        public Viewer3D()
        {
            InitializeComponent();
            SetupViewer();
        }

        private void SetupViewer()
        {
            // 조명 설정
            var lights = new Model3DGroup();

            // 주변광
            lights.Children.Add(new AmbientLight(Colors.White)
            {
                Color = Color.FromRgb(150, 150, 150)
            });

            // 방향광
            _directionalLight = new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1))
            {
                Color = Color.FromRgb(255, 255, 255)
            };
            lights.Children.Add(_directionalLight);

            // 추가 조명 (반대 방향)
            lights.Children.Add(new DirectionalLight(Colors.White, new Vector3D(1, 1, -0.5))
            {
                Color = Color.FromRgb(100, 100, 100)
            });

            var lightVisual = new ModelVisual3D { Content = lights };
            ViewportMain.Children.Add(lightVisual);

            // 모델 컨테이너
            _modelsContainer = new ModelVisual3D();
            ViewportMain.Children.Add(_modelsContainer);

            // 카메라 설정
            CameraMain.Position = new Point3D(50, 50, 50);
            CameraMain.LookDirection = new Vector3D(-50, -50, -50);
            CameraMain.UpDirection = new Vector3D(0, 0, 1);
            CameraMain.FieldOfView = 60;

            // 바닥 그리드 추가
            AddFloorGrid();
        }

        private void AddFloorGrid()
        {
            var gridLines = new GridLinesVisual3D
            {
                // 크기를 조금 더 키워서 매장 크기를 고려합니다.
                Width = 1000 / 12.0, // 1000 픽셀을 피트로 변환
                Length = 800 / 12.0, // 800 픽셀을 피트로 변환
                MinorDistance = 1,   // 1피트 간격
                MajorDistance = 5,   // 5피트 간격
                Thickness = 0.1,
                Center = new Point3D((1000 / 12.0) / 2, (800 / 12.0) / 2, 0) // 중앙에 오도록 조정
            };
            ViewportMain.Children.Add(gridLines);
        }

        public MainViewModel ViewModel
        {
            get => _viewModel;
            set
            {
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    _viewModel.StoreObjects.CollectionChanged -= StoreObjects_CollectionChanged;
                }

                _viewModel = value;
                DataContext = _viewModel;

                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                    _viewModel.StoreObjects.CollectionChanged += StoreObjects_CollectionChanged;
                    UpdateView();
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "StoreObjects" || e.PropertyName == "SelectedObject")
            {
                UpdateView();
            }
        }

        private void StoreObjects_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateView();
        }

        public void UpdateView()
        {
            _modelsContainer.Children.Clear();

            if (_viewModel?.StoreObjects == null) return;

            foreach (var obj in _viewModel.StoreObjects)
            {
                var model3D = CreateStoreObject3D(obj);
                if (model3D != null)
                {
                    var visual = new ModelVisual3D { Content = model3D };
                    visual.SetValue(FrameworkElement.TagProperty, obj.Id);
                    _modelsContainer.Children.Add(visual);

                    // 선택된 객체 강조 (bounding box를 별도의 ModelVisual3D로 추가)
                    if (obj.IsSelected)
                    {
                        var boundingBox = CreateBoundingBox(obj);
                        if (boundingBox != null)
                        {
                            var boundingBoxVisual = new ModelVisual3D { Content = boundingBox };
                            _modelsContainer.Children.Add(boundingBoxVisual);
                        }
                    }
                }
            }
        }

        private Model3D CreateStoreObject3D(StoreObject obj)
        {
            try
            {
                // OBJ 파일 로드 시도
                var loadedModel = TryLoadObjModel(obj);
                if (loadedModel != null)
                    return loadedModel;

                // 실패 시 기본 모델 생성
                return CreateDefaultModel(obj);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"3D 모델 생성 오류: {ex.Message}");
                return CreateDefaultModel(obj);
            }
        }

        private GeometryModel3D TryLoadObjModel(StoreObject obj)
        {
            try
            {
                if (string.IsNullOrEmpty(obj.ModelPath))
                    return null;

                string cacheKey = $"{obj.ModelPath}"; // 텍스처 변경될 경우도 고려하여 캐시키 조정
                if (_modelCache.ContainsKey(cacheKey))
                {
                    // 캐시된 모델의 복사본을 가져와 변환 적용
                    var cachedModel = _modelCache[cacheKey].Clone() as GeometryModel3D;
                    if (cachedModel != null)
                    {
                        ApplyTransform(cachedModel, obj);
                        return cachedModel;
                    }
                }

                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string modelPath = Path.Combine(basePath, obj.ModelPath);

                if (!File.Exists(modelPath))
                {
                    System.Diagnostics.Debug.WriteLine($"모델 파일을 찾을 수 없음: {modelPath}");
                    return null;
                }

                // OBJ 파일 로드
                var objReader = new ObjReader();
                Model3DGroup model3DGroup = null;
                try
                {
                    model3DGroup = objReader.Read(modelPath);
                }
                catch (Exception objEx)
                {
                    System.Diagnostics.Debug.WriteLine($"OBJ 파일 읽기 오류: {objEx.Message}");
                    return null;
                }


                if (model3DGroup == null || model3DGroup.Children.Count == 0)
                    return null;

                // 모든 GeometryModel3D를 하나로 결합
                var combinedMesh = new MeshBuilder();
                Material material = null; // 초기 재질은 null

                foreach (var child in model3DGroup.Children)
                {
                    if (child is GeometryModel3D gm && gm.Geometry is MeshGeometry3D mesh)
                    {
                        combinedMesh.Append(mesh);
                        // 첫 번째 GeometryModel3D의 Material을 기본으로 사용
                        if (material == null && gm.Material != null)
                            material = gm.Material;
                    }
                }

                var finalMesh = combinedMesh.ToMesh();
                if (finalMesh.Positions.Count == 0)
                    return null;

                // 텍스처 처리
                if (!string.IsNullOrEmpty(obj.TexturePath))
                {
                    string texturePath = Path.Combine(basePath, obj.TexturePath);
                    if (File.Exists(texturePath))
                    {
                        var bitmap = new BitmapImage(new Uri(texturePath, UriKind.Absolute));
                        var brush = new ImageBrush(bitmap)
                        {
                            TileMode = TileMode.Tile,
                            ViewportUnits = BrushMappingMode.Absolute,
                            Viewport = new Rect(0, 0, 1, 1)
                        };
                        material = new DiffuseMaterial(brush);
                    }
                }

                // 재질이 없으면 기본 재질 생성
                if (material == null)
                {
                    material = GetMaterialForType(obj.Type);
                }

                var geometryModel = new GeometryModel3D(finalMesh, material);
                geometryModel.BackMaterial = material;

                // 캐시에 저장 (원본 모델을 저장)
                _modelCache[cacheKey] = geometryModel.Clone();

                // 변환 적용 (캐시된 모델의 복사본이 아닌 새로 생성된 모델에 적용)
                ApplyTransform(geometryModel, obj);

                return geometryModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OBJ 로드 실패: {ex.Message}");
                return null;
            }
        }

        private void ApplyTransform(GeometryModel3D model, StoreObject obj)
        {
            var transformGroup = new Transform3DGroup();

            // 모델의 원래 크기를 1x1x1 단위로 정규화했다고 가정하고,
            // StoreObject의 Width, Length, Height를 인치에서 피트로 변환하여 스케일 적용
            // (12인치 = 1피트)
            double scaleX = obj.Width / 12.0;
            double scaleY = obj.Length / 12.0;
            double scaleZ = obj.Height / 12.0;

            // 모델링된 OBJ 파일의 크기가 정규화되지 않았을 경우, 여기서 추가 스케일 조정 필요
            // 예: 모델이 실제 100단위로 되어 있다면, 100으로 나눠주는 추가 스케일 필요
            // 현재 OBJ 파일의 기본 크기를 모르므로, 임의의 기본 크기(예: 1피트)를 기준으로 스케일링
            // 실제 Blender에서 만들 모델의 단위를 고려하여 조정해야 합니다.
            // 예를 들어 Blender에서 1m = 1 unit으로 모델링했다면, 12.0 대신 1.0을 사용하고
            // obj.Width, obj.Length, obj.Height를 미터로 변환해야 할 수 있습니다.
            // 여기서는 임시로 12.0으로 나누어 인치->피트 변환을 유지합니다.

            transformGroup.Children.Add(new ScaleTransform3D(scaleX, scaleY, scaleZ));

            // 회전 적용 (Z축 기준)
            if (Math.Abs(obj.Rotation) > 0.01)
            {
                // Rotation은 2D 평면에서의 회전이므로 Z축을 기준으로 회전합니다.
                transformGroup.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(new Vector3D(0, 0, 1), obj.Rotation)));
            }

            // 위치 이동
            // 2D Position은 왼쪽 상단 기준이므로, 3D의 중앙 기준으로 변환
            // 모델의 중심이 (0,0,0)에 있다고 가정하고, 최종적으로 오브젝트의 좌하단 코너가
            // obj.Position.X / 12.0, obj.Position.Y / 12.0, 0 에 오도록 Translate
            transformGroup.Children.Add(new TranslateTransform3D(
                obj.Position.X / 12.0,
                obj.Position.Y / 12.0,
                0));

            model.Transform = transformGroup;
        }


        private GeometryModel3D CreateDefaultModel(StoreObject obj)
        {
            var meshBuilder = new MeshBuilder();

            // 인치 값을 피트 값으로 변환 (12인치 = 1피트)
            var width = obj.Width / 12.0;
            var depth = obj.Length / 12.0;
            var height = obj.Height / 12.0;

            // 기본 박스 모델은 항상 (0,0,0)을 중심으로 생성되므로,
            // 실제 위치를 고려하여 translateTransform에 영향을 주도록 변경합니다.
            // 여기서는 모델 자체는 (0,0,0)을 중심으로 만들고, TranslateTransform에서 위치를 조정합니다.
            meshBuilder.AddBox(new Point3D(0, 0, 0), width, depth, height);

            var mesh = meshBuilder.ToMesh();
            var material = GetMaterialForType(obj.Type);

            var model = new GeometryModel3D(mesh, material);
            model.BackMaterial = material;

            // 변환 적용 (회전 중심점도 고려)
            var transformGroup = new Transform3DGroup();

            // 회전 적용
            if (Math.Abs(obj.Rotation) > 0.01)
            {
                // 회전의 중심을 객체의 2D 평면 중심 (3D에서는 Z=height/2)으로 설정
                var rotateTransform = new RotateTransform3D(
                    new AxisAngleRotation3D(new Vector3D(0, 0, 1), obj.Rotation),
                    new Point3D(0, 0, height / 2)); // 모델의 로컬 중심 기준
                transformGroup.Children.Add(rotateTransform);
            }

            // 위치 이동
            // 모델의 기준점이 (0,0,0)이므로, 객체의 왼쪽 아래 모서리가 위치하도록 이동
            transformGroup.Children.Add(new TranslateTransform3D(
                obj.Position.X / 12.0,
                obj.Position.Y / 12.0,
                0));

            model.Transform = transformGroup;

            return model;
        }

        private Material GetMaterialForType(ObjectType type)
        {
            Color color = Colors.Gray;

            switch (type)
            {
                case ObjectType.Shelf:
                    color = Color.FromRgb(160, 82, 45); // 시에나 브라운
                    break;
                case ObjectType.Refrigerator:
                    color = Color.FromRgb(230, 240, 250); // 라이트 블루
                    break;
                case ObjectType.Freezer:
                    color = Color.FromRgb(200, 220, 255); // 연한 파랑
                    break;
                case ObjectType.Checkout:
                    color = Color.FromRgb(192, 192, 192); // 실버
                    break;
                case ObjectType.DisplayStand:
                    color = Color.FromRgb(245, 222, 179); // 밀색
                    break;
                case ObjectType.Pillar:
                    color = Color.FromRgb(128, 128, 128); // 회색
                    break;
            }

            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            materialGroup.Children.Add(new SpecularMaterial(Brushes.White, 20));

            return materialGroup;
        }

        private Model3D CreateBoundingBox(StoreObject obj)
        {
            var width = obj.Width / 12.0;
            var depth = obj.Length / 12.0;
            var height = obj.Height / 12.0;

            // Bounding box를 나타낼 Model3D를 생성합니다.
            // 이전 AddTube 방식에서 더 간단하고 일반적으로 사용되는 LinesVisual3D를 직접 활용합니다.
            // LinesVisual3D는 ModelVisual3D의 Content로 직접 추가할 수 없습니다.
            // 대신 BoundingBoxWireFrameVisual3D 또는 Custom GeometryModel3D로 라인을 생성해야 합니다.
            // 여기서는 LinesVisual3D를 활용하여 GeometryModel3D를 구성하는 방식으로 수정합니다.

            // 8개의 꼭짓점 정의
            Point3D[] vertices = new Point3D[8];
            vertices[0] = new Point3D(obj.Position.X / 12.0, obj.Position.Y / 12.0, 0);
            vertices[1] = new Point3D(obj.Position.X / 12.0 + width, obj.Position.Y / 12.0, 0);
            vertices[2] = new Point3D(obj.Position.X / 12.0 + width, obj.Position.Y / 12.0 + depth, 0);
            vertices[3] = new Point3D(obj.Position.X / 12.0, obj.Position.Y / 12.0 + depth, 0);

            vertices[4] = new Point3D(obj.Position.X / 12.0, obj.Position.Y / 12.0, height);
            vertices[5] = new Point3D(obj.Position.X / 12.0 + width, obj.Position.Y / 12.0, height);
            vertices[6] = new Point3D(obj.Position.X / 12.0 + width, obj.Position.Y / 12.0 + depth, height);
            vertices[7] = new Point3D(obj.Position.X / 12.0, obj.Position.Y / 12.0 + depth, height);

            // 12개의 선을 MeshGeometry3D로 구성
            var lineMesh = new MeshGeometry3D();
            var positions = new Point3DCollection();
            var indices = new Int32Collection();

            double lineThickness = 0.05; // 선의 두께 (피트 단위)

            // 아래쪽 면
            AddLineGeometry(positions, indices, vertices[0], vertices[1], lineThickness);
            AddLineGeometry(positions, indices, vertices[1], vertices[2], lineThickness);
            AddLineGeometry(positions, indices, vertices[2], vertices[3], lineThickness);
            AddLineGeometry(positions, indices, vertices[3], vertices[0], lineThickness);

            // 위쪽 면
            AddLineGeometry(positions, indices, vertices[4], vertices[5], lineThickness);
            AddLineGeometry(positions, indices, vertices[5], vertices[6], lineThickness);
            AddLineGeometry(positions, indices, vertices[6], vertices[7], lineThickness);
            AddLineGeometry(positions, indices, vertices[7], vertices[4], lineThickness);

            // 수직선
            AddLineGeometry(positions, indices, vertices[0], vertices[4], lineThickness);
            AddLineGeometry(positions, indices, vertices[1], vertices[5], lineThickness);
            AddLineGeometry(positions, indices, vertices[2], vertices[6], lineThickness);
            AddLineGeometry(positions, indices, vertices[3], vertices[7], lineThickness);

            lineMesh.Positions = positions;
            lineMesh.TriangleIndices = indices;

            var material = new EmissiveMaterial(Brushes.Yellow);
            var boundingBoxModel = new GeometryModel3D(lineMesh, material);

            return boundingBoxModel;
        }

        // 두 점 사이에 원통형 선을 그리는 헬퍼 메서드
        private void AddLineGeometry(Point3DCollection positions, Int32Collection indices, Point3D p1, Point3D p2, double diameter)
        {
            var builder = new MeshBuilder();
            builder.AddCylinder(p1, p2, diameter / 2, 8); // 지름을 반지름으로 변환

            // 생성된 메쉬를 현재 Positions와 Indices에 추가
            int startIdx = positions.Count;
            foreach (var pos in builder.Positions)
            {
                positions.Add(pos);
            }
            foreach (var triIdx in builder.TriangleIndices)
            {
                indices.Add(startIdx + triIdx);
            }
        }


        // 마우스 이벤트 핸들러
        private void ViewportMain_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var camera = CameraMain;
            var delta = e.Delta > 0 ? 0.9 : 1.1;

            var lookDirection = camera.LookDirection;
            lookDirection.Normalize();

            camera.Position = camera.Position + lookDirection * (1 - delta) * 5;

            e.Handled = true;
        }

        // 카메라 리셋
        public void ResetCamera()
        {
            CameraMain.Position = new Point3D(50, 50, 50);
            CameraMain.LookDirection = new Vector3D(-50, -50, -50);
            CameraMain.UpDirection = new Vector3D(0, 0, 1);
        }

        // 이 LoadModel 메서드는 DrawingService에서 제거되었으므로,
        // 필요하다면 TryLoadObjModel 또는 다른 3D 모델 로딩 메서드를 사용하도록 리팩토링해야 합니다.
        // 현재는 직접적으로 호출되는 곳이 없으므로 제거해도 무방합니다.
        /*
        private Model3D LoadModel(string modelPath)
        {
            try
            {
                if (System.IO.File.Exists(modelPath))
                {
                    var objReader = new ObjReader();
                    var model3DGroup = objReader.Read(modelPath);
                    return model3DGroup;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"모델 로드 실패: {ex.Message}");
            }
            return null;
        }
        */
    }
}