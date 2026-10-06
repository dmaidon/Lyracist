// Created on Oct 6, 2026 @ 10:24:30 -> Add unit tests for SingleInstanceHelper
using System;
using System.Threading;
using Lyracist.Shared;
using Xunit;

namespace KSRotation.Tests;

public class SingleInstanceHelperTests : IDisposable
{
    private readonly string _testAppKey = "TestApp_" + Guid.NewGuid().ToString("N")[..8];

    public void Dispose()
    {
        SingleInstanceHelper.Cleanup();
    }

    [Fact]
    public void EnsureSingleInstance_FirstCall_ReturnsTrue()
    {
        bool isFirst = SingleInstanceHelper.EnsureSingleInstance(_testAppKey);
        Assert.True(isFirst);
    }

    [Fact]
    public void IsInstanceRunning_ReflectsStateCorrectly()
    {
        Assert.False(SingleInstanceHelper.IsInstanceRunning(_testAppKey));

        SingleInstanceHelper.EnsureSingleInstance(_testAppKey);

        Assert.True(SingleInstanceHelper.IsInstanceRunning(_testAppKey));

        SingleInstanceHelper.Cleanup();

        Assert.False(SingleInstanceHelper.IsInstanceRunning(_testAppKey));
    }

    [Fact]
    public void EnsureSingleInstance_ThrowsOnEmptyOrNull()
    {
        Assert.Throws<ArgumentException>(() => SingleInstanceHelper.EnsureSingleInstance(""));
        Assert.Throws<ArgumentNullException>(() => SingleInstanceHelper.EnsureSingleInstance(null!));
    }
}
