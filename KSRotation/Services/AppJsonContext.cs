// Edited on Sep 19, 2026 @ 17:41:00 -> Add SessionHandoffPayload and DiscoveredPeer to AppJsonContext
using KSRotation.Models;
using Lyracist.Shared;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KSRotation.Services
{
    /// <summary>
    /// Compile-time JSON metadata for every type the app (de)serializes. Using a source-generated context
    /// avoids reflection-based serialization at runtime (faster startup, trim/AOT-friendly).
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(DisplayTarget))]
    [JsonSerializable(typeof(NightDbState))]
    [JsonSerializable(typeof(List<SingerEntry>))]
    [JsonSerializable(typeof(List<SongPerformance>))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<RotationItemDto>))]
    [JsonSerializable(typeof(QueuedSongDto))]
    [JsonSerializable(typeof(List<QueuedSongDto>))]
    [JsonSerializable(typeof(PatronRequest))]
    [JsonSerializable(typeof(List<PatronRequest>))]
    [JsonSerializable(typeof(RequestedSong))]
    [JsonSerializable(typeof(List<RequestedSong>))]
    [JsonSerializable(typeof(VenueInfoResponseDto))]
    [JsonSerializable(typeof(SessionHandoffPayload))]
    [JsonSerializable(typeof(DiscoveredPeer))]
    [JsonSerializable(typeof(List<DiscoveredPeer>))]
    public partial class AppJsonContext : JsonSerializerContext;
}

