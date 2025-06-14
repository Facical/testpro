using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using testpro.Models;
using testpro.ViewModels;

namespace testpro.Views
{
    public partial class Viewer3D : UserControl
    {
        private MainViewModel _viewModel;
        private ModelVisual3D _modelsContainer;
        private readonly Dictionary<string, Model3D> _modelCache = new Dictionary<string, Model3D>();

        public Viewer3D()
        {
            InitializeComponent();
            SetupViewer();
        }

        public MainViewModel ViewModel
        {
            get => _viewModel;
            set
            {
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    if (_viewModel.StoreObjects != null)
                    {
                        _viewModel.StoreObjects.CollectionChanged -= Objects_CollectionChanged;
                    }
                }

                _viewModel = value;
                DataContext = _viewModel;

                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                    if (_viewModel.StoreObjects != null)
                    {
                        _viewModel.StoreObjects.CollectionChanged += Objects_CollectionChanged;
                    }
                    UpdateView();
                }
            }
        }

        private void SetupViewer()
        {
            var lights = new Model3DGroup();
            lights.Children.Add(new AmbientLight(Color.FromRgb(100, 100, 100)));
            lights.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1.5, -2)));
            lights.Children.Add(new DirectionalLight(Color.FromRgb(150, 150, 150), new Vector3D(1, 1, 0.5)));
            ViewportMain.Children.Add(new ModelVisual3D { Content = lights });

            _modelsContainer = new ModelVisual3D();
            ViewportMain.Children.Add(_modelsContainer);

            ViewportMain.Children.Add(new GridLinesVisual3D
            {
                Width = 200,
                Length = 200,
                MinorDistance = 5,
                MajorDistance = 20,
                Thickness = 0.1
            });

            ResetCamera();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.StoreObjects) || e.PropertyName == nameof(MainViewModel.SelectedObject))
            {
                UpdateView();
            }
        }

        private void Objects_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateView();
        }

        public void UpdateView()
        {
            _modelsContainer.Children.Clear();
            if (_viewModel?.StoreObjects == null) return;

            foreach (var obj in _viewModel.StoreObjects)
            {
                var modelVisual = CreateObjectVisual(obj);
                if (modelVisual != null)
                {
                    _modelsContainer.Children.Add(modelVisual);
                }
            }
        }

        private ModelVisual3D CreateObjectVisual(StoreObject obj)
        {
            try
            {

                var modelGroup = new Model3DGroup();

                // 1. 주 모델(냉장고 몸체)을 로드하여 그룹에 추가합니다.
                var mainModel = GetOrLoadModel(obj.ModelPath) ?? CreateDefaultModel(obj);
                modelGroup.Children.Add(mainModel.Clone());

                // 2. 선반이 필요한 경우, 선반들을 '지역 변환'을 적용하여 그룹에 추가합니다.
                if (obj.Type == ObjectType.Refrigerator && obj.HasLayerSupport && obj.Layers > 0)
                {
                    AddShelves(modelGroup, obj);
                }

                ProcessTransparentMaterials(modelGroup);

                // 3. 완성된 그룹 전체에 '전역 변환'(크기, 회전, 위치)을 적용합니다.
                modelGroup.Transform = CreateWorldTransform(modelGroup, obj);

                // 4. 화면에 표시할 최종 시각적 요소를 생성합니다.
                var visual = new ModelVisual3D { Content = modelGroup };
                if (obj.IsSelected)
                {
                    var boundingBox = new BoundingBoxVisual3D
                    {
                        BoundingBox = modelGroup.Bounds,
                        Fill = new SolidColorBrush(Color.FromArgb(64, 255, 255, 0))
                    };
                    visual.Children.Add(boundingBox);
                }
                return visual;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error] Creating visual for {obj.Id}: {ex.Message}");
                return null;
            }
        }

        private void ProcessTransparentMaterials(Model3DGroup modelGroup)
        {
            var geometries = modelGroup.Children.OfType<GeometryModel3D>().ToList();

            // 자식 그룹이 있다면 재귀적으로 탐색
            var childGroups = modelGroup.Children.OfType<Model3DGroup>().ToList();
            foreach (var group in childGroups)
            {
                geometries.AddRange(group.Children.OfType<GeometryModel3D>());
            }

            foreach (var geo in geometries)
            {
                if (geo.Material is DiffuseMaterial diffuseMat)
                {
                    // .mtl 파일에서 유리 재질의 이름을 "Glass" 또는 "Transparent" 등으로 지정했다고 가정합니다.
                    // 해당 재질을 찾아 알파값을 코드에서 직접 설정합니다.
                    // **중요**: .mtl 파일에서 유리 재질의 이름을 확인하고 아래 "Glass" 부분을 실제 이름으로 바꿔주세요.
                    if (diffuseMat.GetName().Contains("TransparentGlass", StringComparison.OrdinalIgnoreCase))
                    {
                        // 새로운 반투명 재질 생성
                        var transparentBrush = new SolidColorBrush(Colors.LightGray) { Opacity = 0.3 }; // 30% 불투명도
                        var transparentMaterial = new DiffuseMaterial(transparentBrush);

                        // 기존 재질을 새로운 반투명 재질로 교체
                        geo.Material = transparentMaterial;
                        geo.BackMaterial = transparentMaterial; // 뒷면도 동일하게 적용
                    }
                }
            }
        }
        private void AddShelves(Model3DGroup parentGroup, StoreObject obj)
        {
            if (parentGroup.Children.Count == 0) return;
            var mainModelBounds = parentGroup.Children[0].Bounds;
            if (mainModelBounds.IsEmpty) return;

            var shelfModel = GetOrLoadModel(@"Models\Refrigerator\refrigerator_shelf.obj");
            if (shelfModel == null) return;

            // 냉장고 모델의 '지역' 좌표계 기준으로 선반 위치를 계산합니다.
            double localTotalHeight = mainModelBounds.SizeZ;
            double localBottomZ = mainModelBounds.Z;

            double bottomMargin = localTotalHeight * 0.05; // 하단 여유 공간 (모델 높이의 5%)
            double topMargin = localTotalHeight * 0.05;    // 상단 여유 공간 (모델 높이의 5%)
            double usableHeight = localTotalHeight - bottomMargin - topMargin;

            if (usableHeight <= 0) return;
            double spacing = usableHeight / (obj.Layers + 1);

            for (int i = 1; i <= obj.Layers; i++)
            {
                var shelfInstance = shelfModel.Clone();
                var shelfBounds = shelfInstance.Bounds;
                if (shelfBounds.IsEmpty) continue;

                var localTransform = new Transform3DGroup();

                // 선반의 X, Y 크기를 냉장고 내부에 맞게 조절 (예: 95% 크기)
                double scaleX = (mainModelBounds.SizeX * 0.95) / shelfBounds.SizeX;
                double scaleY = (mainModelBounds.SizeY * 0.95) / shelfBounds.SizeY;
                localTransform.Children.Add(new ScaleTransform3D(scaleX, scaleY, 1, shelfBounds.Location.X + shelfBounds.SizeX / 2, shelfBounds.Location.Y + shelfBounds.SizeY / 2, shelfBounds.Z));

                // 선반의 Z 위치를 계산하여 이동
                double targetZ = localBottomZ + bottomMargin + (i * spacing);
                localTransform.Children.Add(new TranslateTransform3D(0, 0, targetZ));

                shelfInstance.Transform = localTransform;
                parentGroup.Children.Add(shelfInstance);
            }
        }

        /// <summary>
        /// 객체 그룹 전체를 월드 공간에 배치하기 위한 최종 변환을 생성합니다.
        /// </summary>
        private Transform3D CreateWorldTransform(Model3DGroup modelGroup, StoreObject obj)
        {
            var transform = new Transform3DGroup();
            Rect3D bounds = modelGroup.Bounds;
            if (bounds.IsEmpty) return Transform3D.Identity;

            // 1. 크기 (Scale)
            double scaleX = bounds.SizeX > 0 ? ((obj.IsHorizontal ? obj.Width : obj.Length) / 12.0) / bounds.SizeX : 1;
            double scaleY = bounds.SizeY > 0 ? ((obj.IsHorizontal ? obj.Length : obj.Width) / 12.0) / bounds.SizeY : 1;
            double scaleZ = bounds.SizeZ > 0 ? (obj.Height / 12.0) / bounds.SizeZ : 1;
            transform.Children.Add(new ScaleTransform3D(scaleX, scaleY, scaleZ, bounds.Location.X + bounds.SizeX / 2, bounds.Location.Y + bounds.SizeY / 2, bounds.Z));

            // 2. 회전 (Rotate)
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), obj.Rotation), new Point3D(bounds.Location.X + bounds.SizeX / 2, bounds.Location.Y + bounds.SizeY / 2, 0)));

            // 3. 위치 (Translate)
            double worldX = (obj.Position.X + (obj.IsHorizontal ? obj.Width : obj.Length) / 2) / 12.0;
            double worldY = (obj.Position.Y + (obj.IsHorizontal ? obj.Length : obj.Width) / 2) / 12.0;
            double worldZ = -bounds.Z * scaleZ;
            transform.Children.Add(new TranslateTransform3D(worldX, worldY, worldZ));

            return transform;
        }

        private Model3D GetOrLoadModel(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath)) return null;
            if (_modelCache.TryGetValue(modelPath, out Model3D cachedModel))
            {
                return cachedModel;
            }

            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelPath);
            if (!File.Exists(fullPath))
            {
                Debug.WriteLine($"[Warning] Model file not found: {fullPath}");
                return null;
            }

            try
            {
                var reader = new ObjReader();
                var model = reader.Read(fullPath);
                model.Freeze();
                _modelCache[modelPath] = model;
                return model;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error] Failed to load model {modelPath}: {ex.Message}");
                return null;
            }
        }

        private GeometryModel3D CreateDefaultModel(StoreObject obj)
        {
            var meshBuilder = new MeshBuilder();
            meshBuilder.AddBox(new Point3D(0, 0, 0.5), 1, 1, 1);
            Color color = obj.Type switch
            {
                ObjectType.Shelf => Colors.Sienna,
                ObjectType.Refrigerator => Colors.LightSteelBlue,
                _ => Colors.Gray
            };
            return new GeometryModel3D(meshBuilder.ToMesh(), new DiffuseMaterial(new SolidColorBrush(color)));
        }

        public void ResetCamera()
        {
            CameraMain.Position = new Point3D(50, 80, 60);
            CameraMain.LookDirection = new Vector3D(-50, -80, -60);
            CameraMain.UpDirection = new Vector3D(0, 0, 1);
            CameraMain.FieldOfView = 60;
        }

        private void ViewportMain_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var newPosition = CameraMain.Position - (CameraMain.LookDirection * (e.Delta / 200.0));
            if (newPosition.Z > 1)
            {
                CameraMain.Position = newPosition;
            }
        }
    }
}