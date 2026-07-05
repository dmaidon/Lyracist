using System.Collections.Generic;
using System.Threading.Tasks;
using Lyracist.Models;

namespace Lyracist.Core.Interfaces
{
    public interface IPartyTymeService
    {
        bool IsAuthenticated { get; }
        string ApiKey { get; set; }
        string ClientId { get; set; }
        string ClientSecret { get; set; }

        Task<bool> AuthenticateAsync(string clientId, string clientSecret);
        Task<IEnumerable<PartyTymeTrack>> SearchCatalogAsync(string query);
        Task<string> GetStreamUrlAsync(string trackId);
        Task<string> DownloadTrackAsync(string trackId, string title, string artist);
        void ClearCache();
    }
}
