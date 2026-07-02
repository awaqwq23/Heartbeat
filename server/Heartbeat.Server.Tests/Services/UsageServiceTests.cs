using Heartbeat.Core;
using Heartbeat.Core.DTOs.Usage;
using Heartbeat.Server.Data;
using Heartbeat.Server.Entities;
using Heartbeat.Server.Services;
using Heartbeat.Server.Tests.Fixtures;

namespace Heartbeat.Server.Tests.Services;

[Collection("postgres")]
public class UsageServiceTests(PostgresContainerFixture fixture) : PostgresTestBase(fixture)
{
    private long _deviceId;

    protected override async Task SeedAsync(AppDbContext db)
    {
        var device = new Device
        {
            OwnerId = "user-1",
            HardwareId = "hw-1",
            DeviceName = "Test PC"
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        _deviceId = device.Id;
    }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static AppUsageItem Item(string app, DateTimeOffset start, DateTimeOffset end) => new()
    {
        AppName = app,
        StartTime = start,
        EndTime = end
    };

    private ActivitySegment SystemSegment(long appId, string appName, DateTimeOffset start, DateTimeOffset end, string? title = null) => new()
    {
        Id = Guid.CreateVersion7(),
        DeviceId = _deviceId,
        Source = ActivitySources.System,
        IdentityKey = UsageMerger.SystemIdentityKey(appName, title),
        AppId = appId,
        Title = title,
        StartTime = start,
        EndTime = end,
        DurationSeconds = (int)(end - start).TotalSeconds
    };

    [Fact]
    public async Task SaveUsage_ValidRecords_CreatesAppsAndSegments()
    {
        using var db = CreateDbContext();
        var svc = new UsageService(db);

        var start = Now.AddMinutes(-5);
        var end = Now.AddMinutes(-2);
        var request = new UsageUploadRequest
        {
            Usages = [Item("VSCode", start, end)]
        };

        await svc.SaveUsageAsync(_deviceId, request);

        Assert.Single(db.Apps);
        Assert.Equal("VSCode", db.Apps.First().Name);
        var segment = db.ActivitySegments.Single();
        Assert.Equal(ActivitySources.System, segment.Source);
        Assert.Equal(UsageMerger.SystemIdentityKey("VSCode", null), segment.IdentityKey);
        Assert.NotEqual(Guid.Empty, segment.Id);
    }

    [Fact]
    public async Task SaveUsage_FiltersInvalidRecords()
    {
        using var db = CreateDbContext();
        var svc = new UsageService(db);

        var request = new UsageUploadRequest
        {
            Usages =
            [
                Item("", Now.AddMinutes(-5), Now.AddMinutes(-2)),         // empty name
                Item("App", default, Now),                                 // default start
                Item("App", Now.AddMinutes(-2), Now.AddMinutes(-5)),       // end < start
                Item("App", new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero), Now), // year < 2020
                Item("App", Now.AddHours(-25), Now),                       // duration > 24h
                Item("App", Now.AddMinutes(20), Now.AddMinutes(30)),       // future beyond skew
            ]
        };

        await svc.SaveUsageAsync(_deviceId, request);

        Assert.Empty(db.ActivitySegments);
    }

    [Fact]
    public async Task SaveUsage_MergesWithExistingRecord_WhenOverlapping()
    {
        using var db = CreateDbContext();
        var svc = new UsageService(db);

        var app = new App { Name = "VSCode" };
        db.Apps.Add(app);
        await db.SaveChangesAsync();

        var existingStart = Now.AddMinutes(-10);
        var existingEnd = Now.AddMinutes(-5);
        db.ActivitySegments.Add(SystemSegment(app.Id, "VSCode", existingStart, existingEnd));
        await db.SaveChangesAsync();

        // Upload overlapping record
        var newStart = Now.AddMinutes(-6);
        var newEnd = Now.AddMinutes(-3);
        var request = new UsageUploadRequest
        {
            Usages = [Item("VSCode", newStart, newEnd)]
        };

        await svc.SaveUsageAsync(_deviceId, request);

        var segments = db.ActivitySegments.ToList();
        Assert.Single(segments);
        Assert.Equal(existingStart, segments[0].StartTime);
        Assert.Equal(newEnd, segments[0].EndTime);
    }

