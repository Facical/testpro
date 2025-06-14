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
using HelixToolkit.Wpf;
using testpro.Models;
using testpro.ViewModels;
using HelixToolkit.Wpf.SharpDX.Cameras;

namespace testpro.Views
{
    public partial class Viewer3D : UserControl
    {
        private MainViewModel _viewModel;
        private ModelVisual3D _modelsContainer;
        private DirectionalLight _directionalLight;
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
                Width = 200,
                Length = 200,
                MinorDistance = 10,
                MajorDistance = 50,
                Thickness = 0.1
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
                    var modelGroup = new Model3DGroup();
                    modelGroup.Children.Add(model3D);

                    // 선택된 객체 강조
                    if (obj.IsSelected)
                    {
                        var boundingBox = CreateBoundingBox(obj);
                        if (boundingBox != null)
                            modelGroup.Children.Add(boundingBox);
                    }

                    var visual = new ModelVisual3D { Content = modelGroup };
                    visual.SetValue(FrameworkElement.TagProperty, obj.Id);
                    _modelsContainer.Children.Add(visual);
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

                // 캐시 확인
                string cacheKey = $"{obj.ModelPath}_{obj.Type}";
                if (_modelCache.ContainsKey(cacheKey))
                {
                    var cachedModel = _modelCache[cacheKey].Clone() as GeometryModel3D;
                    ApplyTransform(cachedModel, obj);
                    return cachedModel;
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
                var model3DGroup = objReader.Read(modelPath);

                if (model3DGroup == null || model3DGroup.Children.Count == 0)
                    return null;

                // 모든 GeometryModel3D를 하나로 결합
                var combinedMesh = new MeshBuilder();
                Material material = null;

                foreach (var child in model3DGroup.Children)
                {
                    if (child is GeometryModel3D gm && gm.Geometry is MeshGeometry3D mesh)
                    {
                        combinedMesh.Append(mesh);
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

                // 캐시에 저장
                _modelCache[cacheKey] = geometryModel.Clone();

                // 변환 적용
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

            // 크기 조정 (단위 변환)
            double scaleX = obj.Width / 36.0;  // 기본 크기 기준
            double scaleY = obj.Length / 24.0;
            double scaleZ = obj.Height / 72.0;

            transformGroup.Children.Add(new ScaleTransform3D(scaleX, scaleY, scaleZ));

            // 회전 적용
            if (Math.Abs(obj.Rotation) > 0.01)
            {
                transformGroup.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(new Vector3D(0, 0, 1), obj.Rotation)));
            }

            // 위치 이동
            transformGroup.Children.Add(new TranslateTransform3D(
                obj.Position.X / 12.0,
                obj.Position.Y / 12.0,
                0));

            model.Transform = transformGroup;
        }

        private GeometryModel3D CreateDefaultModel(StoreObject obj)
        {
            var meshBuilder = new MeshBuilder();

            var width = obj.Width / 12.0;
            var depth = obj.Length / 12.0;
            var height = obj.Height / 12.0;

            switch (obj.Type)
            {
                case ObjectType.Refrigerator:
                case ObjectType.Freezer:
                    // 냉장고 모양
                    meshBuilder.AddBox(new Point3D(0, 0, height / 2), width, depth, height);
                    // 문 핸들
                    meshBuilder.AddBox(new Point3D(width / 2 - 0.1, 0, height / 2), 0.1, 0.5, 0.1);
                    break;

                case ObjectType.Shelf:
                    // 선반 프레임
                    var frameThickness = 0.1;
                    meshBuilder.AddBox(new Point3D(0, 0, frameThickness / 2), width, depth, frameThickness);
                    meshBuilder.AddBox(new Point3D(0, 0, height - frameThickness / 2), width, depth, frameThickness);

                    // 층 추가
                    if (obj.Layers > 0)
                    {
                        var layerHeight = height / (obj.Layers + 1);
                        for (int i = 1; i <= obj.Layers; i++)
                        {
                            meshBuilder.AddBox(new Point3D(0, 0, i * layerHeight), width, depth, frameThickness);
                        }
                    }
                    break;

                default:
                    meshBuilder.AddBox(new Point3D(0, 0, height / 2), width, depth, height);
                    break;
            }

            var mesh = meshBuilder.ToMesh();
            var material = GetMaterialForType(obj.Type);

            var model = new GeometryModel3D(mesh, material);
            model.BackMaterial = material;

            // 변환 적용
            var transform = new Transform3DGroup();
            transform.Children.Add(new TranslateTransform3D(
                obj.Position.X / 12.0 + width / 2,
                obj.Position.Y / 12.0 + depth / 2,
                0));

            if (Math.Abs(obj.Rotation) > 0.01)
            {
                transform.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(new Vector3D(0, 0, 1), obj.Rotation),
                    new Point3D(obj.Position.X / 12.0 + width / 2, obj.Position.Y / 12.0 + depth / 2, 0)));
            }

            model.Transform = transform;

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

            var lineBuilder = new LinesVisual3D
            {
                Color = Colors.Yellow,
                Thickness = 2
            };

            var center = new Point3D(
                obj.Position.X / 12.0 + width / 2,
                obj.Position.Y / 12.0 + depth / 2,
                height / 2);

            // 박스 라인 그리기
            var boxBuilder = new MeshBuilder();
            boxBuilder.AddBoundingBox(
                new Rect3D(center.X - width / 2, center.Y - depth / 2, 0, width, depth, height),
                0.05);

            var material = new EmissiveMaterial(Brushes.Yellow);
            return new GeometryModel3D(boxBuilder.ToMesh(), material);
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
    }
}