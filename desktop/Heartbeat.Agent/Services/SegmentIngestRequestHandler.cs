using Heartbeat.Core;
using Heartbeat.Core.DTOs.Segments;
using System.Text.Json;

namespace Heartbeat.Agent.Services
{
    /// <summary>
    /// loopback ingest 的协议层（ADR-020）：路由、JSON 解析、冒充守卫、状态码映射。
    /// 与 HttpListener 解耦——插件作者的 HTTP 契约（状态码/错误体/accepted 计数）在此可测。
    /// 冒充守卫（拒收 'system'）放在这一层而非缓冲模块：它防的是"谁在调"（本机进程
    /// 冒充内置采集器污染统计互斥轨），是传输信任问题；缓冲模块对 source 无关，
    /// 内置采集器进程内直调不经此层。
    /// </summary>
    public class SegmentIngestRequestHandler(SegmentIngestService ingestService)
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public sealed record Response(int StatusCode, string Body, bool IsJson);

        public async Task<Response> HandleAsync(string httpMethod, string? path, Stream body)
        {
            if (httpMethod != "POST" || path != "/v1/segments")
                return new Response(404, "not found; POST /v1/segments", false);

            SegmentUploadRequest? dto;
            try
            {
                dto = await JsonSerializer.DeserializeAsync<SegmentUploadRequest>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return new Response(400, "invalid JSON", false);
            }

            if (dto?.Segments == null || dto.Segments.Count == 0)
                return new Response(400, "segments cannot be empty", false);

            if (dto.Segments.Any(s => string.Equals(s.Source, ActivitySources.System, StringComparison.OrdinalIgnoreCase)))
                return new Response(400, $"Source '{ActivitySources.System}' is reserved for the built-in collector.", false);

            var accepted = ingestService.Accept(dto.Segments);
            return new Response(200, $"{{\"accepted\":{accepted}}}", true);
        }
    }
}
