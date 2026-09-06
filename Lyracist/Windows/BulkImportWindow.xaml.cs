// Created on Sep 6, 2026 @ 11:51:00 -> Code-behind for Bulk Import Wizard window
using System.Windows;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class BulkImportWindow : Window
{
    public BulkImportViewModel ViewModel { get; }

    public BulkImportWindow(BulkImportViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.RequestClose += () =>
        {
            DialogResult = true;
            Close();
        };
    }
}
