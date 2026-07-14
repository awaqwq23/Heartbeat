namespace Heartbeat.Server.Services
{
    public interface ICurrentUserService
    {
        string GetUserId();
    }

    /// <summary>
    /// 当前用户身份服务（登录暂时禁用）。
    /// 返回配置的默认用户 ID，不依赖 JWT。
    /// 恢复登录后改回从 HttpContext.User 读取 JWT sub claim。
    /// </summary>
    public class CurrentUserService(IConfiguration configuration) : ICurrentUserService
    {
        public const string DefaultUserId = "019d9026-def4-74db-bf9a-f854c16a993e";

        public string GetUserId()
        {
            return configuration.GetValue<string>("DefaultOwnerId") ?? DefaultUserId;
        }
    }
}
