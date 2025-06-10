using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace testpro.Dialogs
{
    public partial class DimensionInputDialog : Window
    {
        public double WidthInInches { get; private set; }
        public double HeightInInches { get; private set; }

        public DimensionInputDialog()
        {
            InitializeComponent();
            WidthFeetTextBox.Focus();

            // 초기값으로 총 인치 계산
            UpdateTotalInches();

            // 텍스트 변경 이벤트 핸들러 추가
            WidthFeetTextBox.TextChanged += WidthTextBox_TextChanged;
            WidthInchesTextBox.TextChanged += WidthTextBox_TextChanged;
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // 숫자만 입력 가능하도록
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void WidthTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateTotalInches();
        }

        private void HeightTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateTotalInches();
        }

        private void UpdateTotalInches()
        {
            try
            {
                // 가로 총 인치 계산 (WidthTotalText가 있는 경우에만)
                var widthTotalText = FindName("WidthTotalText") as System.Windows.Controls.TextBlock;
                if (widthTotalText != null)
                {
                    if (int.TryParse(WidthFeetTextBox.Text, out int widthFeet) &&
                        int.TryParse(WidthInchesTextBox.Text, out int widthInches))
                    {
                        int totalWidthInches = widthFeet * 12 + widthInches;
                        widthTotalText.Text = $"(총 {totalWidthInches}인치)";
                    }
                    else
                    {
                        widthTotalText.Text = "(입력 오류)";
                    }
                }

                // 세로 총 인치 계산 (HeightTotalText가 있는 경우에만)
                var heightTotalText = FindName("HeightTotalText") as System.Windows.Controls.TextBlock;
                if (heightTotalText != null)
                {
                    if (int.TryParse(HeightFeetTextBox.Text, out int heightFeet) &&
                        int.TryParse(HeightInchesTextBox.Text, out int heightInches))
                    {
                        int totalHeightInches = heightFeet * 12 + heightInches;
                        heightTotalText.Text = $"(총 {totalHeightInches}인치)";
                    }
                    else
                    {
                        heightTotalText.Text = "(입력 오류)";
                    }
                }
            }
            catch
            {
                // 계산 오류 시 무시
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 가로 파싱
                if (!int.TryParse(WidthFeetTextBox.Text, out int widthFeet) || widthFeet < 0)
                {
                    MessageBox.Show("올바른 가로 피트 값을 입력하세요.", "입력 오류",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    WidthFeetTextBox.Focus();
                    WidthFeetTextBox.SelectAll();
                    return;
                }

                if (!int.TryParse(WidthInchesTextBox.Text, out int widthInches) || widthInches < 0 || widthInches >= 12)
                {
                    MessageBox.Show("올바른 가로 인치 값을 입력하세요. (0-11)", "입력 오류",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    WidthInchesTextBox.Focus();
                    WidthInchesTextBox.SelectAll();
                    return;
                }

                // 세로 파싱
                if (!int.TryParse(HeightFeetTextBox.Text, out int heightFeet) || heightFeet < 0)
                {
                    MessageBox.Show("올바른 세로 피트 값을 입력하세요.", "입력 오류",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    HeightFeetTextBox.Focus();
                    HeightFeetTextBox.SelectAll();
                    return;
                }

                if (!int.TryParse(HeightInchesTextBox.Text, out int heightInches) || heightInches < 0 || heightInches >= 12)
                {
                    MessageBox.Show("올바른 세로 인치 값을 입력하세요. (0-11)", "입력 오류",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    HeightInchesTextBox.Focus();
                    HeightInchesTextBox.SelectAll();
                    return;
                }

                // 인치로 변환
                WidthInInches = widthFeet * 12.0 + widthInches;
                HeightInInches = heightFeet * 12.0 + heightInches;

                if (WidthInInches <= 0 || HeightInInches <= 0)
                {
                    MessageBox.Show("크기는 0보다 커야 합니다.", "입력 오류",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 크기가 너무 작거나 큰 경우 경고
                if (WidthInInches < 120) // 10피트 미만
                {
                    var result = MessageBox.Show($"가로 크기가 {WidthInInches / 12:F1}피트로 매우 작습니다. 계속하시겠습니까?",
                                               "크기 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes) return;
                }

                if (HeightInInches < 120) // 10피트 미만
                {
                    var result = MessageBox.Show($"세로 크기가 {HeightInInches / 12:F1}피트로 매우 작습니다. 계속하시겠습니까?",
                                               "크기 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes) return;
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"입력 처리 중 오류가 발생했습니다:\n{ex.Message}", "오류",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}