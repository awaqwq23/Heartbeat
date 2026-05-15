namespace Heartbeat.Agent.Http
{
    public interface IAccessTokenProvider
    {
        Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
        void InvalidateToken();
    }
}
