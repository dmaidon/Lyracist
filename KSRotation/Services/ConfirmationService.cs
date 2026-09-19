// Created on Sep 19, 2026 @ 19:10:00 -> Yes/No confirmation helper usable from code shared with KSRotation.Maui
using System.Threading.Tasks;

namespace KSRotation.Services
{
    /// <summary>
    /// Awaitable Yes/No confirmation dialog. Exists so MainViewModel (linked into both KSRotation
    /// and KSRotation.Maui) can prompt the user without referencing WPF types directly - the MAUI
    /// build gets its own implementation of this same type via Shims/WpfShims.cs.
    /// </summary>
    public static class ConfirmationService
    {
        public static async Task<bool> ShowYesNoAsync(string message, string caption)
        {
            // Fully qualified throughout: this project also references System.Windows.Forms,
            // whose Application/MessageBox/MessageBoxButton/etc. types would otherwise collide
            // with the WPF ones (System.Windows.Forms is presumably pulled in for some other
            // interop need elsewhere in KSRotation - not this file's concern).
            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                System.Windows.MessageBox.Show(
                    message,
                    caption,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes);
        }
    }
}
