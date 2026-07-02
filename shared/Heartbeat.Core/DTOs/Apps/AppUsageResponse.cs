namespace Heartbeat.Core.DTOs.Apps
{
    public class AppUsageResponse
    {
        /// <summary>段 Id（UUIDv7，采集端生成，ADR-017）。</summary>
        public Guid Id { get; set; }
        public long AppId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public int DurationSeconds { get; set; }
    }
}
