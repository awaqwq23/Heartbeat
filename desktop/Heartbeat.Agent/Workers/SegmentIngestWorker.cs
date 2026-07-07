using Heartbeat.Agent.Configuration;
using Heartbeat.Agent.Services;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Net;

namespace Heartbeat.Agent.Workers
{
    /// <summary>
    /// 本地 ingest 枢纽（ADR-017）：在 loopback 上开 HTTP 接口，接收插件采集器
    /// （浏览器扩展 / VSCode 插件 / 游戏模组）推送的已折叠段，进入统一上传管线。
    /// 仅绑定 127.0.0.1——信任模型为"本机进程可信"（单用户自部署，ADR-017 §1）。
    /// 协议逻辑（路由/解析/守卫/状态码）在 SegmentIngestRequestHandler，
    /// 本类只负责 HttpListener 生命周期与上下文搬运（ADR-020）。
    /// </summary>
    public class SegmentIngestWorker(
        SegmentIngestRequestHandler handler,
        ConfigManager configManager) : BackgroundService
    {
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
            var response = await handler.HandleAsync(req.HttpMethod, req.Url?.AbsolutePath, req.InputStream);
            TryRespond(ctx, response.StatusCode, response.Body, response.IsJson);
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
