using Heartbeat.Agent.Configuration;
using Heartbeat.Agent.Http;
using Heartbeat.Agent.Services;
using Heartbeat.Agent.Storage;
using Heartbeat.Core;
using Heartbeat.Core.DTOs.Segments;
using System.Net;

namespace Heartbeat.Agent.Tests.Services;

/// <summary>
/// 上传通道契约（ADR-020）：送达，或落离线缓存，否则原样退回。
/// 经真实 HeartbeatApiClient + 桩 HttpMessageHandler 驱动，传输层（URL/负载）一并覆盖。
/// </summary>
public class UploadChannelTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private sealed class FakeCache : ICache<ActivitySegmentItem>
    {
        public List<ActivitySegmentItem> Items { get; private set; } = [];
        public int ClearCount { get; private set; }
        public bool ThrowOnAdd { get; set; }

        public void Add(List<ActivitySegmentItem> items)
        {
            if (ThrowOnAdd) throw new IOException("disk full");
            Items.AddRange(items);
        }

        public List<ActivitySegmentItem> Load() => new(Items);
        public void Clear() { Items = []; ClearCount++; }
    }

    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), body));
            return new HttpResponseMessage(status);
        }
    }

    private (UploadChannel<ActivitySegmentItem> channel, FakeCache cache, CapturingHandler handler) Build(HttpStatusCode status)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"heartbeat-cfg-{Guid.NewGuid()}.json");
        _tempFiles.Add(tempPath);
        var cm = new ConfigManager(tempPath);
        cm.Update(c => c.ApiBaseUrl = "http://localhost");

        var handler = new CapturingHandler(status);
        var api = new HeartbeatApiClient(new HttpClient(handler), cm);
        var cache = new FakeCache();

        // 与 AgentHostExtensions 的段通道同构：compact 策略 = KeepLatest，只作用于出缓存的批
        var channel = new UploadChannel<ActivitySegmentItem>(
            "段",
            batch => api.UploadSegmentsAsync(new SegmentUploadRequest { Segments = batch }),
            cache,
            SnapshotCompaction.KeepLatest);
        return (channel, cache, handler);
    }

    private static ActivitySegmentItem Segment(Guid? id = null, int endSec = 60)
    {
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        return new ActivitySegmentItem
        {
            Id = id ?? Guid.CreateVersion7(),
            Source = "browser",
            IdentityKey = "https://example.com",
            StartTime = t0,
            EndTime = t0.AddSeconds(endSec)
        };
    }

    [Fact]
    public async Task Upload_Success_HitsSegmentsEndpoint_DoesNotCache()
    {
        var (channel, cache, handler) = Build(HttpStatusCode.OK);

        var returned = await channel.UploadAsync([Segment(), Segment()]);

        Assert.Empty(returned);
        Assert.Empty(cache.Items);
        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/api/v1/segments", req.Url);
    }

    [Fact]
    public async Task Upload_Failure_CachesItems_ReturnsEmpty()
    {
        var (channel, cache, _) = Build(HttpStatusCode.InternalServerError);

        var returned = await channel.UploadAsync([Segment(), Segment()]);

        Assert.Empty(returned);
        Assert.Equal(2, cache.Items.Count);
    }

    [Fact]
    public async Task Upload_FailureAndCacheWriteFails_ReturnsItemsIntact()
    {
        // drain-then-fail 修复（ADR-020）：既没送达也没缓存住 → 原样退回，调用方重注入
        var (channel, cache, _) = Build(HttpStatusCode.InternalServerError);
        cache.ThrowOnAdd = true;
        var items = new List<ActivitySegmentItem> { Segment(), Segment() };

        var returned = await channel.UploadAsync(items);

        Assert.Equal(2, returned.Count);
        Assert.Same(items[0], returned[0]); // 原样退回，不复制不丢字段
    }

    [Fact]
    public async Task Upload_EmptyBatch_NoRequest()
    {
        var (channel, _, handler) = Build(HttpStatusCode.OK);

        var returned = await channel.UploadAsync([]);

        Assert.Empty(returned);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UploadCached_Success_ClearsCache()
    {
        var (channel, cache, _) = Build(HttpStatusCode.OK);
        cache.Add([Segment(), Segment()]);

        await channel.UploadCachedAsync();

        Assert.Empty(cache.Items);
        Assert.Equal(1, cache.ClearCount);
    }

    [Fact]
    public async Task UploadCached_Failure_KeepsCache()
    {
        var (channel, cache, _) = Build(HttpStatusCode.InternalServerError);
        cache.Add([Segment(), Segment()]);

        await channel.UploadCachedAsync();

        Assert.Equal(2, cache.Items.Count);
        Assert.Equal(0, cache.ClearCount);
    }

    [Fact]
    public async Task UploadCached_CompactsSameIdSnapshots_BeforeSend()
    {
        // 缓存纯追加，离线期间积累同 Id 快照 → 出网前 KeepLatest（ADR-018）
        var (channel, cache, handler) = Build(HttpStatusCode.OK);
        var id = Guid.CreateVersion7();
        cache.Add([Segment(id, endSec: 30), Segment(id, endSec: 60), Segment(id, endSec: 90)]);

        await channel.UploadCachedAsync();

        var req = Assert.Single(handler.Requests);
        var sent = System.Text.Json.JsonSerializer.Deserialize<SegmentUploadRequest>(
            req.Body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var single = Assert.Single(sent.Segments);
        Assert.Equal(id, single.Id);
    }

    [Fact]
    public async Task UploadCached_EmptyCache_NoRequest()
    {
        var (channel, _, handler) = Build(HttpStatusCode.OK);

        await channel.UploadCachedAsync();

        Assert.Empty(handler.Requests);
    }
}
