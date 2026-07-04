public interface IAudioPlayer
{
    TimeSpan Position { get; }

    Task LoadAsync(string path);

    Task PlayAsync();

    Task PauseAsync();

    Task StopAsync();
}