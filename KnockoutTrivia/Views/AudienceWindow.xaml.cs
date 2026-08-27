// Created on Aug 27, 2026 @ 15:26:50 -> AudienceWindow code-behind with window control handlers
using System.Windows;
using System.Windows.Input;

namespace KnockoutTrivia.Views;

public partial class AudienceWindow : Window
{
    public AudienceWindow()
    {
        InitializeComponent();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnToggleFullscreenClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                Hide();
            }
        }
    }
}
