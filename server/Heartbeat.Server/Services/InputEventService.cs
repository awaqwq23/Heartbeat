using Heartbeat.Core.DTOs.Input;
using Heartbeat.Server.Data;
using Heartbeat.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Services
{
    public class InputEventService(AppDbContext db)
    {
        private readonly AppDbContext _db = db;

        /// <summary>
        /// 批量保存输入事件。基于 Id (UUIDv7) 去重，重复上传幂等。
        /// </summary>
        public async Task SaveAsync(long deviceId, InputEventUploadRequest request)
        {
            // 批内按 Id 去重
            var items = request.Events
                .GroupBy(e => e.Id)
                .Select(g => g.First())
                .ToList();

            if (items.Count == 0) return;

            // 过滤掉库中已存在的 Id（幂等：重传整批不会重复插入）
            var ids = items.Select(e => e.Id).ToList();
            var existing = await _db.InputEvents
                .Where(e => ids.Contains(e.Id))
                .Select(e => e.Id)
                .ToHashSetAsync();

            var toInsert = items
                .Where(e => !existing.Contains(e.Id))
                .Select(e => new InputEvent
                {
                    Id = e.Id,
                    DeviceId = deviceId,
                    EventType = e.EventType,
                    Code = e.Code,
                    Timestamp = e.Timestamp
                });

            _db.InputEvents.AddRange(toInsert);
            await _db.SaveChangesAsync();
        }
    }
}
