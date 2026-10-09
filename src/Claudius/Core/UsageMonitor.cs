using System.IO;
using System.Windows.Threading;
using Claudius.Shared;

namespace Claudius.Core;

/// <summary>Polls the bridge's usage.json / history.jsonl and raises Changed when they change.</summary>
public sealed class UsageMonitor
{
    private readonly DispatcherTimer _timer = new();
    private DateTime _usageWrite;
    private DateTime _historyWrite;

    public UsageSnapshot? Current { get; private set; }
    public IReadOnlyList<HistorySample> History { get; private set; } = [];

    public event Action? Changed;

    public UsageMonitor()
    {
        _timer.Tick += (_, _) => Poll();
    }

    public void Start(int intervalSeconds)
    {
        SetInterval(intervalSeconds);
        Poll();
        _timer.Start();
    }

    public void SetInterval(int seconds) => _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, seconds));

    public void Poll()
    {
        bool changed = false;
        try
        {
            var usageInfo = new FileInfo(DataPaths.UsageFile);
            if (usageInfo.Exists && usageInfo.LastWriteTimeUtc != _usageWrite)
            {
                var snapshot = UsageStore.TryRead(DataPaths.UsageFile);
                if (snapshot != null)
                {
                    Current = snapshot;
                    _usageWrite = usageInfo.LastWriteTimeUtc;
                    changed = true;
                }
            }

            var historyInfo = new FileInfo(DataPaths.HistoryFile);
            if (historyInfo.Exists && historyInfo.LastWriteTimeUtc != _historyWrite)
            {
                History = UsageStore.ReadHistory();
                _historyWrite = historyInfo.LastWriteTimeUtc;
                changed = true;
            }
        }
        catch (Exception ex)
        {
            Log.Write("Usage-Datei konnte nicht gelesen werden: " + ex.Message);
        }

        if (changed) Changed?.Invoke();
    }
}
