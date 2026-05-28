using System.IdentityModel.Tokens.Jwt;

namespace Heartbeat.Server.Services
{
    public interface ICurrentUserService
    {
        string GetUserId();
    }

    public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
    {
        public string GetUserId()
        {
            var userId = httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User ID not found in token.");
            return userId;
        }
    }
}
