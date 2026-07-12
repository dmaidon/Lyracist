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
        private string _partnerName = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _partnerDisplayName = string.Empty;

        [ObservableProperty]
        private bool _isDuet;

        // Singer 1
        [ObservableProperty] private double _treble;
        [ObservableProperty] private double _mid;
        [ObservableProperty] private double _bass;
        [ObservableProperty] private double _gain = 100.0;
        [ObservableProperty] private int _key = 0;
        [ObservableProperty] private double _tempo = 1.0;
        [ObservableProperty] private double _compressor;
        [ObservableProperty] private double _limiter;
        [ObservableProperty] private string _notes = string.Empty;

        // Singer 2 (Partner)
        [ObservableProperty] private double _partnerTreble;
        [ObservableProperty] private double _partnerMid;
        [ObservableProperty] private double _partnerBass;
        [ObservableProperty] private double _partnerGain = 100.0;
        [ObservableProperty] private int _partnerKey = 0;
        [ObservableProperty] private double _partnerTempo = 1.0;
        [ObservableProperty] private double _partnerCompressor;
        [ObservableProperty] private double _partnerLimiter;
        [ObservableProperty] private string _partnerNotes = string.Empty;

        public void Load(Lyracist.Models.Singer singer)
        {
            _singerName = singer.Name;
            DisplayName = singer.Name;
            
            _partnerName = singer.DuetPartnerName;
            PartnerDisplayName = singer.DuetPartnerName;
            IsDuet = singer.IsDuet;

            // Load Singer 1
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

            // Load Partner
            if (IsDuet && !string.IsNullOrEmpty(_partnerName))
            {
                var dbPartnerSettings = _libraryService.GetSingerSettings(_partnerName);
                PartnerTreble = dbPartnerSettings.Treble;
                PartnerMid = dbPartnerSettings.Mid;
                PartnerBass = dbPartnerSettings.Bass;
                PartnerGain = dbPartnerSettings.Gain;
                PartnerKey = dbPartnerSettings.Key;
                PartnerTempo = dbPartnerSettings.Tempo;
                PartnerCompressor = dbPartnerSettings.Compressor;
                PartnerLimiter = dbPartnerSettings.Limiter;
                PartnerNotes = dbPartnerSettings.Notes;
            }
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

            if (IsDuet && !string.IsNullOrEmpty(_partnerName))
            {
                var partnerSettings = new SingerAudioSettings
                {
                    Treble = PartnerTreble,
                    Mid = PartnerMid,
                    Bass = PartnerBass,
                    Gain = PartnerGain,
                    Key = PartnerKey,
                    Tempo = PartnerTempo,
                    Compressor = PartnerCompressor,
                    Limiter = PartnerLimiter,
                    Notes = PartnerNotes
                };
                _libraryService.SaveSingerSettings(_partnerName, partnerSettings);
            }
        }
    }
}
