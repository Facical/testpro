using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using testpro.Models;

namespace testpro.Dialogs
{
    public partial class ObjectTypeSelectionDialog : Window
    {
        // 객체 타입 정보 클래스
        public class ObjectTypeInfo
        {
            public DetectedObjectType Type { get; set; }
            public string Name { get; set; }
            public string Icon { get; set; }
            public string Description { get; set; }
            public string ModelPath { get; set; }
            public bool HasLayers { get; set; }
            public bool HasTemperature { get; set; }
        }

        // 결과 속성들
        public DetectedObjectType SelectedType { get; private set; }
        public double ObjectWidth { get; private set; }
        public double ObjectLength { get; private set; }
        public double ObjectHeight { get; private set; }
        public int ObjectLayers { get; private set; }
        public bool IsHorizontal { get; private set; }
        public double Temperature { get; private set; }
        public string CategoryCode { get; private set; }

        private List<ObjectTypeInfo> objectTypes;
        private ObjectTypeInfo selectedTypeInfo;
        private int currentStep = 1;

        public ObjectTypeSelectionDialog()
        {
            InitializeComponent();
            InitializeObjectTypes();
            SetupEventHandlers();
            UpdateStepVisual();
        }

        private void InitializeObjectTypes()
        {
            objectTypes = new List<ObjectTypeInfo>
    {
        new ObjectTypeInfo
        {
            Type = DetectedObjectType.Shelf,
            Name = "선반/진열대",
            Icon = "📦",
            Description = "다층 진열이 가능한 선반",
            ModelPath = "display_rack_shelf.gltf",
            HasLayers = true,
            HasTemperature = false
        },
        new ObjectTypeInfo
        {
            Type = DetectedObjectType.DisplayRackDouble,
            Name = "양면 진열대",
            Icon = "📚",
            Description = "양쪽에서 접근 가능한 진열대",
            ModelPath = "display_rack_double.gltf",
            HasLayers = true,
            HasTemperature = false
        },
        new ObjectTypeInfo
        {
            Type = DetectedObjectType.Refrigerator,
            Name = "오픈형 냉장고",
            Icon = "❄️",
            Description = "음료 냉장고 (문 없음)",
            ModelPath = "refrigerator_open.gltf",
            HasLayers = true,
            HasTemperature = true
        },
        new ObjectTypeInfo
        {
            Type = DetectedObjectType.RefrigeratorWall,
            Name = "벽면 냉장고",
            Icon = "🥤",
            Description = "유리문이 있는 대형 냉장고",
            ModelPath = "refrigerator_wall.gltf",
            HasLayers = true,
            HasTemperature = true
        },
        new ObjectTypeInfo
        {
            Type = DetectedObjectType.Freezer,
            Name = "냉동고",
            Icon = "🧊",
            Description = "냉동식품 보관",
            ModelPath = "freezer.gltf",
            HasLayers = true,
            HasTemperature = true
        },
        new ObjectTypeInfo
        {
            Type = DetectedObjectType.FreezerChest,
            Name = "평면 냉동고",
            Icon = "🍦",
            Description = "아이스크림 냉동고",
            ModelPath = "freezer_chest.gltf",
            HasLayers = false,
            HasTemperature = true
        },
        // ... 나머지 타입들
    };
        }

        private void SetupEventHandlers()
        {
            // 층수 슬라이더 변경 이벤트
            LayersSlider.ValueChanged += (s, e) =>
            {
                int layers = (int)LayersSlider.Value;
                LayersText.Text = $"{layers}층";
                UpdateLayerSpacing();
            };

            // 높이 텍스트박스 변경 이벤트
            HeightTextBox.TextChanged += (s, e) => UpdateLayerSpacing();

            // 크기 텍스트박스 변경 이벤트들
            WidthTextBox.TextChanged += (s, e) => UpdateSizeDisplay(WidthTextBox, WidthFeetText);
            LengthTextBox.TextChanged += (s, e) => UpdateSizeDisplay(LengthTextBox, LengthFeetText);
            HeightTextBox.TextChanged += (s, e) => UpdateSizeDisplay(HeightTextBox, HeightFeetText);

            // 온도 텍스트박스 변경 이벤트
            TemperatureTextBox.TextChanged += (s, e) => UpdateTemperatureDisplay();
        }

