// Edited on Sep 3, 2026 @ 08:21:00 -> Add VenueInfoResponseDto to AppJsonContext
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
    public partial class AppJsonContext : JsonSerializerContext;
}
