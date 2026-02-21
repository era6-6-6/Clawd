using System.Text.RegularExpressions;

namespace Clawd.Services;

public class Reminder
{
    public string Message { get; init; } = "";
    public DateTime DueAt { get; init; }
}

public class ReminderService : IDisposable
{
    private readonly List<Reminder> _reminders = new();
    private readonly System.Timers.Timer _timer;

    public event Action<Reminder>? OnReminderDue;

    public ReminderService()
    {
        _timer = new System.Timers.Timer(10_000); // check every 10s
        _timer.Elapsed += (_, _) => CheckReminders();
        _timer.AutoReset = true;
        _timer.Start();
    }

    /// <summary>
    /// Try to parse a reminder from user text. Returns true if it was a reminder request.
    /// Supports: "remind me in 30 min to stretch", "reminder 1 hour take a break", etc.
    /// </summary>
    public bool TryParse(string input, out string? confirmMessage)
    {
        confirmMessage = null;

        var match = Regex.Match(input,
            @"remind(?:er|[\s]+me)?[\s]+(?:in[\s]+)?(\d+)\s*(s(?:ec(?:ond)?s?)?|m(?:in(?:ute)?s?)?|h(?:(?:ou)?rs?)?)\s*(?:to\s+)?(.+)",
            RegexOptions.IgnoreCase);

        if (!match.Success) return false;

        var amount = int.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value.ToLowerInvariant();
        var message = match.Groups[3].Value.Trim();

        var delay = unit[0] switch
        {
            's' => TimeSpan.FromSeconds(amount),
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            _ => TimeSpan.FromMinutes(amount)
        };

        var reminder = new Reminder
        {
            Message = message,
            DueAt = DateTime.UtcNow + delay
        };

        lock (_reminders)
            _reminders.Add(reminder);

        var unitName = unit[0] switch
        {
            's' => amount == 1 ? "second" : "seconds",
            'm' => amount == 1 ? "minute" : "minutes",
            'h' => amount == 1 ? "hour" : "hours",
            _ => "minutes"
        };

        confirmMessage = $"Got it! I'll remind you in {amount} {unitName}: \"{message}\"";
        return true;
    }

    private void CheckReminders()
    {
        var now = DateTime.UtcNow;
        List<Reminder> due;

        lock (_reminders)
        {
            due = _reminders.Where(r => r.DueAt <= now).ToList();
            foreach (var r in due)
                _reminders.Remove(r);
        }

        foreach (var r in due)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnReminderDue?.Invoke(r));
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
