using Heartbeat.Agent.Storage;
using Heartbeat.Core.DTOs.Input;

namespace Heartbeat.Agent.Tests.Storage;

public class InputEventLocalCacheTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            if (File.Exists(f)) File.Delete(f);
            if (File.Exists(f + ".tmp")) File.Delete(f + ".tmp");
        }
    }

    private string TempPath()
    {
        var p = Path.Combine(Path.GetTempPath(), $"heartbeat-input-{Guid.NewGuid()}.json");
        _tempFiles.Add(p);
        return p;
    }

    private static InputEventItem Item() => new()
    {
        Id = Guid.CreateVersion7(),
        EventType = InputEventType.KeyDown,
        Code = 65,
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Add_ThenLoad_ReturnsItems()
    {
        var cache = new InputEventLocalCache(TempPath());

        cache.Add([Item(), Item()]);

        Assert.Equal(2, cache.Load().Count);
    }

    [Fact]
    public void Add_EmptyList_NoOp()
    {
        var cache = new InputEventLocalCache(TempPath());

        cache.Add([]);

        Assert.Empty(cache.Load());
    }

    [Fact]
    public void Clear_EmptiesCache()
    {
        var cache = new InputEventLocalCache(TempPath());
        cache.Add([Item()]);

        cache.Clear();

        Assert.Empty(cache.Load());
    }

    [Fact]
    public void Persists_AcrossInstances()
    {
        var path = TempPath();
        var cache1 = new InputEventLocalCache(path);
        cache1.Add([Item(), Item(), Item()]);

        var cache2 = new InputEventLocalCache(path);

        Assert.Equal(3, cache2.Load().Count);
    }

    [Fact]
    public void Add_PreservesItemFields()
    {
        var cache = new InputEventLocalCache(TempPath());
        var item = new InputEventItem
        {
            Id = Guid.CreateVersion7(),
            EventType = InputEventType.MouseScroll,
            Code = 2,
            Timestamp = DateTimeOffset.UtcNow
        };

        cache.Add([item]);
        var loaded = cache.Load().Single();

        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal(InputEventType.MouseScroll, loaded.EventType);
        Assert.Equal((short)2, loaded.Code);
    }
}
