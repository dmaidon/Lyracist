using Lyracist.Models;
using Lyracist.Shared;

namespace Lyracist.Tests;

/// <summary>
/// Covers Lyracist.Shared.RotationHelpers through Lyracist's own Singer model (an
/// ObservableObject with [ObservableProperty] IsCurrent/IsNext/IsPaused/IsInactive), as a
/// counterpart to KSRotation.Tests' coverage of the same helper through SingerEntry.
/// </summary>
public class RotationHelpersSingerTests
{
    private static Singer Active(string name) => new() { Name = name };
    private static Singer Inactive(string name) => new() { Name = name, IsInactive = true };
    private static Singer Paused(string name) => new() { Name = name, IsPaused = true };

    [Fact]
    public void SetCurrentSinger_PromotesEntryAndMarksPreviousCurrentAsNext()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var singers = new List<Singer> { alice, bob, carol };

        // Manually promote Carol (e.g. the KJ's "Set as Current Performer" override).
        RotationHelpers.SetCurrentSinger(singers, carol);

        Assert.True(carol.IsCurrent);
        Assert.False(alice.IsCurrent);
        // The displaced current singer (Alice) should resume as next, not whoever is
        // sequentially after Carol (Bob) — this is the behavior that was being silently
        // undone by KaraokeViewModel re-deriving state after the fact.
        Assert.True(alice.IsNext);
        Assert.False(bob.IsNext);
    }

    [Fact]
    public void SetCurrentSinger_NoPreviousCurrent_FallsBackToSequentialNext()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var singers = new List<Singer> { alice, bob, carol };

        RotationHelpers.SetCurrentSinger(singers, bob);

        Assert.True(bob.IsCurrent);
        Assert.True(carol.IsNext);
        Assert.False(alice.IsNext);
    }

    [Fact]
    public void SetCurrentSinger_PreviousCurrentIsInactive_FallsBackToSequentialNext()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true, IsInactive = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var singers = new List<Singer> { alice, bob, carol };

        RotationHelpers.SetCurrentSinger(singers, bob);

        Assert.True(bob.IsCurrent);
        // Alice is inactive, so she can't become next — Carol (sequentially next) should be.
        Assert.False(alice.IsNext);
        Assert.True(carol.IsNext);
    }

    [Fact]
    public void SetCurrentSinger_ReactivatesPausedOrInactiveEntry()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = Paused("Bob");
        bob.IsInactive = true;
        var singers = new List<Singer> { alice, bob };

        RotationHelpers.SetCurrentSinger(singers, bob);

        Assert.True(bob.IsCurrent);
        Assert.False(bob.IsPaused);
        Assert.False(bob.IsInactive);
    }

    [Fact]
    public void SetCurrentSinger_PromotingTheAlreadyCurrentSinger_IsIdempotent()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = new Singer { Name = "Bob" };
        var singers = new List<Singer> { alice, bob };

        RotationHelpers.SetCurrentSinger(singers, alice);

        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);
    }

    [Fact]
    public void MarkNextSinger_SkipsPausedAndInactiveSingers()
    {
        var singers = new List<Singer>
        {
            Active("Alice"),
            Paused("Bob"),
            Inactive("Carol"),
            Active("Dave")
        };

        RotationHelpers.MarkNextSinger(singers, singers[0]);

        Assert.False(singers[1].IsNext);
        Assert.False(singers[2].IsNext);
        Assert.True(singers[3].IsNext);
    }

    [Fact]
    public void GetNextActiveSingers_ReturnsUpToMaxCountInSequentialOrder()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var carol = Inactive("Carol");
        var dave = new Singer { Name = "Dave" };
        var singers = new List<Singer> { alice, bob, carol, dave };

        var next = RotationHelpers.GetNextActiveSingers(singers, alice, maxCount: 5);

        Assert.Equal(["Bob", "Dave"], next.Select(s => s.Name));
    }
}
