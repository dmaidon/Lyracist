// Edited on Oct 31, 2026 @ 13:23:00 -> Prompt to save unsaved pack changes when closing window
using System.ComponentModel;
using System.Windows;
using TriviaDbCreator.ViewModels;

namespace TriviaDbCreator.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && !vm.ConfirmSaveOrDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
