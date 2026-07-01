namespace Heartbeat.Server.Entities
{
    public class AppUsage
    {
        public long Id { get; set; }
        public long DeviceId { get; set; }
        public long AppId { get; set; }
        public string? Title { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public int DurationSeconds { get; set; }

        public Device Device { get; set; } = null!;
        public App App { get; set; } = null!;
    }
}
