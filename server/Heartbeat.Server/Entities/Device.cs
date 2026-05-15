namespace Heartbeat.Server.Entities
{
    public class Device
    {
        public long Id { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string CurrentApp { get; set; } = string.Empty;
        public DateTimeOffset LastSeen { get; set; }
    }
}
