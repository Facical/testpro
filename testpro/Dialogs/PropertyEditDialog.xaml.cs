using System;
using System.Windows;
using System.Windows.Controls;
using testpro.Models;

namespace testpro.Dialogs
{
    public partial class PropertyEditDialog : Window
    {
        private StoreObject _storeObject;

        public PropertyEditDialog(StoreObject storeObject)
        {
            InitializeComponent();
            _storeObject = storeObject;
            LoadObjectProperties();
        }

        private void LoadObjectProperties()
        {
            // 기본 정보
            TypeText.Text = _storeObject.GetDisplayName();
            WidthTextBox.Text = (_storeObject.Width / 12.0).ToString("F1");
            LengthTextBox.Text = (_storeObject.Length / 12.0).ToString("F1");
            HeightTextBox.Text = (_storeObject.Height / 12.0).ToString("F1");
            CategoryCodeTextBox.Text = _storeObject.CategoryCode;

            // 회전
            RotationSlider.Value = _storeObject.Rotation;
            RotationText.Text = $"{_storeObject.Rotation:F0}°";

            // 방향
            OrientationCheckBox.IsChecked = _storeObject.IsHorizontal;

            // 층수 (지원하는 경우)
            if (_storeObject.HasLayerSupport)
            {
                LayersPanel.Visibility = Visibility.Visible;
                LayersSlider.Value = _storeObject.Layers;
                LayersText.Text = _storeObject.Layers.ToString();
            }
            else
            {
                LayersPanel.Visibility = Visibility.Collapsed;
            }

            // 온도 (냉장고/냉동고)
            if (_storeObject.Type == ObjectType.Refrigerator || _storeObject.Type == ObjectType.Freezer)
            {
                TemperaturePanel.Visibility = Visibility.Visible;
                TemperatureSlider.Value = _storeObject.Temperature;
                TemperatureText.Text = $"{_storeObject.Temperature:F0}°C";

                // 온도 범위 설정
                if (_storeObject.Type == ObjectType.Freezer)
                {
                    TemperatureSlider.Minimum = -25;
                    TemperatureSlider.Maximum = -10;
                }
                else
                {
                    TemperatureSlider.Minimum = 0;
                    TemperatureSlider.Maximum = 10;
                }
            }
            else
            {
                TemperaturePanel.Visibility = Visibility.Collapsed;
            }

            // 메타데이터
            CreatedText.Text = _storeObject.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            ModifiedText.Text = _storeObject.ModifiedAt.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RotationText != null)
                RotationText.Text = $"{e.NewValue:F0}°";
        }

        private void LayersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LayersText != null)
                LayersText.Text = ((int)e.NewValue).ToString();
        }

        private void TemperatureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TemperatureText != null)
                TemperatureText.Text = $"{e.NewValue:F0}°C";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 크기 업데이트
                _storeObject.Width = double.Parse(WidthTextBox.Text) * 12;
                _storeObject.Length = double.Parse(LengthTextBox.Text) * 12;
                _storeObject.Height = double.Parse(HeightTextBox.Text) * 12;

                // 카테고리 코드
                _storeObject.CategoryCode = CategoryCodeTextBox.Text;

                // 회전
                _storeObject.Rotation = RotationSlider.Value;

                // 방향
                _storeObject.IsHorizontal = OrientationCheckBox.IsChecked ?? true;

                // 층수
                if (_storeObject.HasLayerSupport)
                {
                    _storeObject.Layers = (int)LayersSlider.Value;
                }

                // 온도
                if (_storeObject.Type == ObjectType.Refrigerator || _storeObject.Type == ObjectType.Freezer)
                {
                    _storeObject.Temperature = TemperatureSlider.Value;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"입력 오류: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}