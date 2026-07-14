using Heartbeat.Server.Data;
using Heartbeat.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Services
{
    /// <summary>
    /// 用户解析服务（登录暂时禁用）。
    /// 不再依赖外部 AuthService，改为在本地自动创建用户。
    /// PublicUserController 依赖此服务按 username 查找用户信息。
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

            // 找不到则自动创建（不再调用外部 AuthService）
            user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                LastSeenAt = DateTimeOffset.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return user;
        }
    }
}
