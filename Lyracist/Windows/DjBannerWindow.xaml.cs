// Edited on Aug 1, 2026 @ 09:52:00 -> Fix KeyEventArgs ambiguity
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class DjBannerWindow : Window
{
    private readonly DjBannerWindowViewModel _vm;

    public DjBannerWindow(DjBannerWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
