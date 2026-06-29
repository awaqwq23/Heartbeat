using Heartbeat.Agent.Http;
using Heartbeat.Agent.Storage;
using Heartbeat.Core.DTOs.Input;
using Serilog;

namespace Heartbeat.Agent.Services
{
    public class InputEventUploadService(HeartbeatApiClient apiClient, IInputEventCache cache)
    {
        private static InputEventUploadRequest MapToDto(List<InputEventItem> items)
            => new() { Events = items };

        public async Task UploadAsync(List<InputEventItem> events)
        {
            if (events.Count == 0) return;

            var dto = MapToDto(events);

            Log.Information("正在上传 {Count} 条输入事件...", events.Count);
            var result = await apiClient.UploadInputEventsAsync(dto);
            if (!result.Success)
            {
                cache.Add(events);
                Log.Information("{Count} 条输入事件已缓存到本地", events.Count);
                return;
            }
            Log.Information("输入事件上传成功，共 {Count} 条", events.Count);
        }

        public async Task UploadCachedAsync()
        {
            var cached = cache.Load();
            if (cached.Count == 0) return;

            Log.Information("发现 {Count} 条缓存输入事件，尝试上传...", cached.Count);
            var dto = MapToDto(cached);

            var result = await apiClient.UploadInputEventsAsync(dto);
            if (!result.Success) return;

            cache.Clear();
            Log.Information("缓存输入事件上传成功，已清除本地缓存");
        }
    }
}
