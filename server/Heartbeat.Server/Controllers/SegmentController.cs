using Heartbeat.Core;
using Heartbeat.Core.DTOs.Segments;
using Heartbeat.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heartbeat.Server.Controllers
{
    /// <summary>
    /// 插件采集器段的上传端点（ADR-017）。数据经 Agent 本地枢纽转发到这里。
    /// </summary>
    [ApiController]
    [Route("api/v1/segments")]
    [Authorize]
    public class SegmentController(
        UsageService usageService,
        DeviceService deviceService,
        ICurrentUserService currentUser) : ControllerBase
    {
        private readonly UsageService _usageService = usageService;
        private readonly DeviceService _deviceService = deviceService;
        private readonly ICurrentUserService _currentUser = currentUser;

        [HttpPost]
        [EndpointName("uploadSegments")]
        public async Task<IActionResult> Upload([FromBody] SegmentUploadRequest request)
        {
            if (request.Segments == null || request.Segments.Count == 0)
                return BadRequest("Segments cannot be empty.");

            // 'system' 保留给内置采集器的 /usage 路径——防插件冒充污染统计互斥轨（ADR-017 §4）。
            if (request.Segments.Any(s => string.Equals(s.Source, ActivitySources.System, StringComparison.OrdinalIgnoreCase)))
                return BadRequest($"Source '{ActivitySources.System}' is reserved for the built-in collector.");

            var userId = _currentUser.GetUserId();
            var hardwareId = Request.Headers[DeviceService.HardwareIdHeader].FirstOrDefault();
            var deviceName = Request.Headers[DeviceService.DeviceNameHeader].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(hardwareId))
                return BadRequest($"Missing {DeviceService.HardwareIdHeader} header.");

            var device = await _deviceService.ResolveByHardwareIdAsync(userId, hardwareId, deviceName);
            await _usageService.SaveSegmentsAsync(device.Id, request.Segments);
            return Ok();
        }
    }
}
