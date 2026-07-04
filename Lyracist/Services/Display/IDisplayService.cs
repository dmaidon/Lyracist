using System.Collections.Generic;

namespace Lyracist.Services.Display;

public interface IDisplayService
{
    IReadOnlyList<ScreenInfo> GetScreens();

    void ShowRotationWindow();
    void ShowLyricsWindow();

    void MoveRotationToScreen(int screenIndex);
    void MoveLyricsToScreen(int screenIndex);

    void FullscreenRotation();
    void FullscreenLyrics();

    void UpdateRotation(System.Collections.Generic.List<Lyracist.Models.Singer> singers);
    void HighlightSinger(Lyracist.Models.Singer singer);
    void UpdateLyricsFrame(System.Windows.Media.ImageSource frame);
    void SetRotationAnnouncement(string message, bool visible);
}
