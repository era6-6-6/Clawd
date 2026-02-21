using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Clawd.Views;

public class SettingsResult
{
    public string? City { get; init; }
    public bool CheekyMode { get; init; }
    public bool Cancelled { get; init; }
}

public partial class SettingsWindow : Window
{
    private readonly TextBox _cityBox;
    private readonly ToggleSwitch _cheekyToggle;

    public SettingsWindow() : this(null, true) { }

    public SettingsWindow(string? currentCity, bool cheekyMode = true)
    {
        InitializeComponent();
        _cityBox = this.FindControl<TextBox>("CityBox")!;
        _cheekyToggle = this.FindControl<ToggleSwitch>("CheekyToggle")!;

        if (!string.IsNullOrEmpty(currentCity))
            _cityBox.Text = currentCity;

        _cheekyToggle.IsChecked = cheekyMode;

        Opened += (_, _) => _cityBox.Focus();
    }

    private void OnClear(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cityBox.Text = "";
    }

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(new SettingsResult
        {
            City = _cityBox.Text?.Trim() ?? "",
            CheekyMode = _cheekyToggle.IsChecked == true
        });
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(new SettingsResult { Cancelled = true });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
