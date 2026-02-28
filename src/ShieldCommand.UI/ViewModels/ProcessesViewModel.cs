using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShieldCommand.Core.Models;
using ShieldCommand.Core.Services;

namespace ShieldCommand.UI.ViewModels;

public partial class ProcessesViewModel : ViewModelBase
{
    private readonly AdbService _adbService;
    private readonly ActivityMonitorViewModel _activityMonitor;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    // Previous snapshot for computing CPU% deltas
    private Dictionary<int, (long Jiffies, string Name)> _prevProcs = new();
    private long _prevTotalJiffies;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private string _statusText = "Not monitoring";

    [ObservableProperty]
    private string _loadText = "";

    public ObservableCollection<ProcessInfo> Processes { get; } = [];

    public ProcessesViewModel(AdbService adbService, ActivityMonitorViewModel activityMonitor)
    {
        _adbService = adbService;
        _activityMonitor = activityMonitor;

        _activityMonitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ActivityMonitorViewModel.SelectedRefreshInterval) && IsMonitoring)
            {
                Stop();
                _ = StartAsync();
            }
        };
    }

    public async Task StartAsync()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(ParseInterval(_activityMonitor.SelectedRefreshInterval));
        StatusText = "Starting...";

        // Take two snapshots back-to-back so the first render has CPU% deltas
        if (_prevProcs.Count == 0)
        {
            var (baseProcs, baseJiffies) = await _adbService.GetProcessSnapshotAsync();
            if (baseProcs.Count > 0)
            {
                _prevProcs = baseProcs;
                _prevTotalJiffies = baseJiffies;
                await Task.Delay(500);
            }
        }

        await PollAsync();
        StartMonitoringLoop();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _timer?.Dispose();
        _timer = null;
        IsMonitoring = false;
        StatusText = "Monitoring stopped";
    }

    private static TimeSpan ParseInterval(string interval) => interval switch
    {
        "1s" => TimeSpan.FromSeconds(1),
        "2s" => TimeSpan.FromSeconds(2),
        "5s" => TimeSpan.FromSeconds(5),
        "10s" => TimeSpan.FromSeconds(10),
        "30s" => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromSeconds(5),
    };

    private void StartMonitoringLoop()
    {
        var timer = _timer;
        var cts = _cts;
        _ = Task.Run(async () =>
        {
            try
            {
                while (timer is not null && await timer.WaitForNextTickAsync(cts!.Token))
                {
                    await PollAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private async Task PollAsync()
    {
        var (procs, totalJiffies) = await _adbService.GetProcessSnapshotAsync();
        if (procs.Count == 0)
            return; // Bad read, skip this cycle

        var processes = new List<ProcessInfo>();
        var deltaTotalJiffies = totalJiffies - _prevTotalJiffies;
        var hasPrev = deltaTotalJiffies > 0 && _prevProcs.Count > 0;

        foreach (var (pid, (jiffies, name)) in procs)
        {
            var cpuPct = 0.0;
            if (hasPrev && _prevProcs.TryGetValue(pid, out var prev))
            {
                var deltaProc = jiffies - prev.Jiffies;
                if (deltaProc > 0)
                    cpuPct = (double)deltaProc / deltaTotalJiffies * 100.0;
            }

            // Skip kernel threads (pid <= 2 or name starts with common kernel prefixes)
            if (pid <= 2) continue;

            processes.Add(new ProcessInfo(pid, name, Math.Round(cpuPct, 1)));
        }

        _prevProcs = procs;
        _prevTotalJiffies = totalJiffies;

        var sorted = processes.OrderByDescending(p => p.CpuPercent).ToList();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Processes.Clear();
            foreach (var p in sorted)
                Processes.Add(p);

            var totalCpu = sorted.Sum(p => p.CpuPercent);
            LoadText = $"Total CPU: {totalCpu:F1}%";
            StatusText = $"{sorted.Count} processes";
        });
    }
}
