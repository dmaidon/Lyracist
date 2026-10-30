// Edited on Oct 30, 2026 @ 06:22:00 -> Add ScaryokeCategory selection and ClearScaryokeCategoryCommand to EditSingerViewModel
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Models;
using Lyracist.Shared;

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
        private string _scaryokeCategory = string.Empty;

        [ObservableProperty]
        private IReadOnlyList<string> _availableScaryokeCategories = [];

        [ObservableProperty]
        private bool _hasSungInLastRound;

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
            ScaryokeCategory = singer.ScaryokeCategory ?? string.Empty;
            HasSungInLastRound = singer.HasSungInLastRound;
            ValidationError = string.Empty;

            var cats = new List<string> { "(None)" };
            foreach (var c in AppSettings.ScaryokeCategories)
            {
                if (!cats.Contains(c, StringComparer.OrdinalIgnoreCase))
                {
                    cats.Add(c);
                }
            }
            if (!cats.Contains("Singer's Choice", StringComparer.OrdinalIgnoreCase))
            {
                cats.Add("Singer's Choice");
            }
            if (!cats.Contains("DJ's Choice", StringComparer.OrdinalIgnoreCase))
            {
                cats.Add("DJ's Choice");
            }
            AvailableScaryokeCategories = cats;
        }

        [RelayCommand]
        private void ClearScaryokeCategory()
        {
            ScaryokeCategory = string.Empty;
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
            _target.SongTitle = NameFormatting.ProperCase(SongTitle?.Trim() ?? string.Empty);
            _target.Artist = NameFormatting.ProperCase(Artist?.Trim() ?? string.Empty);
            _target.Key = string.IsNullOrWhiteSpace(Key) ? "0" : Key.Trim();
            _target.Notes = Notes ?? string.Empty;

            string trimmedScaryoke = ScaryokeCategory?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedScaryoke)
                || string.Equals(trimmedScaryoke, "(None)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmedScaryoke, "None", StringComparison.OrdinalIgnoreCase))
            {
                _target.ScaryokeCategory = null;
                _target.ScaryokeColor = null;
            }
            else
            {
                _target.ScaryokeCategory = trimmedScaryoke;
                _target.ScaryokeColor = ResolveScaryokeColor(trimmedScaryoke);
            }

            _target.HasSungInLastRound = HasSungInLastRound;
            return true;
        }

        public static string ResolveScaryokeColor(string category)
        {
            if (string.Equals(category, "Singer's Choice", StringComparison.OrdinalIgnoreCase))
                return "#228B22";
            if (string.Equals(category, "DJ's Choice", StringComparison.OrdinalIgnoreCase))
                return "#000000";

            var customList = AppSettings.ScaryokeCategories;
            for (int i = 0; i < customList.Count; i++)
            {
                if (string.Equals(customList[i], category, StringComparison.OrdinalIgnoreCase))
                {
                    var style = AppSettings.GetScaryokeStyle(customList[i]);
                    return style.Color ?? ScaryokeViewModel.PaletteColor(i);
                }
            }
            return "#FF6D00";
        }
    }
}

