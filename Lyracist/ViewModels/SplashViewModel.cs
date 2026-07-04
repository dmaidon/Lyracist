using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.ViewModels;

public partial class SplashViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = "Loading Lyracist...";
}
