using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using testpro.ViewModels;

namespace testpro.Converters
{
    public class ViewModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ViewMode currentMode && parameter is string targetMode)
            {
                switch (targetMode)
                {
                    case "View2D":
                        return currentMode == ViewMode.View2D ? Visibility.Visible : Visibility.Collapsed;
                    case "View3D":
                        return currentMode == ViewMode.View3D ? Visibility.Visible : Visibility.Collapsed;
                    default:
                        return Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}