    [Fact]
    public async Task SaveUsage_DoesNotMerge_WhenGapExceedsTolerance()
    {
        using var db = CreateDbContext();
        var svc = new UsageService(db);

        var app = new App { Name = "VSCode" };
        db.Apps.Add(app);
        await db.SaveChangesAsync();

        db.ActivitySegments.Add(SystemSegment(app.Id, "VSCode", Now.AddMinutes(-15), Now.AddMinutes(-10)));
        await db.SaveChangesAsync();

        // New record starts 5 minutes after existing ends — no merge
        var request = new UsageUploadRequest
        {
            Usages = [Item("VSCode", Now.AddMinutes(-4), Now.AddMinutes(-2))]
        };

        await svc.SaveUsageAsync(_deviceId, request);

        Assert.Equal(2, db.ActivitySegments.Count());
    }

    [Fact]
    public async Task SaveUsage_DoesNotMerge_AcrossDifferentTitles()
    {
        using var db = CreateDbContext();
        var svc = new UsageService(db);

        var app = new App { Name = "msedge" };
        db.Apps.Add(app);
        await db.SaveChangesAsync();

        // 库内最新记录:同 App 但标题不同 → IdentityKey 不同,首尾相连也不续接(ADR-015/017)
        var existingEnd = Now.AddMinutes(-5);
        db.ActivitySegments.Add(SystemSegment(app.Id, "msedge", Now.AddMinutes(-10), existingEnd, "YouTube"));
        await db.SaveChangesAsync();

        var item = Item("msedge", existingEnd, Now.AddMinutes(-3));
        item.Title = "GitHub";
        var request = new UsageUploadRequest { Usages = [item] };

        await svc.SaveUsageAsync(_deviceId, request);

        Assert.Equal(2, db.ActivitySegments.Count());
    }

    [Fact]
    public async Task SaveUsage_CreatesApp_WhenNotExists()
    {
        using var db = CreateDbContext();
        var svc = new UsageService(db);

        var request = new UsageUploadRequest
        {
            Usages =
            [
                Item("NewApp1", Now.AddMinutes(-5), Now.AddMinutes(-3)),
                Item("NewApp2", Now.AddMinutes(-3), Now.AddMinutes(-1))
            ]
        };

        await svc.SaveUsageAsync(_deviceId, request);

        Assert.Equal(2, db.Apps.Count());
        Assert.Equal(2, db.ActivitySegments.Count());
    }

    [Fact]
    public async Task SaveUsage_ReusesExistingApp()
    {
        using var db = CreateDbContext();
        var svc = new UsageService(db);

        db.Apps.Add(new App { Name = "VSCode" });
        await db.SaveChangesAsync();

        var request = new UsageUploadRequest
        {
            Usages = [Item("VSCode", Now.AddMinutes(-5), Now.AddMinutes(-2))]
        };

        await svc.SaveUsageAsync(_deviceId, request);

        Assert.Single(db.Apps);
        Assert.Single(db.ActivitySegments);
    }

    [Fact]
    public async Task SaveUsage_CalculatesDurationSeconds()
    {
        using var db = CreateDbContext();
        var svc = new UsageService(db);

        var start = Now.AddMinutes(-5);
        var end = Now.AddMinutes(-2);
        var request = new UsageUploadRequest
        {
            Usages = [Item("VSCode", start, end)]
        };

        await svc.SaveUsageAsync(_deviceId, request);

        var segment = db.ActivitySegments.Single();
        Assert.Equal((int)(end - start).TotalSeconds, segment.DurationSeconds);
    }
}
