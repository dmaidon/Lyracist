using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class LyricsPage : Page
{
    public LyricsPage(LyricsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
