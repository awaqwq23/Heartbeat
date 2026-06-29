using Heartbeat.Core.DTOs.Input;
using Heartbeat.Server.Data;
using Heartbeat.Server.Entities;
using Heartbeat.Server.Services;
using Heartbeat.Server.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Tests.Services;

[Collection("postgres")]
public class InputEventServiceTests(PostgresContainerFixture fixture) : PostgresTestBase(fixture)
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

    private static InputEventItem Item(InputEventType type, short code, DateTimeOffset ts) => new()
    {
        Id = Guid.CreateVersion7(),
        EventType = type,
        Code = code,
        Timestamp = ts
    };

    [Fact]
    public async Task SaveAsync_InsertsAllEvents()
    {
        using var db = CreateDbContext();
        var svc = new InputEventService(db);

        var now = DateTimeOffset.UtcNow;
        var request = new InputEventUploadRequest
        {
            Events =
            [
                Item(InputEventType.KeyDown, 65, now),
                Item(InputEventType.MouseButton, 1, now.AddMilliseconds(10)),
                Item(InputEventType.MouseScroll, 2, now.AddMilliseconds(20)),
            ]
        };

        await svc.SaveAsync(_deviceId, request);

        Assert.Equal(3, await db.InputEvents.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_IsIdempotent_WhenSameBatchUploadedTwice()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new InputEventUploadRequest
        {
            Events =
            [
                Item(InputEventType.KeyDown, 65, now),
                Item(InputEventType.KeyDown, 66, now.AddMilliseconds(10)),
            ]
        };

        // 第一次上传
        using (var db = CreateDbContext())
        {
            await new InputEventService(db).SaveAsync(_deviceId, request);
        }

        // 重传同一批（相同 Id）
        using (var db = CreateDbContext())
        {
            await new InputEventService(db).SaveAsync(_deviceId, request);
        }

        using (var db = CreateDbContext())
        {
            Assert.Equal(2, await db.InputEvents.CountAsync());
        }
    }

    [Fact]
    public async Task SaveAsync_DedupsWithinBatch()
    {
        using var db = CreateDbContext();
        var svc = new InputEventService(db);

        var dup = Item(InputEventType.KeyDown, 65, DateTimeOffset.UtcNow);
        var request = new InputEventUploadRequest
        {
            Events = [dup, dup]
        };

        await svc.SaveAsync(_deviceId, request);

        Assert.Equal(1, await db.InputEvents.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_PersistsFieldsCorrectly()
    {
        using var db = CreateDbContext();
        var svc = new InputEventService(db);

        var ts = DateTimeOffset.UtcNow;
        var item = Item(InputEventType.MouseScroll, 1, ts);
        await svc.SaveAsync(_deviceId, new InputEventUploadRequest { Events = [item] });

        var saved = await db.InputEvents.SingleAsync();
        Assert.Equal(item.Id, saved.Id);
        Assert.Equal(_deviceId, saved.DeviceId);
        Assert.Equal(InputEventType.MouseScroll, saved.EventType);
        Assert.Equal((short)1, saved.Code);
    }

    [Fact]
    public async Task SaveAsync_EmptyBatch_DoesNothing()
    {
        using var db = CreateDbContext();
        var svc = new InputEventService(db);

        await svc.SaveAsync(_deviceId, new InputEventUploadRequest { Events = [] });

        Assert.Equal(0, await db.InputEvents.CountAsync());
    }
}
