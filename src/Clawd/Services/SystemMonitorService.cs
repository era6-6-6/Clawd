using System.Diagnostics;

namespace Clawd.Services;

public class SystemStats
{
    public double CpuPercent { get; init; }
    public double MemoryUsedPercent { get; init; }
}

public class SystemMonitorService : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private SystemStats _last = new() { CpuPercent = 0, MemoryUsedPercent = 0 };
    private TimeSpan _prevCpuTime;
    private DateTime _prevSampleTime;
    private readonly int _processorCount;

    public SystemStats Current => _last;
    public event Action<SystemStats>? OnUpdated;

    public SystemMonitorService()
    {
        _processorCount = Environment.ProcessorCount;
        _prevSampleTime = DateTime.UtcNow;

        try
        {
            using var proc = Process.GetCurrentProcess();
            _prevCpuTime = proc.TotalProcessorTime;
        }
        catch { }

        _timer = new System.Timers.Timer(5000); // sample every 5s
        _timer.Elapsed += (_, _) => Sample();
        _timer.AutoReset = true;
        _timer.Start();

        // Get initial system-wide stats
        _ = Task.Run(SampleSystemWide);
    }

    private void Sample()
    {
        _ = Task.Run(SampleSystemWide);
    }

    private void SampleSystemWide()
    {
        try
        {
            double cpu = 0;
            double mem = 0;

            if (OperatingSystem.IsMacOS())
            {
                // Use top for CPU on macOS
                var cpuResult = RunCommand("sh", "-c \"top -l 1 -n 0 | grep 'CPU usage'\"");
                if (cpuResult != null)
                {
                    // "CPU usage: 5.26% user, 4.60% sys, 90.13% idle"
                    var idleMatch = System.Text.RegularExpressions.Regex.Match(cpuResult, @"([\d.]+)%\s*idle");
                    if (idleMatch.Success && double.TryParse(idleMatch.Groups[1].Value,
                        System.Globalization.CultureInfo.InvariantCulture, out var idle))
                        cpu = 100 - idle;
                }

                // Memory from vm_stat
                var memResult = RunCommand("sh", "-c \"memory_pressure | head -1\"");
                if (memResult != null)
                {
                    // "The system has 8589934592 (2097152 pages with a page size of 4096)."
                    // Fallback: use sysctl
                    var memResult2 = RunCommand("sh", "-c \"sysctl -n hw.memsize && vm_stat\"");
                    if (memResult2 != null)
                    {
                        var lines = memResult2.Split('\n');
                        if (lines.Length > 0 && long.TryParse(lines[0].Trim(), out var totalBytes))
                        {
                            long freePages = 0;
                            foreach (var line in lines)
                            {
                                if (line.Contains("Pages free"))
                                {
                                    var m = System.Text.RegularExpressions.Regex.Match(line, @":\s*(\d+)");
                                    if (m.Success) freePages += long.Parse(m.Groups[1].Value);
                                }
                                if (line.Contains("Pages inactive"))
                                {
                                    var m = System.Text.RegularExpressions.Regex.Match(line, @":\s*(\d+)");
                                    if (m.Success) freePages += long.Parse(m.Groups[1].Value);
                                }
                            }
                            var freeBytes = freePages * 4096L;
                            mem = (1.0 - (double)freeBytes / totalBytes) * 100;
                        }
                    }
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                // /proc/stat for CPU
                var stat1 = File.ReadAllText("/proc/stat").Split('\n')[0];
                Thread.Sleep(1000);
                var stat2 = File.ReadAllText("/proc/stat").Split('\n')[0];
                cpu = CalculateLinuxCpu(stat1, stat2);

                // /proc/meminfo for memory
                var memInfo = File.ReadAllText("/proc/meminfo");
                var total = ExtractMemValue(memInfo, "MemTotal");
                var available = ExtractMemValue(memInfo, "MemAvailable");
                if (total > 0)
                    mem = (1.0 - (double)available / total) * 100;
            }

            _last = new SystemStats
            {
                CpuPercent = Math.Clamp(cpu, 0, 100),
                MemoryUsedPercent = Math.Clamp(mem, 0, 100)
            };

            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnUpdated?.Invoke(_last));
        }
        catch { }
    }

    private static double CalculateLinuxCpu(string line1, string line2)
    {
        var p1 = ParseCpuLine(line1);
        var p2 = ParseCpuLine(line2);
        if (p1 == null || p2 == null) return 0;

        var totalDelta = p2.Value.total - p1.Value.total;
        var idleDelta = p2.Value.idle - p1.Value.idle;
        if (totalDelta == 0) return 0;
        return (1.0 - (double)idleDelta / totalDelta) * 100;
    }

    private static (long total, long idle)? ParseCpuLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return null;
        long total = 0;
        for (var i = 1; i < parts.Length; i++)
            if (long.TryParse(parts[i], out var v)) total += v;
        long.TryParse(parts[4], out var idle);
        return (total, idle);
    }

    private static long ExtractMemValue(string memInfo, string key)
    {
        foreach (var line in memInfo.Split('\n'))
            if (line.StartsWith(key))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)");
                if (m.Success) return long.Parse(m.Groups[1].Value);
            }
        return 0;
    }

    private static string? RunCommand(string cmd, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(cmd, args)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return output;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
