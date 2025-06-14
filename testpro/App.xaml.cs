// App.xaml.cs에 추가 - 전역 UI 성능 모니터링
using System.Diagnostics;
using System.Windows.Media;
using System.Windows;
using System.Windows.Threading;

public partial class App : Application
{
    private DispatcherTimer _performanceTimer;
    private int _frameCount = 0;
    private DateTime _lastFrameTime = DateTime.Now;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 프레임 렌더링 모니터링
        CompositionTarget.Rendering += OnRendering;

        // 1초마다 FPS 계산
        _performanceTimer = new DispatcherTimer();
        _performanceTimer.Interval = TimeSpan.FromSeconds(1);
        _performanceTimer.Tick += CalculateFPS;
        _performanceTimer.Start();

        // UI 스레드 블로킹 감지
        EnableUIThreadBlockingDetection();

        // 전역 예외 처리
        DispatcherUnhandledException += App_DispatcherUnhandledException;


    }

    private void App_DispatcherUnhandledException(object sender,
      System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"예기치 않은 오류가 발생했습니다:\n{e.Exception.Message}",
            "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnRendering(object sender, EventArgs e)
    {
        _frameCount++;
    }

    private void CalculateFPS(object sender, EventArgs e)
    {
        var now = DateTime.Now;
        var elapsed = (now - _lastFrameTime).TotalSeconds;
        var fps = _frameCount / elapsed;

        Debug.WriteLine($"[FPS] {fps:F2} fps");

        _frameCount = 0;
        _lastFrameTime = now;
    }

    private void EnableUIThreadBlockingDetection()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Send);
        timer.Interval = TimeSpan.FromMilliseconds(100);
        var lastTick = DateTime.Now;

        timer.Tick += (s, e) =>
        {
            var now = DateTime.Now;
            var elapsed = (now - lastTick).TotalMilliseconds;

            if (elapsed > 150) // 150ms 이상 블로킹
            {
                Debug.WriteLine($"[경고] UI 스레드 블로킹 감지: {elapsed:F0}ms");
            }

            lastTick = now;
        };

        timer.Start();
    }
}