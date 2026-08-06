// Edited on Aug 6, 2026 @ 07:01:27 -> Add CanSave guard so Save is disabled until Load() has populated a valid audio path
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Interfaces;
using Lyracist.Data.Models;
using Lyracist.Models;

namespace Lyracist.ViewModels
{
    public partial class SongSettingsViewModel(ILibraryService libraryService) : BaseViewModel
    {
        private readonly ILibraryService _libraryService = libraryService;
        private string _audioPath = string.Empty;

        [ObservableProperty]
        private string _songTitle = string.Empty;

        [ObservableProperty]
        private string _songArtist = string.Empty;

        [ObservableProperty]
        private double _treble;

        [ObservableProperty]
        private double _mid;

        [ObservableProperty]
        private double _bass;

        [ObservableProperty]
        private double _gain = 100.0;

        [ObservableProperty]
        private int _key = 0;

        [ObservableProperty]
        private double _tempo = 1.0;

        [ObservableProperty]
        private double _compressor;

        [ObservableProperty]
        private double _limiter;

        [ObservableProperty]
        private string _notes = string.Empty;

        public void Load(KaraokeSong song)
        {
            _audioPath = song.AudioPath;
            SongTitle = song.Title;
            SongArtist = song.Artist;

            var dbSettings = _libraryService.GetAudioSettings(_audioPath);
            Treble = dbSettings.Treble;
            Mid = dbSettings.Mid;
            Bass = dbSettings.Bass;
            Gain = dbSettings.Gain;
            Key = dbSettings.Key;
            Tempo = dbSettings.Tempo;
            Compressor = dbSettings.Compressor;
            Limiter = dbSettings.Limiter;
            Notes = dbSettings.Notes;

            SaveCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            var settings = new SongAudioSettings
            {
                Treble = Treble,
                Mid = Mid,
                Bass = Bass,
                Gain = Gain,
                Key = Key,
                Tempo = Tempo,
                Compressor = Compressor,
                Limiter = Limiter,
                Notes = Notes
            };
            _libraryService.SaveAudioSettings(_audioPath, settings);
        }

        private bool CanSave() => !string.IsNullOrEmpty(_audioPath);
    }
}
