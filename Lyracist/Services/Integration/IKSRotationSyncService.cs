using System.Threading.Tasks;

namespace Lyracist.Services.Integration
{
    public interface IKSRotationSyncService
    {
        void Start();
        void Stop();
        Task TriggerSettingsReloadAsync();
    }
}
