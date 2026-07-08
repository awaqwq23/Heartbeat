using Heartbeat.Agent.Http;
using Heartbeat.Agent.Storage;
using Serilog;

namespace Heartbeat.Agent.Services
{
    /// <summary>
    /// 上传通道（Upload Channel，ADR-020）：泛化的出网通道，契约为
    /// "送达，或落离线缓存，否则原样退回"。退回项由调用方重注入源 buffer
    /// （段按 Id 收敛天然幂等，输入事件保 Id 重排队）——drain 后的批不允许静默蒸发。
    /// compact 为按流策略，只作用于出缓存的批：缓存纯追加，离线期间积累同 Id 快照；
    /// fresh 批来自按 Id 键控的 buffer，天然无重复。
    /// </summary>
    public class UploadChannel<T>(
        string label,
        Func<List<T>, Task<ApiResult>> send,
        ICache<T> cache,
        Func<List<T>, List<T>>? compactCached = null)
    {
        /// <summary>上传一批 fresh 项。返回既没送达也没缓存住的项（调用方必须重注入），正常为空。</summary>
        public async Task<List<T>> UploadAsync(List<T> items)
        {
            if (items.Count == 0) return [];

            Log.Information("正在上传 {Count} 条{Label}...", items.Count, label);
            var result = await send(items);
            if (result.Success)
            {
                Log.Information("{Label}上传成功，共 {Count} 条", label, items.Count);
                return [];
            }

            try
            {
                cache.Add(items);
                Log.Information("{Count} 条{Label}已缓存到本地", items.Count, label);
                return [];
            }
            catch (Exception ex)
            {
                // 缓存写盘失败（磁盘满等）：不吞数据，原样退回。
                Log.Warning(ex, "{Label}缓存写入失败，{Count} 条退回调用方", label, items.Count);
                return items;
            }
        }

        /// <summary>重传离线缓存。成功清空缓存，失败原样保留（下个周期再试，ADR-008）。</summary>
        public async Task UploadCachedAsync()
        {
            var cached = cache.Load();
            if (cached.Count == 0) return;

            var toSend = compactCached?.Invoke(cached) ?? cached;

            Log.Information("发现 {Count} 条缓存{Label}，尝试上传...", toSend.Count, label);
            var result = await send(toSend);
            if (!result.Success) return;

            cache.Clear();
            Log.Information("缓存{Label}上传成功，已清除本地缓存", label);
        }
    }
}
