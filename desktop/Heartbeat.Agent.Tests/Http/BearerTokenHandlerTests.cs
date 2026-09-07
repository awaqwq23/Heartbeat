using Heartbeat.Agent.Configuration;
using Heartbeat.Agent.Http;
using Heartbeat.Agent.Models;
using System.Net.Http.Headers;

namespace Heartbeat.Agent.Tests.Http;

public class BearerTokenHandlerTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public async Task SendAsync_DeviceNameEmpty_SendsMachineNameHeader()
    {
        var configManager = CreateConfigManager(new AgentConfig { DeviceName = "" });
        var capturingHandler = new CapturingHandler();
        var handler = new BearerTokenHandler(configManager, new FakeTokenProvider("jwt"))
        {
            InnerHandler = capturingHandler
        };

        var client = new HttpClient(handler);
        await client.GetAsync("http://localhost/test");

        var header = capturingHandler.CapturedRequest!.Headers
            .GetValues("X-Device-Name").FirstOrDefault();

        Assert.NotNull(header);
        Assert.Equal(Environment.MachineName, Uri.UnescapeDataString(header));
    }

    [Fact]
    public async Task SendAsync_DeviceNameSet_SendsConfiguredName()
    {
        var configManager = CreateConfigManager(new AgentConfig { DeviceName = "我的电脑" });
        var capturingHandler = new CapturingHandler();
        var handler = new BearerTokenHandler(configManager, new FakeTokenProvider("jwt"))
        {
            InnerHandler = capturingHandler
        };

        var client = new HttpClient(handler);
        await client.GetAsync("http://localhost/test");

        var header = capturingHandler.CapturedRequest!.Headers
            .GetValues("X-Device-Name").FirstOrDefault();

        Assert.NotNull(header);
        Assert.Equal("我的电脑", Uri.UnescapeDataString(header));
    }

    [Fact]
    public async Task SendAsync_NoApiKey_SkipsTokenExchangeAndSendsDeviceIdentity()
    {
        var configManager = CreateConfigManager(new AgentConfig { ApiKey = "", DeviceName = "desktop-2" });
        var tokenProvider = new FakeTokenProvider("jwt");
        var capturingHandler = new CapturingHandler();
        var handler = new BearerTokenHandler(configManager, tokenProvider)
        {
            InnerHandler = capturingHandler
        };

        var client = new HttpClient(handler);
        await client.PostAsync("http://localhost/test", null);

        var request = capturingHandler.CapturedRequest!;
        Assert.Equal(0, tokenProvider.GetTokenCallCount);
        Assert.Null(request.Headers.Authorization);
        Assert.False(string.IsNullOrWhiteSpace(
            request.Headers.GetValues("X-Hardware-Id").Single()));
        Assert.Equal("desktop-2", Uri.UnescapeDataString(
            request.Headers.GetValues("X-Device-Name").Single()));
    }

    [Fact]
    public async Task SendAsync_ApiKeyConfigured_AddsBearerToken()
    {
        var configManager = CreateConfigManager(new AgentConfig { ApiKey = "configured" });
        var tokenProvider = new FakeTokenProvider("jwt");
        var capturingHandler = new CapturingHandler();
        var handler = new BearerTokenHandler(configManager, tokenProvider)
        {
            InnerHandler = capturingHandler
        };

        var client = new HttpClient(handler);
        await client.GetAsync("http://localhost/test");

        Assert.Equal(1, tokenProvider.GetTokenCallCount);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "jwt"),
            capturingHandler.CapturedRequest!.Headers.Authorization);
    }

    private ConfigManager CreateConfigManager(AgentConfig config)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"heartbeat-test-{Guid.NewGuid()}.json");
        _tempFiles.Add(tempPath);
        var cm = new ConfigManager(tempPath);
        cm.Update(c =>
        {
            c.ApiKey = config.ApiKey;
            c.DeviceName = config.DeviceName;
        });
        return cm;
    }

    private class FakeTokenProvider(string token) : IAccessTokenProvider
    {
        public int GetTokenCallCount { get; private set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
        {
            GetTokenCallCount++;
            return Task.FromResult<string?>(token);
        }
        public void InvalidateToken() { }
    }

    private class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
