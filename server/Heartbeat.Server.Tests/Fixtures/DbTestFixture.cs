using Heartbeat.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Tests.Fixtures
{
    public class DbTestFixture : IDisposable
    {
        private readonly string _databaseName;

        public DbTestFixture()
        {
            _databaseName = Guid.NewGuid().ToString();
        }

        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;

            return new AppDbContext(options);
        }

        public void Dispose()
        {
            // InMemory database is disposed when all contexts are disposed
        }
    }
}
