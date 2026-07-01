using Heartbeat.Core;
using Heartbeat.Core.DTOs.Apps;
using Heartbeat.Core.DTOs.Usage;
using Heartbeat.Server.Data;
using Heartbeat.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Services
{
    public class UsageService(AppDbContext db)
    {
        private readonly AppDbContext _db = db;

        public async Task SaveUsageAsync(long deviceId, UsageUploadRequest request)
        {
            var validUsages = UsageValidationPolicy.Filter(request.Usages, DateTimeOffset.UtcNow);

            if (validUsages.Count == 0) return;

            // 获取或创建 App 记录
            var appNames = validUsages.Select(u => u.AppName).Distinct().ToList();
            var existingApps = await _db.Apps
                .Where(a => appNames.Contains(a.Name))
                .ToDictionaryAsync(a => a.Name);

            foreach (var name in appNames)
            {
                if (!existingApps.ContainsKey(name))
                {
                    var app = new App { Name = name };
                    _db.Apps.Add(app);
                    existingApps[name] = app;
                }
            }
            await _db.SaveChangesAsync(); // 保存以获取新 App 的 Id

            var first = validUsages[0];
            var firstAppId = existingApps[first.AppName].Id;
            var firstMerged = false;

            // 查该设备+同应用的最新记录，利用 (DeviceId, AppId, EndTime) 索引
            var lastRecord = await _db.AppUsages
                .Where(x => x.DeviceId == deviceId && x.AppId == firstAppId)
                .OrderByDescending(x => x.EndTime)
                .FirstOrDefaultAsync();

            // 合并判据与客户端共用同一真源（同 App + 同 Title + 时间相连）。详见 ADR-015。
            // lastRecord 已按 AppId == firstAppId 过滤，故其 App 名必等于 first.AppName，
            // 直接复用 first.AppName 避免额外加载 App 导航属性。
            if (lastRecord != null
                && UsageMerger.CanMerge(
                    first.AppName, lastRecord.Title, lastRecord.EndTime,
                    first.AppName, first.Title, first.StartTime)
                && first.EndTime >= lastRecord.StartTime)
            {
                // 批次首条与数据库最新记录同应用+同标题且重叠或首尾相连 → 合并
                if (first.StartTime < lastRecord.StartTime)
                    lastRecord.StartTime = first.StartTime;
                if (first.EndTime > lastRecord.EndTime)
                    lastRecord.EndTime = first.EndTime;
                lastRecord.DurationSeconds = (int)(lastRecord.EndTime - lastRecord.StartTime).TotalSeconds;
                firstMerged = true;
            }

            // 其余记录直接插入
            foreach (var u in validUsages.Skip(firstMerged ? 1 : 0))
            {
                var appId = existingApps[u.AppName].Id;
                _db.AppUsages.Add(new AppUsage
                {
                    DeviceId = deviceId,
                    AppId = appId,
                    Title = u.Title,
                    StartTime = u.StartTime,
                    EndTime = u.EndTime,
                    DurationSeconds = (int)(u.EndTime - u.StartTime).TotalSeconds
                });
            }

            await _db.SaveChangesAsync();
        }

        public async Task<List<AppUsageResponse>> GetUsageAsync(string ownerId, long? deviceId, DateTimeOffset? start, DateTimeOffset? end)
        {
            var query = _db.AppUsages
                .Include(x => x.App)
                .Where(x => x.Device.OwnerId == ownerId)
                .AsQueryable();

            if (deviceId.HasValue)
                query = query.Where(x => x.DeviceId == deviceId.Value);

            if (start.HasValue)
                query = query.Where(x => x.StartTime >= start.Value);

            if (end.HasValue)
                query = query.Where(x => x.StartTime < end.Value);

            return await query
                .OrderByDescending(x => x.StartTime)
                .Take(10000)
                .Select(x => new AppUsageResponse
                {
                    Id = x.Id,
                    AppId = x.AppId,
                    AppName = x.App.Name,
                    Title = x.Title,
                    StartTime = x.StartTime,
                    EndTime = x.EndTime,
                    DurationSeconds = x.DurationSeconds
                })
                .ToListAsync();
        }
    }
}
