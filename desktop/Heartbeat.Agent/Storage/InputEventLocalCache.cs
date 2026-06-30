using Heartbeat.Core.DTOs.Input;

namespace Heartbeat.Agent.Storage
{
    /// <summary>
    /// 输入事件的离线缓存。基于 <see cref="JsonFileCache{T}"/>，
    /// 行为：紧凑 JSON、上限 100000 条、纯追加不合并
    /// （事件靠 Id/UUIDv7 在服务端去重）。详见 ADR-012。
    /// </summary>
    public class InputEventLocalCache : IInputEventCache, IDisposable
    {
        private const int MaxCacheSize = 100_000;

        private readonly JsonFileCache<InputEventItem> _cache;

        public InputEventLocalCache(string filePath)
        {
            _cache = new JsonFileCache<InputEventItem>(filePath, MaxCacheSize);
        }

        public void Add(List<InputEventItem> items) => _cache.Add(items);

        public List<InputEventItem> Load() => _cache.Load();

        public void Clear() => _cache.Clear();

        public void Dispose() => _cache.Dispose();
    }
}
