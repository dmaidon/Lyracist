// Edited on Aug 27, 2026 @ 15:36:00 -> Updated branding and description to reflect membership in the Lyracist Suite
using System;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KnockoutTrivia.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _appName = "Knockout Trivia";

    [ObservableProperty]
    private string _company = "PAROLE Software";

    [ObservableProperty]
    private string _author = "Dennis N. Maidon";

    [ObservableProperty]
    private string _version = "26.8.27.1";

    [ObservableProperty]
    private string _copyright = $"Copyright © {DateTime.UtcNow.Year} PAROLE Software - All rights reserved.";

    [ObservableProperty]
    private string _description = "A premium bar-friendly elimination game module and integral component of the professional Lyracist Suite. Features high-energy tournament elimination, shield token armor, 5-block streak meters, Scaryoke-style rotary Super Streak target wheels, multi-monitor audience projection, and 16:9 auto-scaling game-show presentation.";

    [ObservableProperty]
    private string _databaseNotice = "Fully compatible with the Lyracist Suite shared SQLite trivia database and category pack ecosystem, maintaining isolated game sessions, logs, and token tracking.";

    public AboutViewModel()
    {
        var asm = Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        if (ver != null)
        {
            Version = ver.ToString();
        }
    }
}
