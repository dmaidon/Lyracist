// Edited on Sep 10, 2026 @ 12:53:00 -> Add special event sync methods to IKSRotationSyncService
using System.Threading.Tasks;

namespace Lyracist.Services.Integration
{
    public interface IKSRotationSyncService
    {
        void Start();
        void Stop();
        Task TriggerSettingsReloadAsync();
        void NotifyLocalSpecialEventChanged(string eventName);
        Task PushSpecialEventAsync(string eventName, string performerName = "");
    }
}
