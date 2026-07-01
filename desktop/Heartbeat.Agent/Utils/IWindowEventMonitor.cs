namespace Heartbeat.Agent.Utils
{
    public interface IWindowEventMonitor
    {
        event Action<ForegroundWindow>? ForegroundWindowChanged;
        ForegroundWindow GetForegroundWindow();
        void Start();
        void Stop();
    }

    public sealed class WindowsWindowEventMonitor : IWindowEventMonitor
    {
        private Thread? _hookThread;

        public event Action<ForegroundWindow>? ForegroundWindowChanged;

        public ForegroundWindow GetForegroundWindow()
            => ActiveWindowHelper.GetForegroundWindowSample();

        public void Start()
        {
            ActiveWindowHelper.ForegroundWindowChanged += OnChanged;
            _hookThread = new Thread(() =>
            {
                try { ActiveWindowHelper.StartHook(); }
                catch (Exception ex) { Serilog.Log.Error(ex, "WinEvent 钩子线程异常"); }
            })
            {
                IsBackground = true,
                Name = "WinEventHookThread"
            };
            _hookThread.Start();
        }

        public void Stop()
        {
            ActiveWindowHelper.ForegroundWindowChanged -= OnChanged;
            ActiveWindowHelper.StopHook();
            _hookThread?.Join(TimeSpan.FromSeconds(3));
        }

        private void OnChanged(ForegroundWindow fw)
            => ForegroundWindowChanged?.Invoke(fw);
    }
}
