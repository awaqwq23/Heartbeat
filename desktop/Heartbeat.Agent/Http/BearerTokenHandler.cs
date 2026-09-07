using Heartbeat.Agent.Configuration;
using Heartbeat.Agent.Utils;
using Serilog;
using System.Net.Http.Headers;

namespace Heartbeat.Agent.Http
{
    /// <summary>
    /// 为每个请求注入 X-Hardware-Id 和 X-Device-Name 头。
    /// 仅当用户显式配置 ApiKey 时才通过 TokenManager 获取 Bearer JWT；
    /// 单用户/LocalAccess 部署不需要 AuthService，也不会触发 token exchange。
    /// </summary>
    public class BearerTokenHandler(ConfigManager configManager, IAccessTokenProvider tokenProvider) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var config = configManager.Current;

            // ApiKey 为空是单用户部署的正常状态，直接使用服务端受限的 Agent 摄入身份。
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                else
                    Log.Warning("Agent authentication is configured, but no access token is available.");
            }

            // Inject X-Hardware-Id header
            request.Headers.TryAddWithoutValidation("X-Hardware-Id", MachineIdentity.MachineGuid);

            // Inject X-Device-Name header (URL-encoded to support non-ASCII chars)
            var deviceName = config.DeviceName;
            if (string.IsNullOrEmpty(deviceName))
            {
                deviceName = Environment.MachineName;
            }
            request.Headers.TryAddWithoutValidation("X-Device-Name", Uri.EscapeDataString(deviceName));

            var response = await base.SendAsync(request, cancellationToken);

            // On 401, invalidate cached token so next request will re-exchange
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Log.Warning("Received 401 Unauthorized; invalidating cached token.");
                tokenProvider.InvalidateToken();
            }

            return response;
        }
    }
}
