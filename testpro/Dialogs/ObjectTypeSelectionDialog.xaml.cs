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
            public testpro.Models.DetectedObjectType Type { get; set; }
            public string Name { get; set; }
            public string Icon { get; set; }
            public string Description { get; set; }
            public string ModelPath { get; set; }
            public bool HasLayers { get; set; }
            public bool HasTemperature { get; set; }
        }

        // 결과 속성들
        public testpro.Models.DetectedObjectType SelectedType { get; private set; }
        // public set 접근자로 변경하여 외부에서 초기값 설정 가능하게 함
        public double ObjectWidth { get; set; }
        public double ObjectLength { get; set; }
        public double ObjectHeight { get; set; }
        public int ObjectLayers { get; set; }
        public bool IsHorizontal { get; set; }
        public double Temperature { get; set; }
        public string CategoryCode { get; set; }

        private List<ObjectTypeInfo> objectTypes;
        private ObjectTypeInfo selectedTypeInfo;
        private int currentStep = 1;

        public ObjectTypeSelectionDialog()
        {
            InitializeComponent();
            InitializeObjectTypes();
            SetupEventHandlers();
            UpdateStepVisual();

            // 슬라이더의 기본값 설정 (UI 로드 후)
            // 여기에서 초기값을 설정하여 UI에 반영합니다.
            // DrawingCanvas에서 설정한 값이 이 기본값을 덮어씁니다.
            WidthSlider.Value = ObjectWidth / 12.0; // 인치를 피트로
            LengthSlider.Value = ObjectLength / 12.0;
            HeightSlider.Value = ObjectHeight / 12.0;
            LayersSlider.Value = ObjectLayers;
            HorizontalRadio.IsChecked = IsHorizontal;
            VerticalRadio.IsChecked = !IsHorizontal;
            TemperatureSlider.Value = Temperature;
            CategoryCombo.SelectedValue = CategoryCode; // ComboBox Item의 Content와 일치해야 함

            UpdateDimensionsText(); // 초기 텍스트 업데이트
            UpdateTemperatureText(); // 초기 온도 텍스트 업데이트
        }

        private void InitializeObjectTypes()
        {
            objectTypes = new List<ObjectTypeInfo>
            {
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.Shelf,
                    Name = "선반/진열대",
                    Icon = "📦",
                    Description = "다층 진열이 가능한 선반",
                    ModelPath = "display_rack_shelf.obj",
                    HasLayers = true,
                    HasTemperature = false
                },
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.Refrigerator,
                    Name = "냉장고",
                    Icon = "❄️",
                    Description = "음료 및 냉장 제품 보관",
                    ModelPath = "beverage_refrigerator.obj",
                    HasLayers = true,
                    HasTemperature = true
                },
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.Freezer,
                    Name = "냉동고",
                    Icon = "🧊",
                    Description = "아이스크림 및 냉동식품 보관",
                    ModelPath = "freezer.obj",
                    HasLayers = true,
                    HasTemperature = true
                },
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.Checkout,
                    Name = "계산대",
                    Icon = "💳",
                    Description = "고객 계산 처리 공간",
                    ModelPath = "checkout.obj",
                    HasLayers = false,
                    HasTemperature = false
                },
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.DisplayStand,
                    Name = "진열대",
                    Icon = "🏪",
                    Description = "특별 진열용 스탠드",
                    ModelPath = "display_stand_pillar.obj",
                    HasLayers = true,
                    HasTemperature = false
                },
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.Pillar,
                    Name = "기둥",
                    Icon = "🏛️",
                    Description = "구조물 기둥",
                    ModelPath = "pillar.obj",
                    HasLayers = false,
                    HasTemperature = false
                },
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.DisplayRackDouble,
                    Name = "양면진열대",
                    Icon = "📊", // 적절한 아이콘 선택
                    Description = "양면으로 진열 가능한 스탠드",
                    ModelPath = "display_rack_double.obj", // 적절한 모델 경로 지정
                    HasLayers = true,
                    HasTemperature = false
                },
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.RefrigeratorWall,
                    Name = "벽면 냉장고",
                    Icon = "🥶", // 적절한 아이콘 선택
                    Description = "벽면에 설치되는 냉장고",
                    ModelPath = "refrigerator_wall.obj", // 적절한 모델 경로 지정
                    HasLayers = true,
                    HasTemperature = true
                },
                new ObjectTypeInfo
                {
                    Type = testpro.Models.DetectedObjectType.FreezerChest,
                    Name = "평형 냉동고",
                    Icon = "🍦", // 적절한 아이콘 선택
                    Description = "평평한 형태의 냉동고",
                    ModelPath = "freezer_chest.obj", // 적절한 모델 경로 지정
                    HasLayers = true,
                    HasTemperature = true
                }
            };

            ObjectTypesList.ItemsSource = objectTypes;
        }

        private void SetupEventHandlers()
        {
            WidthSlider.ValueChanged += (s, e) => UpdateDimensionsText();
            LengthSlider.ValueChanged += (s, e) => UpdateDimensionsText();
            HeightSlider.ValueChanged += (s, e) => UpdateDimensionsText();
            LayersSlider.ValueChanged += (s, e) => LayersText.Text = ((int)e.NewValue).ToString();
            TemperatureSlider.ValueChanged += (s, e) => UpdateTemperatureText();
        }

        private void UpdateDimensionsText()
        {
            WidthText.Text = $"{WidthSlider.Value:F1}";
            LengthText.Text = $"{LengthSlider.Value:F1}";
            HeightText.Text = $"{HeightSlider.Value:F1}";
        }

        private void UpdateTemperatureText()
        {
            TemperatureText.Text = $"{TemperatureSlider.Value:F0}°C";
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

                    // 선택된 객체 타입의 기본 크기 또는 특성으로 슬라이더 초기화
                    // 단, DrawingCanvas에서 초기값을 설정했다면 그 값을 우선합니다.
                    if (ObjectWidth == 0 && ObjectHeight == 0 && ObjectLength == 0) // 초기값이 설정되지 않은 경우만
                    {
                        // ObjectType.Shelf의 기본값
                        double defaultWidth = 48; // 인치
                        double defaultLength = 18; // 인치
                        double defaultHeight = 72; // 인치
                        int defaultLayers = 3;
                        double defaultTemp = 4.0;

                        switch (selectedTypeInfo.Type)
                        {
                            case DetectedObjectType.Refrigerator:
                            case DetectedObjectType.RefrigeratorWall:
                                defaultWidth = 36; defaultLength = 24; defaultHeight = 72; defaultTemp = 4.0;
                                break;
                            case DetectedObjectType.Freezer:
                            case DetectedObjectType.FreezerChest:
                                defaultWidth = 36; defaultLength = 24; defaultHeight = 72; defaultTemp = -18.0;
                                break;
                            case DetectedObjectType.Checkout:
                                defaultWidth = 48; defaultLength = 36; defaultHeight = 36; defaultLayers = 1;
                                break;
                            case DetectedObjectType.DisplayStand:
                                defaultWidth = 60; defaultLength = 30; defaultHeight = 48; defaultLayers = 2;
                                break;
                            case DetectedObjectType.Pillar:
                                defaultWidth = 12; defaultLength = 12; defaultHeight = 96; defaultLayers = 1;
                                break;
                            case DetectedObjectType.Shelf:
                            case DetectedObjectType.DisplayRackDouble:
                            default: // 기본은 선반으로
                                break;
                        }
                        WidthSlider.Value = defaultWidth / 12.0; // 인치를 피트로 변환
                        LengthSlider.Value = defaultLength / 12.0;
                        HeightSlider.Value = defaultHeight / 12.0;
                        LayersSlider.Value = defaultLayers;
                        TemperatureSlider.Value = defaultTemp;
                        IsHorizontal = true; // 기본적으로 가로 방향
                    }
                    else
                    {
                        // DrawingCanvas에서 설정한 초기값을 반영
                        WidthSlider.Value = ObjectWidth / 12.0;
                        LengthSlider.Value = ObjectLength / 12.0;
                        HeightSlider.Value = ObjectHeight / 12.0;
                        LayersSlider.Value = ObjectLayers;
                        HorizontalRadio.IsChecked = IsHorizontal;
                        VerticalRadio.IsChecked = !IsHorizontal;
                        TemperatureSlider.Value = Temperature;
                    }
                    UpdateDimensionsText();
                    UpdateTemperatureText();
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

                // 객체 타입 선택 시, 현재 입력된 크기 값들을 초기화하지 않고
                // 다음 스텝으로 넘어갈 때 새로 선택된 타입의 기본값을 적용하거나,
                // DrawingCanvas에서 넘어온 값을 유지하도록 로직을 변경해야 합니다.
                // 현재는 이 메서드에서 명시적으로 초기화하지 않습니다.
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
                // 값 저장 (public set으로 변경된 속성에 최종 값 저장)
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