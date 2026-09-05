using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.ViewModels;
using Moq;

namespace Lyracist.Tests;

/// <summary>
/// Regression tests proving RotationViewModel recalculates estimated wait-time badges as soon as a
/// singer is added or marked current - not only after the first "done" - so a fresh rotation isn't
/// left showing no badges at all until someone finishes a song. See CHANGELOG 26.9.5.10.
/// </summary>
public class EstimatedWaitTimeViewModelTests
{
    private static Singer MakeSinger(string name) => new() { Name = name };

    private static RotationViewModel CreateRotationViewModel()
    {
        var display = new Mock<IDisplayService>();
        var mediaEngine = new Mock<IMediaEngine>();
        var vm = new RotationViewModel(display.Object, mediaEngine.Object);
        var field = typeof(RotationViewModel).GetField("_floatCurrentSingerToTop",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("RotationViewModel._floatCurrentSingerToTop field not found.");
        field.SetValue(vm, false);
        vm.Rotation.Clear();
        vm.InactiveSingers.Clear();
        return vm;
    }

    [Fact]
    public void SetCurrentSinger_RecalculatesWaitsImmediately_BeforeAnyoneIsDone()
    {
        var vm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        vm.Rotation.Add(alice);
        vm.Rotation.Add(bob);

        vm.SetCurrentSingerCommand.Execute(alice);

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        Assert.True(bob.EstimatedWaitMinutes > 0, "Bob should have a nonzero estimated wait as soon as Alice is marked current, without anyone having finished a song yet.");
    }

    [Fact]
    public void AddSinger_ToRotationWithExistingCurrentSinger_GetsAWaitEstimateImmediately()
    {
        var vm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        vm.Rotation.Add(alice);
        vm.SetCurrentSingerCommand.Execute(alice);

        vm.NewSingerName = "Bob";
        vm.AddSingerCommand.Execute(null);

        var bob = vm.Rotation.Single(s => s.Name == "Bob");
        Assert.True(bob.EstimatedWaitMinutes > 0, "Bob should get a wait estimate as soon as he's added, since Alice is already current.");
    }
}
