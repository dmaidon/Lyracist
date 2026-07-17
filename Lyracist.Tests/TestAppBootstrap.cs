using System.Runtime.CompilerServices;

namespace Lyracist.Tests;

/// <summary>
/// Some ViewModel code paths (e.g. RotationViewModel's test-mode seeding) call
/// System.Windows.Application.Current.Dispatcher, which is null outside a running WPF app.
/// Constructing a bare Application (never calling Run()) gives Current a valid Dispatcher without
/// pumping a message loop or touching any user files/settings.
/// </summary>
internal static class TestAppBootstrap
{
    [ModuleInitializer]
    internal static void EnsureWpfApplication()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application();
        }
    }
}
