using Heartbeat.Core;
using Heartbeat.Core.DTOs.Reports;
using Heartbeat.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Services
{
    public class ReportService(AppDbContext db)
    {
        private readonly AppDbContext _db = db;

        public async Task<DailyReportResponse> GetDailyReportAsync(string ownerId, long? deviceId, DateTimeOffset date)
        {
            var range = DateRange.Day(date);
            var apps = await AggregateAsync(ownerId, deviceId, range);

            return new DailyReportResponse
            {
                Date = date.Date.ToString("yyyy-MM-dd"),
                Apps = apps
            };
        }

        public async Task<WeeklyReportResponse> GetWeeklyReportAsync(string ownerId, long? deviceId, DateTimeOffset date)
        {
            var range = DateRange.Week(date);
            var apps = await AggregateAsync(ownerId, deviceId, range);

            var d = date.Date;
            var dayOfWeek = d.DayOfWeek;
            var mondayOffset = dayOfWeek == DayOfWeek.Sunday ? -6 : -(int)dayOfWeek + 1;
            var monday = d.AddDays(mondayOffset);

            return new WeeklyReportResponse
            {
                WeekStart = monday.ToString("yyyy-MM-dd"),
                WeekEnd = monday.AddDays(6).ToString("yyyy-MM-dd"),
                Apps = apps
            };
        }

        private async Task<List<AppDurationItem>> AggregateAsync(string ownerId, long? deviceId, DateRange range)
        {
            // 统计只消费 system source（互斥轨，时长可求和）。插件段只进回放。详见 ADR-017 §4。
            var query = _db.ActivitySegments
                .Where(x => x.Device.OwnerId == ownerId)
                .Where(x => x.Source == ActivitySources.System)
                .Where(x => x.StartTime >= range.UtcStart && x.StartTime < range.UtcEnd);

            if (deviceId.HasValue)
                query = query.Where(x => x.DeviceId == deviceId.Value);

            return await query
                .GroupBy(x => new { x.AppId, AppName = x.App!.Name })
                .Select(g => new AppDurationItem
                {
                    AppId = g.Key.AppId!.Value,
                    AppName = g.Key.AppName,
                    DurationSeconds = g.Sum(x => x.DurationSeconds)
                })
                .OrderByDescending(x => x.DurationSeconds)
                .ToListAsync();
        }
    }
}
