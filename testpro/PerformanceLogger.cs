// PerformanceLogger.cs - ETW를 사용한 성능 로깅
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Windows.Threading;

[EventSource(Name = "SmartShelf-Performance")]
public sealed class PerformanceEventSource : EventSource
{
    public static PerformanceEventSource Log = new PerformanceEventSource();

    [Event(1, Level = EventLevel.Informational)]
    public void UIInputStart(string toolName)
    {
        WriteEvent(1, toolName);
    }

    [Event(2, Level = EventLevel.Informational)]
    public void UIInputEnd(string toolName, double elapsedMs)
    {
        WriteEvent(2, toolName, elapsedMs);
    }

    [Event(3, Level = EventLevel.Warning)]
    public void SlowUIResponse(string toolName, double elapsedMs)
    {
        WriteEvent(3, toolName, elapsedMs);
    }
}