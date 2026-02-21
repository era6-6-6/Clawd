using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Clawd.Views;

namespace Clawd;

public class App : Application
{
    private PetWindow? _petWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _petWindow = new PetWindow();
            desktop.MainWindow = _petWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        base.OnFrameworkInitializationCompleted();
    }

    public void OnChat(object? sender, EventArgs e) => _petWindow?.OpenChat();
    public void OnFeed(object? sender, EventArgs e) => _petWindow?.CrabControl?.DoFeed();
    public void OnPet(object? sender, EventArgs e) => _petWindow?.CrabControl?.DoPet();
    public void OnDance(object? sender, EventArgs e) => _petWindow?.CrabControl?.DoDance();
    public void OnChangeHat(object? sender, EventArgs e) => _petWindow?.CrabControl?.DoChangeHat();
    public void OnToggleFriend(object? sender, EventArgs e) => _petWindow?.CrabControl?.DoToggleFriend();

    public void OnQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
