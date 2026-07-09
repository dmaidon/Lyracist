using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Interfaces;
using Lyracist.Data.Models;
using Lyracist.Models;

namespace Lyracist.ViewModels
{
    public partial class SingerSettingsViewModel(ILibraryService libraryService) : BaseViewModel
    {
        private readonly ILibraryService _libraryService = libraryService;
        private string _singerName = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

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

        public void Load(Lyracist.Models.Singer singer)
        {
            _singerName = singer.Name;
            DisplayName = singer.Name;

            var dbSettings = _libraryService.GetSingerSettings(_singerName);
            Treble = dbSettings.Treble;
            Mid = dbSettings.Mid;
            Bass = dbSettings.Bass;
            Gain = dbSettings.Gain;
            Key = dbSettings.Key;
            Tempo = dbSettings.Tempo;
            Compressor = dbSettings.Compressor;
            Limiter = dbSettings.Limiter;
            Notes = dbSettings.Notes;
        }

        [RelayCommand]
        private void Save()
        {
            var settings = new SingerAudioSettings
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
            _libraryService.SaveSingerSettings(_singerName, settings);
        }
    }
}
