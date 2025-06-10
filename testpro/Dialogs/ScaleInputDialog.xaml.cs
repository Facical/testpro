// Dialogs/ScaleInputDialog.xaml.cs
using System;
using System.Windows;

namespace testpro.Dialogs
{
    public partial class ScaleInputDialog : Window
    {
        public double ActualLength { get; private set; }

        public ScaleInputDialog()
        {
            InitializeComponent();
            LengthTextBox.Focus();
            LengthTextBox.SelectAll();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double value = double.Parse(LengthTextBox.Text);

                // 인치로 변환
                if (UnitComboBox.SelectedIndex == 0) // 피트
                {
                    ActualLength = value * 12;
                }
                else // 인치
                {
                    ActualLength = value;
                }

                if (ActualLength <= 0)
                {
                    MessageBox.Show("길이는 0보다 커야 합니다.", "입력 오류",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
            }
            catch (FormatException)
            {
                MessageBox.Show("올바른 숫자를 입력하세요.", "입력 오류",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                LengthTextBox.Focus();
                LengthTextBox.SelectAll();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}