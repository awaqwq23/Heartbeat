using Heartbeat.Core.DTOs.Input;
using System.Text.Json;

namespace Heartbeat.Agent.Storage
{
    /// <summary>
    /// 输入事件的离线缓存（ADR-008 同款本地 JSON 文件 + 原子写）。
    /// 与 LocalCache 的区别：纯追加，不做合并（事件靠 Id/UUIDv7 在服务端去重），
    /// 超出上限丢弃最旧。详见 ADR-012。
    /// </summary>
    public class InputEventLocalCache : IInputEventCache, IDisposable
    {
        private readonly string _filePath;
        private readonly ReaderWriterLockSlim _lock = new();
        private List<InputEventItem> _cache;

        /// <summary>缓存最大条数限制（事件量大，给得比 Usage 宽）。</summary>
        private const int MaxCacheSize = 100_000;

        public InputEventLocalCache(string filePath)
        {
            _filePath = filePath;
            _cache = LoadInternal();
        }

        public void Add(List<InputEventItem> items)
        {
            if (items == null || items.Count == 0) return;

            _lock.EnterWriteLock();
            try
            {
                var snapshot = new List<InputEventItem>(_cache);
                _cache.AddRange(items);

                if (_cache.Count > MaxCacheSize)
                {
                    _cache = _cache.GetRange(_cache.Count - MaxCacheSize, MaxCacheSize);
                }

                try
                {
                    SaveInternal();
                }
                catch
                {
                    _cache = snapshot;
                    throw;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public List<InputEventItem> Load()
        {
            _lock.EnterReadLock();
            try
            {
                return new List<InputEventItem>(_cache);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                var snapshot = new List<InputEventItem>(_cache);
                _cache.Clear();

                try
                {
                    SaveInternal();
                }
                catch
                {
                    _cache = snapshot;
                    throw;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void SaveInternal()
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_cache);

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }

        private List<InputEventItem> LoadInternal()
        {
            if (!File.Exists(_filePath)) return [];
            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<InputEventItem>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
