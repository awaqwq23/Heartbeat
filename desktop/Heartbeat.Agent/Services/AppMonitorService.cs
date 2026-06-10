using Heartbeat.Agent.Utils;
using Heartbeat.Core.DTOs.Usage;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Heartbeat.Agent.Services
{
    public class AppMonitorService(IClock clock, IWindowEventMonitor windowMonitor) : IHostedService, IDisposable
    {
        private readonly object _lock = new();
        private string? _currentApp;
        private DateTimeOffset _currentStart;
        private readonly List<AppUsageItem> _usages = [];

        public event Action<string?>? CurrentAppChanged;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Information("应用监测服务启动");

            windowMonitor.ForegroundWindowChanged += OnForegroundChanged;

            var initialApp = windowMonitor.GetForegroundProcessName();
            if (initialApp != null)
            {
                lock (_lock)
                {
                    _currentApp = initialApp;
                    _currentStart = clock.UtcNow;
                    Log.Information("初始前台应用: {App}", initialApp);
                }
            }

            windowMonitor.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Information("应用监测服务停止");
            windowMonitor.ForegroundWindowChanged -= OnForegroundChanged;
            windowMonitor.Stop();
            return Task.CompletedTask;
        }

        private void OnForegroundChanged(string? newApp)
        {
            var now = clock.UtcNow;

            lock (_lock)
            {
                if (string.Equals(_currentApp, newApp, StringComparison.OrdinalIgnoreCase))
                    return;

                if (_currentApp != null && _currentStart != default)
                {
                    var duration = now - _currentStart;
                    if (duration.TotalSeconds >= 1)
                    {
                        _usages.Add(new AppUsageItem
                        {
                            AppName = _currentApp,
                            StartTime = _currentStart,
                            EndTime = now
                        });
                        Log.Debug("应用结束: {App}，时长 {Duration:F1}s", _currentApp, duration.TotalSeconds);
                    }
                }

                _currentApp = newApp;
                _currentStart = now;

                if (newApp != null)
                {
                    Log.Debug("应用切换: {App}", newApp);
                }
            }

            CurrentAppChanged?.Invoke(newApp);
        }

        public string? GetCurrentApp()
        {
            lock (_lock)
            {
                return _currentApp;
            }
        }

        public List<AppUsageItem> GetAndClearUsages()
        {
            var now = clock.UtcNow;

            lock (_lock)
            {
                if (_currentApp != null && _currentStart != default)
                {
                    var duration = now - _currentStart;
                    if (duration.TotalSeconds >= 1)
                    {
                        _usages.Add(new AppUsageItem
                        {
                            AppName = _currentApp,
                            StartTime = _currentStart,
                            EndTime = now
                        });
                    }
                    _currentStart = now;
                }

                var copy = new List<AppUsageItem>(_usages);
                _usages.Clear();

                Log.Information("收集到 {Count} 条使用记录，准备上传", copy.Count);
                foreach (var item in copy)
                {
                    Log.Debug("  {App}: {Start:HH:mm:ss} - {End:HH:mm:ss} ({Duration:F1}s)",
                        item.AppName, item.StartTime.LocalDateTime, item.EndTime.LocalDateTime,
                        (item.EndTime - item.StartTime).TotalSeconds);
                }

                return copy;
            }
        }

        public void Dispose()
        {
            windowMonitor.ForegroundWindowChanged -= OnForegroundChanged;
            windowMonitor.Stop();
            GC.SuppressFinalize(this);
        }
    }
}
