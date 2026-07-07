using Heartbeat.Core.DTOs.Apps;
using Heartbeat.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heartbeat.Server.Controllers
{
    /// <summary>
    /// system 段的查询投影（Dashboard Timeline 数据源）。
    /// 上传入口已收敛为 POST /segments（ADR-020）——本控制器只读。
    /// </summary>
    [ApiController]
    [Route("api/v1/usage")]
    [Authorize]
    public class UsageController(UsageService usageService, ICurrentUserService currentUser) : ControllerBase
    {
        private readonly UsageService _usageService = usageService;
        private readonly ICurrentUserService _currentUser = currentUser;

        [HttpGet]
        [EndpointName("getUsage")]
        public async Task<ActionResult<List<AppUsageResponse>>> GetUsage(
            [FromQuery] long? deviceId,
            [FromQuery] DateTimeOffset? start,
            [FromQuery] DateTimeOffset? end)
        {
            var userId = _currentUser.GetUserId();
            return await _usageService.GetUsageAsync(userId, deviceId, start, end);
        }
    }
}
