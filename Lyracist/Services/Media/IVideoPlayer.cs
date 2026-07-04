public interface IVideoPlayer
{
    Task LoadAsync(string path);

    Task PlayAsync();

    Task PauseAsync();

    Task StopAsync();
}