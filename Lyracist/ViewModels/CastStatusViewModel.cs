// Created on Aug 1, 2026 @ 14:40:00 -> Add CastStatusViewModel class
using System;
using Lyracist.Shared;

namespace Lyracist.ViewModels
{
    public class CastStatusViewModel : BaseViewModel
    {
        private readonly ICastingService _casting;

        public CastStatusViewModel(ICastingService casting)
        {
            _casting = casting;
        }

        public bool IsCasting => _casting.IsCasting;
        public DisplayTarget CurrentTarget => _casting.CurrentTarget;

        public string StatusText =>
            !_casting.IsCasting ? "Not Casting" :
            $"Casting to {_casting.CurrentTarget}";

        public void Refresh()
        {
            OnPropertyChanged(nameof(IsCasting));
            OnPropertyChanged(nameof(CurrentTarget));
            OnPropertyChanged(nameof(StatusText));
        }
    }
}
