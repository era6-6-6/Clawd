using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Clawd.Services;

namespace Clawd.Views;

public partial class ChatBubbleWindow : Window
{
    private readonly ClaudeService _claude = new();
    private readonly StackPanel _messagesPanel;
    private readonly ScrollViewer _messagesScroll;
    private readonly TextBox _inputBox;
    private SelectableTextBlock? _currentResponseBlock;
    private bool _isDragging;
    private Point _dragStart;

    public ChatBubbleWindow()
    {
        InitializeComponent();

        _messagesPanel = this.FindControl<StackPanel>("MessagesPanel")!;
        _messagesScroll = this.FindControl<ScrollViewer>("MessagesScroll")!;
        _inputBox = this.FindControl<TextBox>("InputBox")!;

        _claude.OnToken += OnClaudeToken;
        _claude.OnComplete += OnClaudeComplete;
        _claude.OnError += OnClaudeError;

        // Allow dragging the window from the header area
        PointerPressed += OnWindowPointerPressed;
        PointerMoved += OnWindowPointerMoved;
        PointerReleased += OnWindowPointerReleased;

        Opened += (_, _) => _inputBox.Focus();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _claude.Cancel();
        Close();
    }

    public void OnSend(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SendMessage();
    }

    public void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            SendMessage();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            OnClose(null, null!);
        }
    }

    private void SendMessage()
    {
        var text = _inputBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _inputBox.Text = "";

        // Add user message bubble
        AddMessageBubble(text, isUser: true);

        // Add empty response bubble (will be filled by streaming)
        _currentResponseBlock = AddMessageBubble("", isUser: false);

        _claude.Send(text);
    }

    private SelectableTextBlock AddMessageBubble(string text, bool isUser)
    {
        var bubble = new Border
        {
            Background = new SolidColorBrush(Color.Parse(isUser ? "#1a2633" : "#1a1a1a")),
            BorderBrush = new SolidColorBrush(Color.Parse(isUser ? "#2a4a6a" : "#333")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6),
            Margin = isUser ? new Thickness(60, 0, 0, 0) : new Thickness(0, 0, 20, 0),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

        var tb = new SelectableTextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse(isUser ? "#88bbdd" : "#d4d4d4")),
            FontFamily = new FontFamily("monospace"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        };

        if (!isUser && string.IsNullOrEmpty(text))
        {
            tb.Text = "...";
            tb.Foreground = new SolidColorBrush(Color.Parse("#666"));
            tb.FontStyle = FontStyle.Italic;
        }

        bubble.Child = tb;
        _messagesPanel.Children.Add(bubble);
        ScrollToBottom();

        return tb;
    }

    private void OnClaudeToken(string token)
    {
        if (_currentResponseBlock == null) return;

        if (_currentResponseBlock.FontStyle == FontStyle.Italic)
        {
            // First token — clear the "..." placeholder
            _currentResponseBlock.Text = "";
            _currentResponseBlock.FontStyle = FontStyle.Normal;
            _currentResponseBlock.Foreground = new SolidColorBrush(Color.Parse("#d4d4d4"));
        }

        _currentResponseBlock.Text += token;
        ScrollToBottom();
    }

    private void OnClaudeComplete()
    {
        if (_currentResponseBlock != null && string.IsNullOrWhiteSpace(_currentResponseBlock.Text))
        {
            _currentResponseBlock.Text = "(no response)";
            _currentResponseBlock.Foreground = new SolidColorBrush(Color.Parse("#666"));
        }
        _currentResponseBlock = null;
        _inputBox.Focus();
    }

    private void OnClaudeError(string error)
    {
        if (_currentResponseBlock != null)
        {
            _currentResponseBlock.Text = $"Error: {error}";
            _currentResponseBlock.Foreground = new SolidColorBrush(Color.Parse("#ef4444"));
            _currentResponseBlock.FontStyle = FontStyle.Normal;
        }
    }

    private void ScrollToBottom()
    {
        // Defer scroll so layout updates first, otherwise the last line gets clipped
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _messagesScroll.ScrollToEnd();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    // Window dragging
    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        // Only drag from top 40px (header area)
        if (pos.Y < 50)
        {
            _isDragging = true;
            _dragStart = pos;
            e.Handled = true;
        }
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        var pos = e.GetPosition(this);
        var delta = pos - _dragStart;
        Position = new PixelPoint(Position.X + (int)delta.X, Position.Y + (int)delta.Y);
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _claude.Dispose();
        base.OnClosed(e);
    }
}
