using System.Net.Http;
using System.Text.Json;

namespace Clawd.Services;

public enum WeatherCondition
{
    Clear,
    Cloudy,
    Rain,
    Snow,
    Thunderstorm,
    Fog,
    Unknown
}

public class WeatherInfo
{
    public WeatherCondition Condition { get; init; }
    public double Temperature { get; init; }
    public string Description { get; init; } = "";
    public string Location { get; init; } = "";
}

public class WeatherService : IDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _settingsPath;
    private string? _city;
    private double? _lat;
    private double? _lon;
    private WeatherInfo? _lastWeather;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly TimeSpan _fetchInterval = TimeSpan.FromMinutes(30);

    public WeatherInfo? Current => _lastWeather;
    public string? City => _city;
    public string? ResolvedLocation { get; private set; }
    public bool IsEnabled => _city != null;

    private System.Timers.Timer? _refreshTimer;

    public event Action<WeatherInfo>? OnWeatherUpdated;

    public WeatherService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Clawd", "weather.json");

        LoadSettings();
        if (IsEnabled)
            _ = FetchAsync();

        _refreshTimer = new System.Timers.Timer(_fetchInterval.TotalMilliseconds);
        _refreshTimer.Elapsed += async (_, _) => await RefreshIfNeeded();
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();
    }

    public async Task SetCity(string city)
    {
        _city = city.Trim();
        _lat = null;
        _lon = null;
        SaveSettings();
        await FetchAsync();
    }

    public void Disable()
    {
        _city = null;
        _lat = null;
        _lon = null;
        _lastWeather = null;
        SaveSettings();
        Avalonia.Threading.Dispatcher.UIThread.Post(() => OnWeatherUpdated?.Invoke(null!));
    }

    public async Task FetchAsync()
    {
        if (string.IsNullOrEmpty(_city)) return;

        try
        {
            if (_lat == null || _lon == null)
            {
                var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(_city)}&count=1";
                var geoJson = await Http.GetStringAsync(geoUrl);
                using var geoDoc = JsonDocument.Parse(geoJson);

                if (!geoDoc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                    return;

                var first = results[0];
                _lat = first.GetProperty("latitude").GetDouble();
                _lon = first.GetProperty("longitude").GetDouble();
                var name = first.TryGetProperty("name", out var n) ? n.GetString() : _city;
                var country = first.TryGetProperty("country", out var c) ? c.GetString() : "";
                ResolvedLocation = string.IsNullOrEmpty(country) ? name : $"{name}, {country}";
            }

            var latStr = _lat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lonStr = _lon.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current_weather=true";
            var weatherJson = await Http.GetStringAsync(weatherUrl);
            using var weatherDoc = JsonDocument.Parse(weatherJson);

            var current = weatherDoc.RootElement.GetProperty("current_weather");
            var code = current.GetProperty("weathercode").GetInt32();
            var temp = current.GetProperty("temperature").GetDouble();

            _lastWeather = new WeatherInfo
            {
                Condition = MapCode(code),
                Temperature = temp,
                Description = DescribeCode(code),
                Location = ResolvedLocation ?? _city ?? ""
            };

            _lastFetch = DateTime.Now;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnWeatherUpdated?.Invoke(_lastWeather));
        }
        catch
        {
            // Silently fail — weather is non-critical
        }
    }

    public async Task RefreshIfNeeded()
    {
        if (!IsEnabled) return;
        if (DateTime.Now - _lastFetch > _fetchInterval)
            await FetchAsync();
    }

    private static WeatherCondition MapCode(int code) => code switch
    {
        0 => WeatherCondition.Clear,
        1 or 2 or 3 => WeatherCondition.Cloudy,
        45 or 48 => WeatherCondition.Fog,
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => WeatherCondition.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherCondition.Snow,
        95 or 96 or 99 => WeatherCondition.Thunderstorm,
        _ => WeatherCondition.Unknown
    };

    private static string DescribeCode(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Foggy",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Freezing drizzle",
        61 or 63 or 65 => "Rain",
        66 or 67 => "Freezing rain",
        71 or 73 or 75 => "Snow",
        77 => "Snow grains",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Unknown"
    };

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("city", out var cityProp))
                _city = cityProp.GetString();
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new { city = _city });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }
}
