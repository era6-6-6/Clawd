using System.Diagnostics;
using System.Text;

namespace Clawd.Services;

/// <summary>
/// Runs "claude" CLI in print mode and streams the response back.
/// </summary>
public class ClaudeService : IDisposable
{
    private const string SystemPrompt =
        "You are Clawd, a helpful desktop assistant who happens to be a cute pixel crab. " +
        "You are knowledgeable, resourceful, and friendly. " +
        "You help with anything the user asks — research, answers, advice, coding, explanations, etc. " +
        "Use tools when helpful (web fetch, search, etc). Give complete, useful answers. " +
        "Keep a warm casual tone but prioritize being genuinely helpful over being cute. " +
        "You can sprinkle in the occasional crab reference but substance comes first.";

    private Process? _process;
    private bool _running;

    public event Action<string>? OnToken;
    public event Action? OnComplete;
    public event Action<string>? OnError;

    public bool IsRunning => _running;

    public void Send(string message)
    {
        if (_running)
        {
            Cancel();
        }

        _running = true;

        Task.Run(() =>
        {
            try
            {
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "claude",
                        Arguments = $"--system-prompt \"{EscapeArg(SystemPrompt)}\" -p \"{EscapeArg(message)}\" --model haiku",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    },
                    EnableRaisingEvents = true
                };

                _process.Start();

                // Stream stdout token by token
                var buf = new char[64];
                var reader = _process.StandardOutput;
                int read;
                while ((read = reader.Read(buf, 0, buf.Length)) > 0)
                {
                    var chunk = new string(buf, 0, read);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => OnToken?.Invoke(chunk));
                }

                _process.WaitForExit(30000);

                if (_process.ExitCode != 0)
                {
                    var err = _process.StandardError.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(err))
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => OnError?.Invoke(err));
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _running = false;
                    OnComplete?.Invoke();
                });
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _running = false;
                    OnError?.Invoke(ex.Message);
                    OnComplete?.Invoke();
                });
            }
        });
    }

    public void Cancel()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(true);
        }
        catch { }
        _running = false;
    }

    private static string EscapeArg(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public void Dispose()
    {
        Cancel();
        _process?.Dispose();
    }
}