        private void UpdateSizeDisplay(TextBox textBox, TextBlock displayText)
        {
            if (double.TryParse(textBox.Text, out double inches))
            {
                double feet = inches / 12.0;
                displayText.Text = $"({feet:F1}ft)";
            }
            else
            {
                displayText.Text = "(?)";
            }
        }

        private void UpdateLayerSpacing()
        {
            if (double.TryParse(HeightTextBox.Text, out double height))
            {
                int layers = (int)LayersSlider.Value;
                double spacing = height / layers;
                LayerSpacingText.Text = $"{spacing:F1}인치";
            }
        }

        private void UpdateTemperatureDisplay()
        {
            if (double.TryParse(TemperatureTextBox.Text, out double celsius))
            {
                double fahrenheit = celsius * 9 / 5 + 32;
                TemperatureFahrenheitText.Text = $"({fahrenheit:F1}°F)";
            }
        }

        private void UpdateStepVisual()
        {
            // 단계별 UI 표시 업데이트
            if (currentStep == 1)
            {
                Step1Border.Background = new SolidColorBrush(Colors.DodgerBlue);
                Step2Border.Background = new SolidColorBrush(Colors.LightGray);

                Step1Panel.Visibility = Visibility.Visible;
                Step2Panel.Visibility = Visibility.Collapsed;

                BackButton.Visibility = Visibility.Collapsed;
                NextButton.Content = "다음";
            }
            else
            {
                Step1Border.Background = new SolidColorBrush(Colors.LightGray);
                Step2Border.Background = new SolidColorBrush(Colors.DodgerBlue);

                Step1Panel.Visibility = Visibility.Collapsed;
                Step2Panel.Visibility = Visibility.Visible;

                BackButton.Visibility = Visibility.Visible;
                NextButton.Content = "완료";

                // 선택된 타입에 따라 UI 조정
                ConfigureStep2UI();
            }
        }

        private void ConfigureStep2UI()
        {
            if (selectedTypeInfo == null) return;

            PreviewText.Text = $"{selectedTypeInfo.Name} - {selectedTypeInfo.ModelPath}";
            LayersGroup.Visibility = selectedTypeInfo.HasLayers ? Visibility.Visible : Visibility.Collapsed;
            TemperatureGroup.Visibility = selectedTypeInfo.HasTemperature ? Visibility.Visible : Visibility.Collapsed;

            // 타입별 기본값 설정
            switch (selectedTypeInfo.Type)
            {
                case DetectedObjectType.DisplayRackDouble:
                    WidthTextBox.Text = "48";
                    LengthTextBox.Text = "36";  // 양면이므로 더 깊음
                    HeightTextBox.Text = "72";
                    LayersSlider.Value = 5;
                    LayersSlider.Maximum = 6;
                    break;

                case DetectedObjectType.RefrigeratorWall:
                    WidthTextBox.Text = "96";  // 벽면형은 더 김
                    LengthTextBox.Text = "24";
                    HeightTextBox.Text = "84";
                    LayersSlider.Value = 6;
                    LayersSlider.Maximum = 7;
                    TemperatureTextBox.Text = "4";
                    break;

                case DetectedObjectType.FreezerChest:
                    WidthTextBox.Text = "72";
                    LengthTextBox.Text = "36";
                    HeightTextBox.Text = "36";  // 낮은 높이
                    TemperatureTextBox.Text = "-18";
                    break;

                case DetectedObjectType.Refrigerator:
                    WidthTextBox.Text = "36";
                    LengthTextBox.Text = "24";
                    HeightTextBox.Text = "84";
                    LayersSlider.Value = 5;  // 음료 냉장고는 보통 5-6층
                    LayersSlider.Maximum = 7;
                    TemperatureTextBox.Text = "4";
                    break;

                    // ... 기존 케이스들
            }

            SetDefaultCategory();
        }

