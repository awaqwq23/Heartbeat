namespace Heartbeat.Core.DTOs.Reports
{
    public class DailyReportResponse
    {
        public string Date { get; set; } = string.Empty;
        public List<AppDurationItem> Apps { get; set; } = [];
    }

    public class AppDurationItem
    {
        public long AppId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
    }
}
