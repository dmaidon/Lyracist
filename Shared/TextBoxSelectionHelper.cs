// Created on Aug 21, 2026 @ 08:27:00 -> Global TextBox and PasswordBox select-all on focus helper with explicit WPF type aliases
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfPasswordBox = System.Windows.Controls.PasswordBox;

namespace Lyracist.Shared;

/// <summary>
/// Provides global class event handler registration so that whenever any <see cref="WpfTextBox"/>,
/// <see cref="WpfPasswordBox"/>, numeric input box, or editable control in the application receives focus
/// or is clicked into from an unfocused state, all text inside it is immediately selected and highlighted.
/// </summary>
public static class TextBoxSelectionHelper
{
    private static bool _initialized;

    /// <summary>
    /// Registers global class handlers for all <see cref="WpfTextBox"/> and <see cref="WpfPasswordBox"/>
    /// controls across the entire application domain / window hierarchy.
    /// </summary>
    public static void EnableGlobalSelectAllOnFocus()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        // Handle keyboard focus navigation (Tab, programmatic Focus(), etc.)
        EventManager.RegisterClassHandler(
            typeof(WpfTextBox),
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnTextBoxGotKeyboardFocus),
            handledEventsToo: true);

        // Handle initial mouse click into an unfocused TextBox
        EventManager.RegisterClassHandler(
            typeof(WpfTextBox),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnTextBoxPreviewMouseLeftButtonDown),
            handledEventsToo: true);

        // Handle PasswordBox controls
        EventManager.RegisterClassHandler(
            typeof(WpfPasswordBox),
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnPasswordBoxGotKeyboardFocus),
            handledEventsToo: true);

        EventManager.RegisterClassHandler(
            typeof(WpfPasswordBox),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPasswordBoxPreviewMouseLeftButtonDown),
            handledEventsToo: true);
    }

    private static void OnTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is WpfTextBox textBox)
        {
            textBox.Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (textBox.IsKeyboardFocusWithin)
                    {
                        textBox.SelectAll();
                    }
                }));
        }
    }

    private static void OnTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is WpfTextBox textBox && !textBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private static void OnPasswordBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is WpfPasswordBox passwordBox)
        {
            passwordBox.Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (passwordBox.IsKeyboardFocusWithin)
                    {
                        passwordBox.SelectAll();
                    }
                }));
        }
    }

    private static void OnPasswordBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is WpfPasswordBox passwordBox && !passwordBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            passwordBox.Focus();
            passwordBox.SelectAll();
        }
    }
}
