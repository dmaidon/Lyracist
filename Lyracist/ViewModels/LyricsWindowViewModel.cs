using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.ViewModels;

public partial class LyricsWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ImageSource? _frame;

    [ObservableProperty]
    private string _fallbackText = "Lyrics Loading...";

    [ObservableProperty]
    private bool _isFallbackVisible = true;

    public void UpdateFrame(ImageSource? newFrame)
    {
        if (newFrame == null)
        {
            IsFallbackVisible = true;
            Frame = null;
            FallbackText = "No Lyrics Available";
        }
        else
        {
            Frame = newFrame;
            IsFallbackVisible = false;
        }
    }
}
