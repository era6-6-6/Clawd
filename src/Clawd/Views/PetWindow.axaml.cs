using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Clawd.Controls;

namespace Clawd.Views;

public partial class PetWindow : Window
{
    private ChatBubbleWindow? _chatWindow;

    public CrabControl? CrabControl => this.FindControl<CrabControl>("Crab");

    public PetWindow()
    {
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

            crab.RequestWindowMove += OnCrabRequestWindowMove;
            crab.RequestOpenChat += OnOpenChat;

            Width = Controls.CrabControl.WindowWidth;
            Height = Controls.CrabControl.WindowHeight;
        };
    }

    private void OnCrabRequestWindowMove(double screenX, double screenY)
    {
        var screen = Screens.Primary;
        var scaling = screen?.Scaling ?? 1.0;
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
        // If already open, just focus it
        if (_chatWindow is { IsVisible: true })
        {
            _chatWindow.Activate();
            return;
        }

        var screen = Screens.Primary;
        var scaling = screen?.Scaling ?? 1.0;

        _chatWindow = new ChatBubbleWindow();
        _chatWindow.Closed += (_, _) => CrabControl?.SetChatOpen(false);

        // Position above the crab
        var chatX = crabScreenX - 80;
        var chatY = crabScreenY - 460;

        // Clamp to screen
        if (screen != null)
        {
            var wa = screen.WorkingArea;
            chatX = Math.Clamp(chatX, wa.X / scaling, wa.Right / scaling - 420);
            chatY = Math.Clamp(chatY, wa.Y / scaling, wa.Bottom / scaling - 440);
        }

        _chatWindow.Position = new PixelPoint((int)(chatX * scaling), (int)(chatY * scaling));
        _chatWindow.Show();

        // Freeze the crab while chatting
        CrabControl?.SetChatOpen(true);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
