using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Clawd.Services;
using Clawd.Views;

namespace Clawd;

public class App : Application
{
    private PetWindow? _petWindow;
    private WeatherService? _weather;
    private SystemMonitorService? _sysMonitor;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _weather = new WeatherService();
            _sysMonitor = new SystemMonitorService();
            _petWindow = new PetWindow(_weather, _sysMonitor);
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

    public async void OnSettings(object? sender, EventArgs e)
    {
        if (_petWindow == null || _weather == null) return;
        var crab = _petWindow.CrabControl;
        var settings = new SettingsWindow(_weather.City, crab?.CheekyMode ?? true);
        var result = await settings.ShowDialog<SettingsResult?>(_petWindow);
        if (result is { Cancelled: false })
        {
            if (string.IsNullOrWhiteSpace(result.City))
                _weather.Disable();
            else
                await _weather.SetCity(result.City);

            if (crab != null)
                crab.CheekyMode = result.CheekyMode;
        }
    }

    public void OnQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
