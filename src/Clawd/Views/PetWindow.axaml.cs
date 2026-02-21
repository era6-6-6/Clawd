using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Clawd.Controls;
using Clawd.Services;

namespace Clawd.Views;

public partial class PetWindow : Window
{
    private ChatBubbleWindow? _chatWindow;
    private readonly WeatherService? _weather;
    private readonly SystemMonitorService? _sysMonitor;

    public CrabControl? CrabControl => this.FindControl<CrabControl>("Crab");

    public PetWindow() : this(null, null) { }

    public PetWindow(WeatherService? weather, SystemMonitorService? sysMonitor = null)
    {
        _weather = weather;
        _sysMonitor = sysMonitor;
        InitializeComponent();

        Opened += (_, _) =>
        {
            var crab = CrabControl;
            if (crab == null) return;

            var screen = Screens.Primary;
            if (screen == null) return;

            var workArea = screen.WorkingArea;
            var scaling = screen.Scaling;

            crab.ScreenWorkArea = new Rect(
                workArea.X / scaling,
                workArea.Y / scaling,
                workArea.Width / scaling,
                workArea.Height / scaling);

            // Multi-monitor: pass all screen work areas
            var allScreens = new System.Collections.Generic.List<Rect>();
            foreach (var s in Screens.All)
            {
                var sc = s.Scaling;
                var wa2 = s.WorkingArea;
                allScreens.Add(new Rect(wa2.X / sc, wa2.Y / sc, wa2.Width / sc, wa2.Height / sc));
            }
            crab.SetAllScreens(allScreens);

            crab.RequestWindowMove += OnCrabRequestWindowMove;
            crab.RequestOpenChat += OnOpenChat;
            crab.RequestSummarizeClipboard += OnSummarizeClipboard;

            if (_weather != null)
            {
                crab.SetWeather(_weather.Current);
                _weather.OnWeatherUpdated += info => crab.SetWeather(info);
            }

            if (_sysMonitor != null)
            {
                crab.SetSystemStats(_sysMonitor.Current);
                _sysMonitor.OnUpdated += stats => crab.SetSystemStats(stats);
            }

            Width = Controls.CrabControl.WindowWidth;
            Height = Controls.CrabControl.WindowHeight;
        };
    }

    private void OnCrabRequestWindowMove(double screenX, double screenY)
    {
        // Find which screen the crab is on for correct scaling
        var scaling = 1.0;
        foreach (var s in Screens.All)
        {
            var sc = s.Scaling;
            var wa = s.WorkingArea;
            var r = new Rect(wa.X / sc, wa.Y / sc, wa.Width / sc, wa.Height / sc);
            if (screenX >= r.X && screenX <= r.Right && screenY >= r.Y - 100 && screenY <= r.Bottom + 100)
            {
                scaling = sc;
                break;
            }
        }
        Position = new PixelPoint((int)(screenX * scaling), (int)(screenY * scaling));
    }

    public void OpenChat()
    {
        var screen = Screens.Primary;
        var scaling = screen?.Scaling ?? 1.0;
        var cx = Position.X / scaling + Controls.CrabControl.WindowWidth / 2;
        var cy = Position.Y / scaling;
        OnOpenChat(cx, cy);
    }

    private void OnOpenChat(double crabScreenX, double crabScreenY)
    {
        if (_chatWindow is { IsVisible: true })
        {
            _chatWindow.Activate();
            return;
        }

        var screen = Screens.Primary;
        var scaling = screen?.Scaling ?? 1.0;

        _chatWindow = new ChatBubbleWindow();
        _chatWindow.Closed += (_, _) => CrabControl?.SetChatOpen(false);

        var chatX = crabScreenX - 80;
        var chatY = crabScreenY - 460;

        if (screen != null)
        {
            var wa = screen.WorkingArea;
            chatX = Math.Clamp(chatX, wa.X / scaling, wa.Right / scaling - 420);
            chatY = Math.Clamp(chatY, wa.Y / scaling, wa.Bottom / scaling - 440);
        }

        _chatWindow.Position = new PixelPoint((int)(chatX * scaling), (int)(chatY * scaling));
        _chatWindow.Show();

        CrabControl?.SetChatOpen(true);
    }

    private void OnSummarizeClipboard()
    {
        // Open chat and trigger clipboard summary
        OpenChat();
        // Small delay to let the chat window open, then send the clipboard command
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_chatWindow != null)
            {
                // The chat window handles "clipboard" as a special command
                _chatWindow.FindControl<TextBox>("InputBox")!.Text = "summarize clipboard";
                // Simulate send
                _chatWindow.OnSend(null, null!);
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
