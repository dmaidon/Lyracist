// Created on Oct 7, 2026 @ 19:46:00 -> Unit tests for SamplePad configuration and item handling
using System.IO;
using System.Text.Json;
using Lyracist.Core.Interfaces;
using Xunit;

namespace Lyracist.Tests;

public class SamplePadServiceTests
{
    [Fact]
    public void SamplePadItem_MissingFile_ReportsIsMissingTrue()
    {
        var item = new SamplePadItem
        {
            SlotIndex = 0,
            Label = "Test Pad",
            FilePath = "C:\\nonexistent_folder_abc123\\missing_sample.wav"
        };

        Assert.True(item.IsMissing);
        Assert.True(item.IsAssigned);
    }

    [Fact]
    public void SamplePadItem_EmptyPath_ReportsNotAssignedAndNotMissing()
    {
        var item = new SamplePadItem
        {
            SlotIndex = 0,
            Label = "Pad 1",
            FilePath = string.Empty
        };

        Assert.False(item.IsAssigned);
        Assert.False(item.IsMissing);
    }

    [Fact]
    public void SamplePadConfig_SerializationRoundTrip_PreservesProperties()
    {
        var items = new List<SamplePadItem>
        {
            new()
            {
                SlotIndex = 0,
                Label = "Air Horn",
                FilePath = "C:\\sounds\\airhorn.mp3",
                Color = "#EF4444",
                HotKey = "F1",
                Volume = 0.85,
                Order = 0
            },
            new()
            {
                SlotIndex = 1,
                Label = "Rimshot",
                FilePath = "C:\\sounds\\rimshot.mp3",
                Color = "#10B981",
                HotKey = "F2",
                Volume = 1.0,
                Order = 1
            }
        };

        string json = JsonSerializer.Serialize(items);
        var deserialized = JsonSerializer.Deserialize<List<SamplePadItem>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("Air Horn", deserialized[0].Label);
        Assert.Equal("#EF4444", deserialized[0].Color);
        Assert.Equal("F1", deserialized[0].HotKey);
        Assert.Equal(0.85, deserialized[0].Volume);
        Assert.Equal("Rimshot", deserialized[1].Label);
    }
}
