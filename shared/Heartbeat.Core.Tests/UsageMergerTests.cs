using Heartbeat.Core;
using Heartbeat.Core.DTOs.Usage;

namespace Heartbeat.Core.Tests;

public class UsageMergerTests
{
    private static readonly DateTimeOffset Base = new(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private static AppUsageItem Item(string app, int startSec, int endSec) => new()
    {
        AppName = app,
        StartTime = Base.AddSeconds(startSec),
        EndTime = Base.AddSeconds(endSec)
    };

    [Fact]
    public void EmptyList_ReturnsEmpty()
    {
        var result = UsageMerger.Merge([]);
        Assert.Empty(result);
    }

    [Fact]
    public void SingleItem_ReturnsSame()
    {
        var input = new List<AppUsageItem> { Item("VSCode", 0, 60) };
        var result = UsageMerger.Merge(input);

        Assert.Single(result);
        Assert.Equal("VSCode", result[0].AppName);
    }

    [Fact]
    public void SameApp_Adjacent_WithinTolerance_Merges()
    {
        var input = new List<AppUsageItem>
        {
            Item("VSCode", 0, 60),
            Item("VSCode", 61, 120) // gap = 1s, exactly at tolerance
        };

        var result = UsageMerger.Merge(input);

        Assert.Single(result);
        Assert.Equal(Base, result[0].StartTime);
        Assert.Equal(Base.AddSeconds(120), result[0].EndTime);
    }

    [Fact]
    public void SameApp_Gap_ExceedsTolerance_NoMerge()
    {
        var input = new List<AppUsageItem>
        {
            Item("VSCode", 0, 60),
            Item("VSCode", 62, 120) // gap = 2s, exceeds 1s tolerance
        };

        var result = UsageMerger.Merge(input);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void DifferentApps_NeverMerge()
    {
        var input = new List<AppUsageItem>
        {
            Item("VSCode", 0, 60),
            Item("Chrome", 60, 120) // same boundary, different app
        };

        var result = UsageMerger.Merge(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("VSCode", result[0].AppName);
        Assert.Equal("Chrome", result[1].AppName);
    }

    [Fact]
    public void SameApp_Overlapping_Merges()
    {
        var input = new List<AppUsageItem>
        {
            Item("VSCode", 0, 60),
            Item("VSCode", 30, 90) // overlaps
        };

        var result = UsageMerger.Merge(input);

        Assert.Single(result);
        Assert.Equal(Base, result[0].StartTime);
        Assert.Equal(Base.AddSeconds(90), result[0].EndTime);
    }

    [Fact]
    public void SameApp_CompletelyContained_NoExtension()
    {
        var input = new List<AppUsageItem>
        {
            Item("VSCode", 0, 120),
            Item("VSCode", 30, 60) // fully inside first
        };

        var result = UsageMerger.Merge(input);

        Assert.Single(result);
        Assert.Equal(Base.AddSeconds(120), result[0].EndTime);
    }

    [Fact]
    public void UnsortedInput_SortsBeforeMerge()
    {
        var input = new List<AppUsageItem>
        {
            Item("VSCode", 61, 120),
            Item("VSCode", 0, 60)
        };

        var result = UsageMerger.Merge(input);

        Assert.Single(result);
        Assert.Equal(Base, result[0].StartTime);
        Assert.Equal(Base.AddSeconds(120), result[0].EndTime);
    }

    [Fact]
    public void CaseInsensitive_AppName_Merges()
    {
        var input = new List<AppUsageItem>
        {
            Item("vscode", 0, 60),
            Item("VSCode", 61, 120)
        };

        var result = UsageMerger.Merge(input);

        Assert.Single(result);
    }

    [Fact]
    public void DuplicateUpload_IdenticalRecords_Merges()
    {
        var input = new List<AppUsageItem>
        {
            Item("VSCode", 0, 60),
            Item("VSCode", 0, 60) // exact duplicate
        };

        var result = UsageMerger.Merge(input);

        Assert.Single(result);
        Assert.Equal(Base, result[0].StartTime);
        Assert.Equal(Base.AddSeconds(60), result[0].EndTime);
    }

    [Fact]
    public void DoesNotMutateInput()
    {
        var input = new List<AppUsageItem>
        {
            Item("VSCode", 0, 60),
            Item("VSCode", 61, 120)
        };
        var originalStart = input[0].StartTime;
        var originalEnd = input[1].EndTime;

        UsageMerger.Merge(input);

        Assert.Equal(originalStart, input[0].StartTime);
        Assert.Equal(originalEnd, input[1].EndTime);
        Assert.Equal(2, input.Count);
    }
}
