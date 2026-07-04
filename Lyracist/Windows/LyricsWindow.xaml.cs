using System.Windows;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class LyricsWindow : Window
{
    public LyricsWindow(LyricsWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
