namespace Heartbeat.Agent.Utils
{
    /// <summary>
    /// 前台窗口的一次采样：进程名 + 窗口标题。
    /// 作为多信号采集的容器，将来可扩展更多信号（音频、全屏等）。
    /// </summary>
    public readonly record struct ForegroundWindow(string? ProcessName, string? Title)
    {
        public static readonly ForegroundWindow None = new(null, null);
    }
}