        private void SetDefaultCategory()
        {
            switch (selectedTypeInfo.Type)
            {
                case DetectedObjectType.Refrigerator:
                    CategoryCombo.SelectedIndex = 1; // 음료
                    break;
                case DetectedObjectType.Freezer:
                    CategoryCombo.SelectedIndex = 2; // 냉동식품
                    break;
                default:
                    CategoryCombo.SelectedIndex = 0; // 일반
                    break;
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep == 1)
            {
                // Step 1: 타입 선택 확인
                selectedTypeInfo = TypeListBox.SelectedItem as ObjectTypeInfo;
                if (selectedTypeInfo == null)
                {
                    MessageBox.Show("객체 타입을 선택하세요.", "선택 필요",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Step 2로 이동
                currentStep = 2;
                UpdateStepVisual();
            }
            else
            {
                // Step 2: 속성 검증 및 완료
                if (!ValidateInputs())
                    return;

                // 결과 저장
                SelectedType = selectedTypeInfo.Type;
                ObjectWidth = double.Parse(WidthTextBox.Text);
                ObjectLength = double.Parse(LengthTextBox.Text);
                ObjectHeight = double.Parse(HeightTextBox.Text);
                ObjectLayers = selectedTypeInfo.HasLayers ? (int)LayersSlider.Value : 1;
                IsHorizontal = OrientationCombo.SelectedIndex == 0;

                if (selectedTypeInfo.HasTemperature)
                {
                    Temperature = double.Parse(TemperatureTextBox.Text);
                }

                var selectedCategory = (CategoryCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                CategoryCode = GetCategoryCode(selectedCategory);

                DialogResult = true;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep == 2)
            {
                currentStep = 1;
                UpdateStepVisual();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private bool ValidateInputs()
        {
            // 너비 검증
            if (!double.TryParse(WidthTextBox.Text, out double width) || width <= 0)
            {
                MessageBox.Show("올바른 너비를 입력하세요.", "입력 오류",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                WidthTextBox.Focus();
                return false;
            }

            // 깊이 검증
            if (!double.TryParse(LengthTextBox.Text, out double length) || length <= 0)
            {
                MessageBox.Show("올바른 깊이를 입력하세요.", "입력 오류",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                LengthTextBox.Focus();
                return false;
            }

            // 높이 검증
            if (!double.TryParse(HeightTextBox.Text, out double height) || height <= 0)
            {
                MessageBox.Show("올바른 높이를 입력하세요.", "입력 오류",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                HeightTextBox.Focus();
                return false;
            }

            // 온도 검증 (해당되는 경우)
            if (selectedTypeInfo.HasTemperature)
            {
                if (!double.TryParse(TemperatureTextBox.Text, out double temp))
                {
                    MessageBox.Show("올바른 온도를 입력하세요.", "입력 오류",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    TemperatureTextBox.Focus();
                    return false;
                }
            }

            return true;
        }

        private string GetCategoryCode(string categoryName)
        {
            switch (categoryName)
            {
                case "음료": return "BEV";
                case "냉동식품": return "FRZ";
                case "유제품": return "DRY";
                case "신선식품": return "FRS";
                case "생활용품": return "HOM";
                default: return "GEN";
            }
        }

        // 외부에서 DetectedObjectType 열거형 변환을 위한 메서드
        public static ObjectType ConvertToObjectType(DetectedObjectType detectedType)
        {
            switch (detectedType)
            {
                case DetectedObjectType.Shelf:
                    return ObjectType.Shelf;
                case DetectedObjectType.Refrigerator:
                    return ObjectType.Refrigerator;
                case DetectedObjectType.Freezer:
                    return ObjectType.Freezer;
                case DetectedObjectType.Checkout:
                    return ObjectType.Checkout;
                case DetectedObjectType.DisplayStand:
                    return ObjectType.DisplayStand;
                case DetectedObjectType.Pillar:
                    return ObjectType.Pillar;
                default:
                    return ObjectType.Shelf;
            }
        }
    }
}