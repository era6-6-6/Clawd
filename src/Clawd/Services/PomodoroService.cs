namespace Clawd.Services;

public enum PomodoroState
{
    Idle,
    Focus,
    Break
}

public class PomodoroService : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private DateTime _endTime;
    private int _focusMinutes = 25;
    private int _breakMinutes = 5;
    private int _sessionsCompleted;

    public PomodoroState State { get; private set; } = PomodoroState.Idle;
    public TimeSpan Remaining => State == PomodoroState.Idle ? TimeSpan.Zero : _endTime - DateTime.UtcNow;
    public int SessionsCompleted => _sessionsCompleted;

    public event Action? OnFocusComplete;
    public event Action? OnBreakComplete;
    public event Action<TimeSpan>? OnTick;

    public PomodoroService()
    {
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += (_, _) => Check();
        _timer.AutoReset = true;
    }

    public void StartFocus(int minutes = 25)
    {
        _focusMinutes = minutes;
        _endTime = DateTime.UtcNow.AddMinutes(minutes);
        State = PomodoroState.Focus;
        _timer.Start();
    }

    public void StartFocusSeconds(int seconds)
    {
        _endTime = DateTime.UtcNow.AddSeconds(seconds);
        State = PomodoroState.Focus;
        _timer.Start();
    }

    public void StartBreak(int minutes = 5)
    {
        _breakMinutes = minutes;
        _endTime = DateTime.UtcNow.AddMinutes(minutes);
        State = PomodoroState.Break;
        _timer.Start();
    }

    public void Stop()
    {
        State = PomodoroState.Idle;
        _timer.Stop();
    }

    private void Check()
    {
        var remaining = _endTime - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            if (State == PomodoroState.Focus)
            {
                _sessionsCompleted++;
                State = PomodoroState.Idle;
                _timer.Stop();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => OnFocusComplete?.Invoke());
            }
            else if (State == PomodoroState.Break)
            {
                State = PomodoroState.Idle;
                _timer.Stop();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => OnBreakComplete?.Invoke());
            }
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnTick?.Invoke(remaining));
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
