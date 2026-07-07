using Heartbeat.Agent.Services;
using System.Text;

namespace Heartbeat.Agent.Tests.Services;

/// <summary>
/// loopback ingest 协议契约（ADR-020）：插件作者看到的状态码、错误体、accepted 计数。
/// </summary>
public class SegmentIngestRequestHandlerTests
{
    private sealed class FakeClock : Heartbeat.Agent.Utils.IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private readonly SegmentIngestService _ingest;
    private readonly SegmentIngestRequestHandler _handler;

    public SegmentIngestRequestHandlerTests()
    {
        _ingest = new SegmentIngestService(new FakeClock());
        _handler = new SegmentIngestRequestHandler(_ingest);
    }

    private static Stream Body(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    private static string SegmentJson(string source = "browser", string identityKey = "https://example.com")
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        return $$"""
            {
              "id": "{{Guid.CreateVersion7()}}",
              "source": "{{source}}",
              "identityKey": "{{identityKey}}",
              "startTime": "{{start:O}}",
              "endTime": "{{start.AddMinutes(2):O}}"
            }
            """;
    }

    [Fact]
    public async Task WrongPath_404()
    {
        var response = await _handler.HandleAsync("POST", "/v1/other", Body("{}"));

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("POST /v1/segments", response.Body);
    }

    [Fact]
    public async Task WrongMethod_404()
    {
        var response = await _handler.HandleAsync("GET", "/v1/segments", Body("{}"));

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task InvalidJson_400()
    {
        var response = await _handler.HandleAsync("POST", "/v1/segments", Body("{not json"));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("invalid JSON", response.Body);
    }

    [Fact]
    public async Task EmptySegments_400()
    {
        var missing = await _handler.HandleAsync("POST", "/v1/segments", Body("{}"));
        var empty = await _handler.HandleAsync("POST", "/v1/segments", Body("""{"segments":[]}"""));

        Assert.Equal(400, missing.StatusCode);
        Assert.Equal(400, empty.StatusCode);
        Assert.Equal("segments cannot be empty", empty.Body);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("System")]
    public async Task SystemSource_400_NothingBuffered(string source)
    {
        // 冒充守卫：loopback 来的 'system' 段整批拒收，缓冲不留痕。
        var json = $$"""{"segments":[{{SegmentJson()}},{{SegmentJson(source: source)}}]}""";

        var response = await _handler.HandleAsync("POST", "/v1/segments", Body(json));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("reserved", response.Body);
        Assert.Empty(_ingest.GetAndClearSegments());
    }

    [Fact]
    public async Task ValidBatch_200_AcceptedCount()
    {
        var json = $$"""{"segments":[{{SegmentJson()}},{{SegmentJson(identityKey: "https://other.com")}}]}""";

        var response = await _handler.HandleAsync("POST", "/v1/segments", Body(json));

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.IsJson);
        Assert.Equal("""{"accepted":2}""", response.Body);
        Assert.Equal(2, _ingest.GetAndClearSegments().Count);
    }

    [Fact]
    public async Task InvalidSegmentsFiltered_200_AcceptedZero()
    {
        // 校验丢弃不是错误：契约是 200 + accepted 计数，采集端据此发现数据被丢。
        var json = $$"""{"segments":[{{SegmentJson(identityKey: "")}}]}""";

        var response = await _handler.HandleAsync("POST", "/v1/segments", Body(json));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"accepted":0}""", response.Body);
    }
}
