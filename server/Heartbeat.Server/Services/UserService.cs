using Heartbeat.Server.Data;
using Heartbeat.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Services
{
    /// <summary>
    /// 用户解析服务（登录暂时禁用）。
    /// 不再依赖外部 AuthService，改为在本地自动创建用户。
    /// 默认用户 awaqwq233 使用与 CurrentUserService 一致的固定 ID，
    /// 确保 Agent 上传的数据（OwnerId）与前端查询的用户 ID 匹配。
    /// </summary>
    public class UserService(AppDbContext db)
    {
        private readonly AppDbContext _db = db;

        public async Task<User?> ResolveByUsernameAsync(string username)
        {
            // 先在本地查找
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user != null) return user;

            // 默认用户使用固定 ID，与 CurrentUserService 保持一致
            var id = username == "awaqwq233"
                ? CurrentUserService.DefaultUserId
                : Guid.NewGuid().ToString();

            // 找不到则自动创建（不再调用外部 AuthService）
            user = new User
            {
                Id = id,
                Username = username,
                LastSeenAt = DateTimeOffset.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return user;
        }
    }
}
