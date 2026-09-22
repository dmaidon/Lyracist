// Created on Sep 22, 2026 @ 10:30:00 -> Add ProjectionRotationEntry for the automatic screen-rotation schedule
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lyracist.Shared;

/// <summary>
/// One row of the DJ-configured screen-rotation schedule: whether a given projection view
/// participates in the automatic overnight rotation, and how many seconds it stays up before the
/// display advances to the next enabled view. Implements INotifyPropertyChanged by hand (rather than
/// via CommunityToolkit.Mvvm's ObservableObject) so this plain data type stays dependency-free and
/// safe to link into every consuming project, including KSRotation.Maui.
/// </summary>
public class ProjectionRotationEntry : INotifyPropertyChanged
{
    private bool _isEnabled;
    private int _durationSeconds = 30;

    public string ViewName { get; set; } = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>How long (in seconds) this view stays on screen before the rotation advances. Clamped
    /// to a sane minimum on set so a DJ mis-typing "0" can't produce a rotation that never settles.</summary>
    public int DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            int clamped = value < 5 ? 5 : value;
            if (_durationSeconds == clamped) return;
            _durationSeconds = clamped;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
