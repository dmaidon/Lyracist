// Created on Sep 3, 2026 @ 08:33:00 -> ContentPage hosting BillboardView for secondary window
using Microsoft.Maui.Controls;
using KSRotation.ViewModels;

namespace KSRotation.Maui.Views
{
    public partial class BillboardPage : ContentPage
    {
        public BillboardPage(MainViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
