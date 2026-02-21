using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Clawd.Views;

public partial class SettingsWindow : Window
{
    private readonly TextBox _cityBox;

    public SettingsWindow() : this(null) { }

    public SettingsWindow(string? currentCity)
    {
        InitializeComponent();
        _cityBox = this.FindControl<TextBox>("CityBox")!;
        if (!string.IsNullOrEmpty(currentCity))
            _cityBox.Text = currentCity;

        Opened += (_, _) => _cityBox.Focus();
    }

    private void OnClear(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cityBox.Text = "";
    }

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(_cityBox.Text?.Trim() ?? "");
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
