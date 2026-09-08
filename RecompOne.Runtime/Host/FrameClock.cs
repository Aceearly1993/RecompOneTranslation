using System.Diagnostics;

namespace RecompOne.Runtime.Host;

internal static class FrameClock
{
    private static double FrameMs => Sdk.LibGpu.Pal ? 1000.0 / 50.0 : 1000.0 / 60.0;
    private const double SpinMs = 1.5;

    private static readonly Stopwatch _clock = Stopwatch.StartNew();
    private static double _nextFrameMs;

    public static bool VSync { get; set; }

    public static double LastFrameMs { get; private set; }

    public static double Fps { get; private set; }
    private static double _fpsAccumMs;
    private static int _fpsFrames;

    public static double PresentFps { get; private set; }
    private static double _presentStartMs;
    private static int _presentFrames;
    public static double LastWaitMs { get; private set; }

    private static double _lastStart;


    public static void Throttle()
    {
        var now = _clock.Elapsed.TotalMilliseconds;
        LastFrameMs = now - _lastStart;
        _lastStart = now;

        _fpsAccumMs += LastFrameMs;
        _fpsFrames++;
        if (_fpsAccumMs >= 1000.0)
        {
            Fps = _fpsFrames * 1000.0 / _fpsAccumMs;
            _fpsAccumMs = 0;
            _fpsFrames = 0;

        }

        _nextFrameMs += FrameMs;
        var wait = _nextFrameMs - now;

        if (wait < -100)
        {
            _nextFrameMs = now;
            LastWaitMs = 0;
            return;
        }

        if (wait <= 0)
        {
            LastWaitMs = 0;
            return;
        }

        if (VSync && wait < FrameMs * 0.75)
        {
            LastWaitMs = 0;
            return;
        }

        var sleepUntil = _nextFrameMs - SpinMs;
        if (now < sleepUntil)
        {
            var ms = (int)(sleepUntil - now);
            if (ms > 0) Thread.Sleep(ms);
        }

        while (_clock.Elapsed.TotalMilliseconds < _nextFrameMs)
            Thread.SpinWait(48);

        LastWaitMs = wait;
    }

    public static void MarkPresent()
    {
        var now = _clock.Elapsed.TotalMilliseconds;
        _presentFrames++;



        var elapsed = now - _presentStartMs;
        if (elapsed < 1000.0) return;

        PresentFps = _presentFrames * 1000.0 / elapsed;
        _presentStartMs = now;
        _presentFrames = 0;
    }

    public static void Resync()
    {
        _nextFrameMs = _clock.Elapsed.TotalMilliseconds;
    }
}