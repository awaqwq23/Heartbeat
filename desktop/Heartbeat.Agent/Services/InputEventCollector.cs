using Heartbeat.Agent.Utils;
using Heartbeat.Core.DTOs.Input;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Heartbeat.Agent.Services
{
    /// <summary>
    /// 输入事件采集服务。在专用线程上运行低级键鼠钩子（与 WinEvent 线程隔离），
    /// 将原始钩子事件翻译为 InputEventBuffer 的语义调用。详见 ADR-012。
    /// </summary>
    public sealed class InputEventCollector(IClock clock, ILowLevelInputHook hook, IInputActivitySignal inputActivity) : IHostedService, IDisposable
    {
        private readonly ILowLevelInputHook _hook = hook;
        private readonly IInputActivitySignal _inputActivity = inputActivity;
        private readonly InputEventBuffer _buffer = new(clock);
        private Thread? _hookThread;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Information("输入事件采集服务启动");

            _hook.KeyDown += OnKeyDown;
            _hook.KeyUp += OnKeyUp;
            _hook.MouseButton += OnMouseButton;
            _hook.Scroll += OnScroll;

            _hookThread = new Thread(() =>
            {
                try { _hook.StartHook(); }
                catch (Exception ex) { Log.Error(ex, "低级输入钩子线程异常"); }
            })
            {
                IsBackground = true,
                Name = "InputHookThread"
            };
            _hookThread.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Information("输入事件采集服务停止");
            Unsubscribe();
            _hook.StopHook();
            _hookThread?.Join(TimeSpan.FromSeconds(3));
            return Task.CompletedTask;
        }

        /// <summary>取走当前缓冲的所有输入事件（供上传调度调用）。</summary>
        public List<InputEventItem> GetAndClearEvents()
        {
            var events = _buffer.DrainAll();
            if (events.Count > 0)
                Log.Information("收集到 {Count} 条输入事件，准备上传", events.Count);
            return events;
        }

        /// <summary>退回重注入（ADR-020 上传通道契约）：保 Id 回队，服务端幂等去重。</summary>
        public void Requeue(List<InputEventItem> events) => _buffer.Requeue(events);

        // 回调保持最小工作：仅转发给 buffer（buffer 内部为并发安全的轻量操作）
        private void OnKeyDown(int vk) => _buffer.OnKeyDown(vk);
        private void OnKeyUp(int vk) => _buffer.OnKeyUp(vk);
        private void OnMouseButton(short code)
        {
            // 点击（含触摸板点击）标记输入活动，供标题变化门控使用（ADR-016）。
            _inputActivity.MarkClick();
            _buffer.OnMouseButton(code);
        }
        private void OnScroll(int delta) => _buffer.OnScroll(delta);

        private void Unsubscribe()
        {
            _hook.KeyDown -= OnKeyDown;
            _hook.KeyUp -= OnKeyUp;
            _hook.MouseButton -= OnMouseButton;
            _hook.Scroll -= OnScroll;
        }

        public void Dispose()
        {
            Unsubscribe();
            _hook.StopHook();
            GC.SuppressFinalize(this);
        }
    }
}
