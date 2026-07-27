using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Core.Helpers;
using Lyracist.Models;

namespace Lyracist.ViewModels
{
    public partial class EditSingerViewModel : BaseViewModel
    {
        private Singer? _target;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _duetPartnerName = string.Empty;

        [ObservableProperty]
        private string _songTitle = string.Empty;

        [ObservableProperty]
        private string _artist = string.Empty;

        [ObservableProperty]
        private string _key = "0";

        [ObservableProperty]
        private string _notes = string.Empty;

        [ObservableProperty]
        private string _validationError = string.Empty;

        public void Load(Singer singer)
        {
            _target = singer;
            Name = singer.Name;
            DuetPartnerName = singer.DuetPartnerName;
            SongTitle = singer.SongTitle;
            Artist = singer.Artist;
            Key = singer.Key;
            Notes = singer.Notes;
            ValidationError = string.Empty;
        }

        /// <summary>Writes the edited fields back onto the target Singer. Returns false (and sets
        /// ValidationError) without touching the target if Name is blank.</summary>
        public bool Save()
        {
            if (_target == null)
            {
                return false;
            }

            string trimmedName = Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                ValidationError = "Singer name is required.";
                return false;
            }

            _target.Name = NameFormatting.ProperCase(trimmedName);
            _target.DuetPartnerName = NameFormatting.ProperCase(DuetPartnerName?.Trim() ?? string.Empty);
            _target.SongTitle = SongTitle?.Trim() ?? string.Empty;
            _target.Artist = Artist?.Trim() ?? string.Empty;
            _target.Key = string.IsNullOrWhiteSpace(Key) ? "0" : Key.Trim();
            _target.Notes = Notes ?? string.Empty;
            return true;
        }
    }
}
