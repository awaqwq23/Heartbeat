using Heartbeat.Agent.Configuration;
using Heartbeat.Agent.Services;
using Heartbeat.Core.DTOs.Segments;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Net;
using System.Text.Json;

namespace Heartbeat.Agent.Workers
{
    /// <summary>
    /// 本地 ingest 枢纽（ADR-017）：在 loopback 上开 HTTP 接口，接收插件采集器
    /// （浏览器扩展 / VSCode 插件 / 游戏模组）推送的已折叠段，进入统一上传管线。
    /// 仅绑定 127.0.0.1——信任模型为"本机进程可信"（单用户自部署，ADR-017 §1）。
    /// </summary>
    public class SegmentIngestWorker(
        SegmentIngestService ingestService,
        ConfigManager configManager) : BackgroundService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var port = configManager.Current.IngestPort;
            if (port <= 0)
            {
                Log.Information("本地 ingest 枢纽未启用（ingestPort = {Port}）", port);
                return;
            }

            using var listener = new HttpListener();
            // loopback 限定：非本机流量到不了这个前缀。
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                Log.Error(ex, "本地 ingest 枢纽启动失败（端口 {Port} 可能被占用），插件采集不可用", port);
                return;
            }

            Log.Information("本地 ingest 枢纽已启动: http://127.0.0.1:{Port}/", port);

            using var _ = stoppingToken.Register(listener.Stop);

            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync();
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested)
                {
                    break; // Stop() 会让 GetContextAsync 抛出
                }

                // 逐请求串行处理即可：本机插件低频小批量，无并发压力。
                try
                {
                    await HandleRequestAsync(ctx);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "ingest 请求处理异常");
                    TryRespond(ctx, 500, "internal error");
                }
            }

            Log.Information("本地 ingest 枢纽已停止");
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            var req = ctx.Request;

            if (req.HttpMethod != "POST" || req.Url?.AbsolutePath != "/v1/segments")
            {
                TryRespond(ctx, 404, "not found; POST /v1/segments");
                return;
            }

            SegmentUploadRequest? dto;
            try
            {
                dto = await JsonSerializer.DeserializeAsync<SegmentUploadRequest>(req.InputStream, JsonOptions);
            }
            catch (JsonException)
            {
                TryRespond(ctx, 400, "invalid JSON");
                return;
            }

            if (dto?.Segments == null || dto.Segments.Count == 0)
            {
                TryRespond(ctx, 400, "segments cannot be empty");
                return;
            }

            try
            {
                var accepted = ingestService.Accept(dto.Segments);
                TryRespond(ctx, 200, $"{{\"accepted\":{accepted}}}", json: true);
            }
            catch (InvalidSourceException ex)
            {
                TryRespond(ctx, 400, ex.Message);
            }
        }

        private static void TryRespond(HttpListenerContext ctx, int status, string body, bool json = false)
        {
            try
            {
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = json ? "application/json" : "text/plain";
                using var writer = new StreamWriter(ctx.Response.OutputStream);
                writer.Write(body);
            }
            catch
            {
                // 客户端可能已断开；忽略。
            }
        }
    }
}
