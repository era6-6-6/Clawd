namespace Clawd.Services;

public enum Mood
{
    Happy,
    Content,
    Neutral,
    Hungry,
    Sad,
    Lonely
}

public class MoodService
{
    private DateTime _lastFed = DateTime.UtcNow;
    private DateTime _lastPetted = DateTime.UtcNow;
    private DateTime _lastInteraction = DateTime.UtcNow;
    private int _feedCount;
    private int _petCount;

    public Mood Current { get; private set; } = Mood.Content;

    public void OnFed()
    {
        _lastFed = DateTime.UtcNow;
        _feedCount++;
        _lastInteraction = DateTime.UtcNow;
        Recalculate();
    }

    public void OnPetted()
    {
        _lastPetted = DateTime.UtcNow;
        _petCount++;
        _lastInteraction = DateTime.UtcNow;
        Recalculate();
    }

    public void OnInteraction()
    {
        _lastInteraction = DateTime.UtcNow;
        Recalculate();
    }

    public void Update()
    {
        Recalculate();
    }

    private void Recalculate()
    {
        var now = DateTime.UtcNow;
        var sinceFed = now - _lastFed;
        var sincePetted = now - _lastPetted;
        var sinceInteraction = now - _lastInteraction;

        // Hungry after 2 hours without feeding
        if (sinceFed.TotalMinutes > 120)
        {
            Current = Mood.Hungry;
            return;
        }

        // Lonely after 1 hour without any interaction
        if (sinceInteraction.TotalMinutes > 60)
        {
            Current = Mood.Lonely;
            return;
        }

        // Sad after 30 min without petting or interaction
        if (sincePetted.TotalMinutes > 30 && sinceInteraction.TotalMinutes > 30)
        {
            Current = Mood.Sad;
            return;
        }

        // Neutral if just okay
        if (sinceInteraction.TotalMinutes > 10)
        {
            Current = Mood.Neutral;
            return;
        }

        // Recently interacted — happy or content
        if (sinceFed.TotalMinutes < 5 || sincePetted.TotalMinutes < 5)
        {
            Current = Mood.Happy;
            return;
        }

        Current = Mood.Content;
    }

    public string MoodEmoji => Current switch
    {
        Mood.Happy => "\U0001F60A",
        Mood.Content => "\U0001F642",
        Mood.Neutral => "\U0001F610",
        Mood.Hungry => "\U0001F374",
        Mood.Sad => "\U0001F622",
        Mood.Lonely => "\U0001F97A",
        _ => ""
    };

    public string MoodText => Current switch
    {
        Mood.Happy => "Happy",
        Mood.Content => "Content",
        Mood.Neutral => "Neutral",
        Mood.Hungry => "Hungry",
        Mood.Sad => "Sad",
        Mood.Lonely => "Lonely",
        _ => ""
    };
}
