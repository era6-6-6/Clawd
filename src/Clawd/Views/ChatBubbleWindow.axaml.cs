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
    private readonly ReminderService _reminders = new();
    private readonly StackPanel _messagesPanel;
    private readonly ScrollViewer _messagesScroll;
    private readonly TextBox _inputBox;
    private string _currentResponseText = "";
    private Border? _currentResponseBubble;
    private bool _isStreaming;
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

        _reminders.OnReminderDue += OnReminderDue;

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

    public void OnSend(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SendMessage();

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

        // Check for reminder
        if (_reminders.TryParse(text, out var confirm))
        {
            AddMessageBubble(text, isUser: true);
            AddRenderedBubble(confirm!, isUser: false);
            return;
        }

        // Check for clipboard summary
        if (text.StartsWith("summarize clipboard", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("summarise clipboard", StringComparison.OrdinalIgnoreCase)
            || text.Equals("clipboard", StringComparison.OrdinalIgnoreCase))
        {
            _ = SummarizeClipboard();
            return;
        }

        AddMessageBubble(text, isUser: true);

        // Add streaming placeholder
        _currentResponseText = "";
        _isStreaming = true;
        _currentResponseBubble = CreateBubble(isUser: false);
        var placeholder = new SelectableTextBlock
        {
            Text = "...",
            Foreground = new SolidColorBrush(Color.Parse("#666")),
            FontFamily = new FontFamily("monospace"),
            FontSize = 11,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
        };
        _currentResponseBubble.Child = placeholder;
        _messagesPanel.Children.Add(_currentResponseBubble);
        ScrollToBottom();

        _claude.Send(text);
    }

    private async Task SummarizeClipboard()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        var clipText = await clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(clipText))
        {
            AddRenderedBubble("Clipboard is empty!", isUser: false);
            return;
        }

        var prompt = $"Summarize this:\n\n{clipText}";
        AddMessageBubble("Summarize clipboard", isUser: true);

        _currentResponseText = "";
        _isStreaming = true;
        _currentResponseBubble = CreateBubble(isUser: false);
        var placeholder = new SelectableTextBlock
        {
            Text = "...",
            Foreground = new SolidColorBrush(Color.Parse("#666")),
            FontFamily = new FontFamily("monospace"),
            FontSize = 11,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
        };
        _currentResponseBubble.Child = placeholder;
        _messagesPanel.Children.Add(_currentResponseBubble);
        ScrollToBottom();

        _claude.Send(prompt);
    }

    private void OnReminderDue(Reminder reminder)
    {
        AddRenderedBubble($"\u23F0 Reminder: {reminder.Message}", isUser: false);
        Activate();
        if (!IsVisible) Show();
    }

    private Border CreateBubble(bool isUser)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(isUser ? "#1a2633" : "#1a1a1a")),
            BorderBrush = new SolidColorBrush(Color.Parse(isUser ? "#2a4a6a" : "#333")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6),
            Margin = isUser ? new Thickness(60, 0, 0, 0) : new Thickness(0, 0, 20, 0),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
    }

    private void AddMessageBubble(string text, bool isUser)
    {
        var bubble = CreateBubble(isUser);
        var tb = new SelectableTextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse(isUser ? "#88bbdd" : "#d4d4d4")),
            FontFamily = new FontFamily("monospace"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        };
        bubble.Child = tb;
        _messagesPanel.Children.Add(bubble);
        ScrollToBottom();
    }

    /// <summary>
    /// Add a bubble with basic markdown rendering (code blocks, bold, headers).
    /// </summary>
    private void AddRenderedBubble(string text, bool isUser)
    {
        var bubble = CreateBubble(isUser);
        bubble.Child = RenderMarkdown(text, isUser);
        _messagesPanel.Children.Add(bubble);
        ScrollToBottom();
    }

    private Control RenderMarkdown(string text, bool isUser)
    {
        var lines = text.Split('\n');
        var panel = new StackPanel { Spacing = 4 };
        var inCodeBlock = false;
        var codeLines = new List<string>();
        var normalColor = isUser ? "#88bbdd" : "#d4d4d4";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Code block toggle
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    // Close code block
                    var codeBlock = new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#0d0d0d")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#444")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 6),
                        Margin = new Thickness(0, 2),
                    };
                    codeBlock.Child = new SelectableTextBlock
                    {
                        Text = string.Join("\n", codeLines),
                        Foreground = new SolidColorBrush(Color.Parse("#a0d0a0")),
                        FontFamily = new FontFamily("monospace"),
                        FontSize = 10,
                        TextWrapping = TextWrapping.Wrap,
                    };
                    panel.Children.Add(codeBlock);
                    codeLines.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeLines.Add(line);
                continue;
            }

            // Headers
            if (line.StartsWith("### "))
            {
                panel.Children.Add(new SelectableTextBlock
                {
                    Text = line[4..],
                    Foreground = new SolidColorBrush(Color.Parse("#D77757")),
                    FontFamily = new FontFamily("monospace"),
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0),
                });
                continue;
            }
            if (line.StartsWith("## "))
            {
                panel.Children.Add(new SelectableTextBlock
                {
                    Text = line[3..],
                    Foreground = new SolidColorBrush(Color.Parse("#D77757")),
                    FontFamily = new FontFamily("monospace"),
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0),
                });
                continue;
            }
            if (line.StartsWith("# "))
            {
                panel.Children.Add(new SelectableTextBlock
                {
                    Text = line[2..],
                    Foreground = new SolidColorBrush(Color.Parse("#D77757")),
                    FontFamily = new FontFamily("monospace"),
                    FontSize = 13,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0),
                });
                continue;
            }

            // Inline code: `code`
            if (line.Contains('`'))
            {
                var inlinePanel = new WrapPanel();
                var parts = line.Split('`');
                for (var j = 0; j < parts.Length; j++)
                {
                    if (string.IsNullOrEmpty(parts[j])) continue;
                    if (j % 2 == 1) // inside backticks
                    {
                        var codeBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.Parse("#0d0d0d")),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 1),
                            Margin = new Thickness(1, 0),
                        };
                        codeBorder.Child = new TextBlock
                        {
                            Text = parts[j],
                            Foreground = new SolidColorBrush(Color.Parse("#a0d0a0")),
                            FontFamily = new FontFamily("monospace"),
                            FontSize = 10,
                        };
                        inlinePanel.Children.Add(codeBorder);
                    }
                    else
                    {
                        // Handle **bold**
                        AddBoldText(inlinePanel, parts[j], normalColor);
                    }
                }
                panel.Children.Add(inlinePanel);
                continue;
            }

            // Bold text: **bold**
            if (line.Contains("**"))
            {
                var inlinePanel = new WrapPanel();
                AddBoldText(inlinePanel, line, normalColor);
                panel.Children.Add(inlinePanel);
                continue;
            }

            // Bullet points
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                panel.Children.Add(new SelectableTextBlock
                {
                    Text = "\u2022 " + line[2..],
                    Foreground = new SolidColorBrush(Color.Parse(normalColor)),
                    FontFamily = new FontFamily("monospace"),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                });
                continue;
            }

            // Normal text
            panel.Children.Add(new SelectableTextBlock
            {
                Text = line,
                Foreground = new SolidColorBrush(Color.Parse(normalColor)),
                FontFamily = new FontFamily("monospace"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        // Unclosed code block
        if (inCodeBlock && codeLines.Count > 0)
        {
            var codeBlock = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0d0d0d")),
                BorderBrush = new SolidColorBrush(Color.Parse("#444")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6),
                Margin = new Thickness(0, 2),
            };
            codeBlock.Child = new SelectableTextBlock
            {
                Text = string.Join("\n", codeLines),
                Foreground = new SolidColorBrush(Color.Parse("#a0d0a0")),
                FontFamily = new FontFamily("monospace"),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
            };
            panel.Children.Add(codeBlock);
        }

        // If only one child, return it directly
        if (panel.Children.Count == 1)
            return panel.Children[0];

        return panel;
    }

    private static void AddBoldText(WrapPanel panel, string text, string normalColor)
    {
        var parts = text.Split("**");
        for (var i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            panel.Children.Add(new TextBlock
            {
                Text = parts[i],
                Foreground = new SolidColorBrush(Color.Parse(i % 2 == 1 ? "#ffffff" : normalColor)),
                FontFamily = new FontFamily("monospace"),
                FontSize = 11,
                FontWeight = i % 2 == 1 ? FontWeight.Bold : FontWeight.Normal,
            });
        }
    }

    private void OnClaudeToken(string token)
    {
        if (!_isStreaming || _currentResponseBubble == null) return;

        _currentResponseText += token;

        // Re-render markdown on every token (keeps it simple)
        _currentResponseBubble.Child = RenderMarkdown(_currentResponseText, false);
        ScrollToBottom();
    }

    private void OnClaudeComplete()
    {
        if (_isStreaming && _currentResponseBubble != null)
        {
            if (string.IsNullOrWhiteSpace(_currentResponseText))
            {
                _currentResponseBubble.Child = new SelectableTextBlock
                {
                    Text = "(no response)",
                    Foreground = new SolidColorBrush(Color.Parse("#666")),
                    FontFamily = new FontFamily("monospace"),
                    FontSize = 11,
                };
            }
            else
            {
                // Final render
                _currentResponseBubble.Child = RenderMarkdown(_currentResponseText, false);
            }
        }
        _isStreaming = false;
        _currentResponseBubble = null;
        _currentResponseText = "";
        _inputBox.Focus();
    }

    private void OnClaudeError(string error)
    {
        if (_currentResponseBubble != null)
        {
            _currentResponseBubble.Child = new SelectableTextBlock
            {
                Text = $"Error: {error}",
                Foreground = new SolidColorBrush(Color.Parse("#ef4444")),
                FontFamily = new FontFamily("monospace"),
                FontSize = 11,
            };
        }
        _isStreaming = false;
    }

    private void ScrollToBottom()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _messagesScroll.ScrollToEnd();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
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
        _reminders.Dispose();
        base.OnClosed(e);
    }
}
