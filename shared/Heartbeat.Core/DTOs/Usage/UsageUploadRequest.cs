namespace Heartbeat.Core.DTOs.Usage
{
    public class UsageUploadRequest
    {
        public List<AppUsageItem> Usages { get; set; } = [];
    }

    public class AppUsageItem
    {
        public string AppName { get; set; } = string.Empty;

        /// <summary>窗口标题（段级细分维度，可空）。详见 ADR-015。</summary>
        public string? Title { get; set; }

        public DateTimeOffset StartTime { get; set; }

        public DateTimeOffset EndTime { get; set; }
    }
}
