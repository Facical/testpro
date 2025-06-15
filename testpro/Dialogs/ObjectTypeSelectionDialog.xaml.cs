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

        public ObjectTypeSelectionDialog(StoreObject objectToEdit)
        {
            InitializeComponent();
            InitializeObjectTypes();
            SetupEventHandlers();

            PopulateControlsForEdit(objectToEdit);

            currentStep = 2;
            UpdateStepVisual();
        }

        private void PopulateControlsForEdit(StoreObject obj)
        {
            SelectedType = ConvertToDetectedType(obj.Type);
            selectedTypeInfo = objectTypes.FirstOrDefault(t => t.Type == SelectedType);
            if (selectedTypeInfo == null) return;

            TypeListBox.SelectedItem = selectedTypeInfo;
            TypeListBox.IsEnabled = false;

            WidthTextBox.Text = obj.Width.ToString();
            LengthTextBox.Text = obj.Length.ToString();
            HeightTextBox.Text = obj.Height.ToString();
            OrientationCombo.SelectedIndex = obj.IsHorizontal ? 0 : 1;

            if (selectedTypeInfo.HasLayers)
            {
                LayersSlider.Value = obj.Layers;
            }
            if (selectedTypeInfo.HasTemperature)
            {
                TemperatureTextBox.Text = obj.Temperature.ToString();
            }
            SetCategoryByCode(obj.CategoryCode);
        }

        private void InitializeObjectTypes()
        {
            objectTypes = new List<ObjectTypeInfo>
            {
                new ObjectTypeInfo { Type = DetectedObjectType.Shelf, Name = "선반/진열대", Icon = "📦", Description = "다층 진열이 가능한 선반", ModelPath = "display_rack_shelf.obj", HasLayers = true, HasTemperature = false },
                new ObjectTypeInfo { Type = DetectedObjectType.Refrigerator, Name = "냉장고", Icon = "❄️", Description = "음료 및 냉장 제품 보관", ModelPath = "beverage_refrigerator.obj", HasLayers = true, HasTemperature = true },
                new ObjectTypeInfo { Type = DetectedObjectType.Freezer, Name = "냉동고", Icon = "🧊", Description = "아이스크림 및 냉동식품 보관", ModelPath = "freezer.obj", HasLayers = true, HasTemperature = true },
                new ObjectTypeInfo { Type = DetectedObjectType.Checkout, Name = "계산대", Icon = "💳", Description = "고객 계산 처리 공간", ModelPath = "checkout.obj", HasLayers = false, HasTemperature = false },
                new ObjectTypeInfo { Type = DetectedObjectType.DisplayStand, Name = "진열대", Icon = "🏪", Description = "특별 진열용 스탠드", ModelPath = "display_stand_pillar.obj", HasLayers = true, HasTemperature = false },
                new ObjectTypeInfo { Type = DetectedObjectType.Pillar, Name = "기둥", Icon = "🏛️", Description = "구조물 기둥", ModelPath = "pillar.obj", HasLayers = false, HasTemperature = false }
            };

            TypeListBox.ItemsSource = objectTypes;
            TypeListBox.SelectedIndex = 0;
        }

        private void SetupEventHandlers()
        {
            LayersSlider.ValueChanged += (s, e) => { if (LayersText != null) LayersText.Text = $"{(int)LayersSlider.Value}층"; UpdateLayerSpacing(); };
            HeightTextBox.TextChanged += (s, e) => UpdateLayerSpacing();
            WidthTextBox.TextChanged += (s, e) => UpdateSizeDisplay(WidthTextBox, WidthFeetText);
            LengthTextBox.TextChanged += (s, e) => UpdateSizeDisplay(LengthTextBox, LengthFeetText);
            HeightTextBox.TextChanged += (s, e) => UpdateSizeDisplay(HeightTextBox, HeightFeetText);
            TemperatureTextBox.TextChanged += (s, e) => UpdateTemperatureDisplay();
        }

        private void UpdateSizeDisplay(TextBox textBox, TextBlock displayText)
        {
            if (displayText == null) return;
            if (double.TryParse(textBox.Text, out double inches)) displayText.Text = $"({inches / 12.0:F1}ft)";
            else displayText.Text = "(?)";
        }

        private void UpdateLayerSpacing()
        {
            if (LayerSpacingText == null) return;
            if (double.TryParse(HeightTextBox.Text, out double height) && LayersSlider.Value > 0)
            {
                LayerSpacingText.Text = $"{height / (int)LayersSlider.Value:F1}인치";
            }
        }

        private void UpdateTemperatureDisplay()
        {
            if (TemperatureFahrenheitText == null) return;
            if (double.TryParse(TemperatureTextBox.Text, out double celsius))
            {
                TemperatureFahrenheitText.Text = $"({celsius * 9 / 5 + 32:F1}°F)";
            }
        }

        private void UpdateStepVisual()
        {
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
                ConfigureStep2UI();
            }
        }

        private void ConfigureStep2UI()
        {
            if (selectedTypeInfo == null) return;
            PreviewText.Text = $"{selectedTypeInfo.Name} - {selectedTypeInfo.ModelPath}";
            LayersGroup.Visibility = selectedTypeInfo.HasLayers ? Visibility.Visible : Visibility.Collapsed;
            TemperatureGroup.Visibility = selectedTypeInfo.HasTemperature ? Visibility.Visible : Visibility.Collapsed;
            if (string.IsNullOrEmpty(WidthTextBox.Text)) SetDefaultValues();
        }

        private void SetDefaultValues()
        {
            switch (selectedTypeInfo.Type)
            {
                case DetectedObjectType.Shelf: WidthTextBox.Text = "48"; LengthTextBox.Text = "18"; HeightTextBox.Text = "72"; LayersSlider.Value = 3; break;
                case DetectedObjectType.Refrigerator: WidthTextBox.Text = "36"; LengthTextBox.Text = "24"; HeightTextBox.Text = "84"; LayersSlider.Value = 2; TemperatureTextBox.Text = "4"; break;
                case DetectedObjectType.Freezer: WidthTextBox.Text = "36"; LengthTextBox.Text = "24"; HeightTextBox.Text = "84"; LayersSlider.Value = 3; TemperatureTextBox.Text = "-18"; break;
                case DetectedObjectType.Checkout: WidthTextBox.Text = "48"; LengthTextBox.Text = "36"; HeightTextBox.Text = "36"; break;
                case DetectedObjectType.DisplayStand: WidthTextBox.Text = "60"; LengthTextBox.Text = "30"; HeightTextBox.Text = "48"; LayersSlider.Value = 2; break;
                case DetectedObjectType.Pillar: WidthTextBox.Text = "12"; LengthTextBox.Text = "12"; HeightTextBox.Text = "96"; break;
            }
            SetDefaultCategory();
        }

        private void SetDefaultCategory()
        {
            switch (selectedTypeInfo.Type)
            {
                case DetectedObjectType.Refrigerator: CategoryCombo.SelectedIndex = 1; break;
                case DetectedObjectType.Freezer: CategoryCombo.SelectedIndex = 2; break;
                default: CategoryCombo.SelectedIndex = 0; break;
            }
        }

        private void SetCategoryByCode(string code)
        {
            CategoryCombo.SelectedIndex = code switch
            {
                "BEV" => 1,
                "FRZ" => 2,
                "DRY" => 3,
                "FRS" => 4,
                "HOM" => 5,
                _ => 0,
            };
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep == 1)
            {
                selectedTypeInfo = TypeListBox.SelectedItem as ObjectTypeInfo;
                if (selectedTypeInfo == null) { MessageBox.Show("객체 타입을 선택하세요."); return; }
                currentStep = 2;
                UpdateStepVisual();
            }
            else
            {
                if (!ValidateInputs()) return;
                SelectedType = selectedTypeInfo.Type;
                ObjectWidth = double.Parse(WidthTextBox.Text);
                ObjectLength = double.Parse(LengthTextBox.Text);
                ObjectHeight = double.Parse(HeightTextBox.Text);
                ObjectLayers = selectedTypeInfo.HasLayers ? (int)LayersSlider.Value : 1;
                IsHorizontal = OrientationCombo.SelectedIndex == 0;
                if (selectedTypeInfo.HasTemperature) Temperature = double.Parse(TemperatureTextBox.Text);
                CategoryCode = GetCategoryCode((CategoryCombo.SelectedItem as ComboBoxItem)?.Content.ToString());
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

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private bool ValidateInputs()
        {
            if (!double.TryParse(WidthTextBox.Text, out double w) || w <= 0) { MessageBox.Show("올바른 너비를 입력하세요."); return false; }
            if (!double.TryParse(LengthTextBox.Text, out double l) || l <= 0) { MessageBox.Show("올바른 깊이를 입력하세요."); return false; }
            if (!double.TryParse(HeightTextBox.Text, out double h) || h <= 0) { MessageBox.Show("올바른 높이를 입력하세요."); return false; }
            if (selectedTypeInfo.HasTemperature && !double.TryParse(TemperatureTextBox.Text, out double t)) { MessageBox.Show("올바른 온도를 입력하세요."); return false; }
            return true;
        }

        private string GetCategoryCode(string name) => name switch
        {
            "음료" => "BEV",
            "냉동식품" => "FRZ",
            "유제품" => "DRY",
            "신선식품" => "FRS",
            "생활용품" => "HOM",
            _ => "GEN",
        };

        public static ObjectType ConvertToObjectType(DetectedObjectType detectedType) => detectedType switch
        {
            DetectedObjectType.Shelf => ObjectType.Shelf,
            DetectedObjectType.Refrigerator => ObjectType.Refrigerator,
            DetectedObjectType.Freezer => ObjectType.Freezer,
            DetectedObjectType.Checkout => ObjectType.Checkout,
            DetectedObjectType.DisplayStand => ObjectType.DisplayStand,
            DetectedObjectType.Pillar => ObjectType.Pillar,
            _ => ObjectType.Shelf,
        };

        private DetectedObjectType ConvertToDetectedType(ObjectType storeType) => storeType switch
        {
            ObjectType.Shelf => DetectedObjectType.Shelf,
            ObjectType.Refrigerator => DetectedObjectType.Refrigerator,
            ObjectType.Freezer => DetectedObjectType.Freezer,
            ObjectType.Checkout => DetectedObjectType.Checkout,
            ObjectType.DisplayStand => DetectedObjectType.DisplayStand,
            ObjectType.Pillar => DetectedObjectType.Pillar,
            _ => DetectedObjectType.Unknown,
        };
    }
}