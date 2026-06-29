using System.Net;
using Heartbeat.Agent.Configuration;
using Heartbeat.Agent.Http;
using Heartbeat.Agent.Services;
using Heartbeat.Agent.Storage;
using Heartbeat.Core.DTOs.Input;

namespace Heartbeat.Agent.Tests.Services;

public class InputEventUploadServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private sealed class FakeCache : IInputEventCache
    {
        public List<InputEventItem> Items { get; private set; } = [];
        public int ClearCount { get; private set; }
        public void Add(List<InputEventItem> items) => Items.AddRange(items);
        public List<InputEventItem> Load() => new(Items);
        public void Clear() { Items = []; ClearCount++; }
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status));
    }

    private HeartbeatApiClient CreateClient(HttpStatusCode status)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"heartbeat-cfg-{Guid.NewGuid()}.json");
        _tempFiles.Add(tempPath);
        var cm = new ConfigManager(tempPath);
        cm.Update(c => c.ApiBaseUrl = "http://localhost");

        var http = new HttpClient(new StubHandler(status));
        return new HeartbeatApiClient(http, cm);
    }

    private static InputEventItem Item() => new()
    {
        Id = Guid.CreateVersion7(),
        EventType = InputEventType.KeyDown,
        Code = 65,
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task UploadAsync_Success_DoesNotCache()
    {
        var cache = new FakeCache();
        var svc = new InputEventUploadService(CreateClient(HttpStatusCode.OK), cache);

        await svc.UploadAsync([Item(), Item()]);

        Assert.Empty(cache.Items);
    }

    [Fact]
    public async Task UploadAsync_Failure_CachesEvents()
    {
        var cache = new FakeCache();
        var svc = new InputEventUploadService(CreateClient(HttpStatusCode.InternalServerError), cache);

        await svc.UploadAsync([Item(), Item()]);

        Assert.Equal(2, cache.Items.Count);
    }

    [Fact]
    public async Task UploadAsync_EmptyList_NoOp()
    {
        var cache = new FakeCache();
        var svc = new InputEventUploadService(CreateClient(HttpStatusCode.InternalServerError), cache);

        await svc.UploadAsync([]);

        Assert.Empty(cache.Items);
    }

    [Fact]
    public async Task UploadCachedAsync_Success_ClearsCache()
    {
        var cache = new FakeCache();
        cache.Add([Item(), Item()]);
        var svc = new InputEventUploadService(CreateClient(HttpStatusCode.OK), cache);

        await svc.UploadCachedAsync();

        Assert.Empty(cache.Items);
        Assert.Equal(1, cache.ClearCount);
    }

    [Fact]
    public async Task UploadCachedAsync_Failure_KeepsCache()
    {
        var cache = new FakeCache();
        cache.Add([Item(), Item()]);
        var svc = new InputEventUploadService(CreateClient(HttpStatusCode.InternalServerError), cache);

        await svc.UploadCachedAsync();

        Assert.Equal(2, cache.Items.Count);
        Assert.Equal(0, cache.ClearCount);
    }

    [Fact]
    public async Task UploadCachedAsync_EmptyCache_NoOp()
    {
        var cache = new FakeCache();
        var svc = new InputEventUploadService(CreateClient(HttpStatusCode.OK), cache);

        await svc.UploadCachedAsync();

        Assert.Equal(0, cache.ClearCount);
    }
}
