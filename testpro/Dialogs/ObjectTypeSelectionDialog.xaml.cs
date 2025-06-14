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
                    ModelPath = "display_rack_shelf.obj",
                    HasLayers = true,
                    HasTemperature = false
                },
                new ObjectTypeInfo
                {
                    Type = DetectedObjectType.Refrigerator,
                    Name = "냉장고",
                    Icon = "❄️",
                    Description = "음료 및 냉장 제품 보관",
                    ModelPath = "beverage_refrigerator.obj",
                    HasLayers = true,
                    HasTemperature = true
                },
                new ObjectTypeInfo
                {
                    Type = DetectedObjectType.Freezer,
                    Name = "냉동고",
                    Icon = "🧊",
                    Description = "아이스크림 및 냉동식품 보관",
                    ModelPath = "freezer.obj",
                    HasLayers = true,
                    HasTemperature = true
                },
                new ObjectTypeInfo
                {
                    Type = DetectedObjectType.Checkout,
                    Name = "계산대",
                    Icon = "💳",
                    Description = "고객 계산 처리 공간",
                    ModelPath = "checkout.obj",
                    HasLayers = false,
                    HasTemperature = false
                },
                new ObjectTypeInfo
                {
                    Type = DetectedObjectType.DisplayStand,
                    Name = "진열대",
                    Icon = "🏪",
                    Description = "특별 진열용 스탠드",
                    ModelPath = "display_stand_pillar.obj",
                    HasLayers = true,
                    HasTemperature = false
                },
                new ObjectTypeInfo
                {
                    Type = DetectedObjectType.Pillar,
                    Name = "기둥",
                    Icon = "🏛️",
                    Description = "구조물 기둥",
                    ModelPath = "pillar.obj",
                    HasLayers = false,
                    HasTemperature = false
                }
            };

            ObjectTypesList.ItemsSource = objectTypes;
        }

        private void SetupEventHandlers()
        {
            WidthSlider.ValueChanged += (s, e) => WidthText.Text = $"{e.NewValue:F1}";
            LengthSlider.ValueChanged += (s, e) => LengthText.Text = $"{e.NewValue:F1}";
            HeightSlider.ValueChanged += (s, e) => HeightText.Text = $"{e.NewValue:F1}";
            LayersSlider.ValueChanged += (s, e) => LayersText.Text = ((int)e.NewValue).ToString();
            TemperatureSlider.ValueChanged += (s, e) => TemperatureText.Text = $"{e.NewValue:F0}°C";
        }

        private void UpdateStepVisual()
        {
            if (currentStep == 1)
            {
                Step1Border.Background = Brushes.DodgerBlue;
                Step2Border.Background = Brushes.LightGray;
                Step1Panel.Visibility = Visibility.Visible;
                Step2Panel.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Collapsed;
                NextButton.Content = "다음";
                NextButton.IsEnabled = selectedTypeInfo != null;
            }
            else
            {
                Step1Border.Background = Brushes.LightGray;
                Step2Border.Background = Brushes.DodgerBlue;
                Step1Panel.Visibility = Visibility.Collapsed;
                Step2Panel.Visibility = Visibility.Visible;
                BackButton.Visibility = Visibility.Visible;
                NextButton.Content = "확인";
                NextButton.IsEnabled = true;

                // 선택된 타입에 따라 UI 조정
                if (selectedTypeInfo != null)
                {
                    LayersGroup.Visibility = selectedTypeInfo.HasLayers ? Visibility.Visible : Visibility.Collapsed;
                    TemperatureGroup.Visibility = selectedTypeInfo.HasTemperature ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void ObjectType_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ObjectTypeInfo typeInfo)
            {
                selectedTypeInfo = typeInfo;
                NextButton.IsEnabled = true;

                // 선택 표시
                foreach (Button btn in FindVisualChildren<Button>(ObjectTypesList))
                {
                    btn.Background = btn.Tag == typeInfo ?
                        new SolidColorBrush(Color.FromRgb(220, 240, 255)) :
                        Brushes.White;
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            currentStep = 1;
            UpdateStepVisual();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep == 1)
            {
                currentStep = 2;
                UpdateStepVisual();
            }
            else
            {
                // 값 저장
                SelectedType = selectedTypeInfo.Type;
                ObjectWidth = WidthSlider.Value * 12; // 피트를 인치로 변환
                ObjectLength = LengthSlider.Value * 12;
                ObjectHeight = HeightSlider.Value * 12;
                ObjectLayers = (int)LayersSlider.Value;
                IsHorizontal = HorizontalRadio.IsChecked ?? true;
                Temperature = TemperatureSlider.Value;

                // 카테고리 코드 추출
                if (CategoryCombo.SelectedItem is ComboBoxItem item)
                {
                    CategoryCode = item.Content.ToString().Split('-')[0].Trim();
                }
                else
                {
                    CategoryCode = "GEN";
                }

                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}