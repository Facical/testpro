using System.Windows;

namespace testpro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set up global exception handling
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Log the exception and show user-friendly message
            MessageBox.Show($"예상하지 못한 오류가 발생했습니다:\n{e.Exception.Message}",
                          "오류", MessageBoxButton.OK, MessageBoxImage.Error);

            // Mark as handled to prevent application crash
            e.Handled = true;
        }
    }
}