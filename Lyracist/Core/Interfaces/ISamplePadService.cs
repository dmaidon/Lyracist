// Created on Oct 7, 2026 @ 19:46:00 -> Interface for the DJ sample / jingle pad service
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lyracist.Core.Interfaces;

public class SamplePadItem
{
    public int SlotIndex { get; set; }
    public string Label { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Color { get; set; } = "#2563EB";
    public string HotKey { get; set; } = string.Empty;
    public double Volume { get; set; } = 1.0;
    public int Order { get; set; }

    // Runtime state (not persisted)
    public bool IsMissing => !string.IsNullOrEmpty(FilePath) && !System.IO.File.Exists(FilePath);
    public bool IsAssigned => !string.IsNullOrEmpty(FilePath);
}

public interface ISamplePadService : IDisposable
{
    IReadOnlyList<SamplePadItem> Pads { get; }
    bool IsPlaying { get; }

    event EventHandler? StateChanged;
    event Action<int, bool>? PadPlayingChanged;

    Task InitializeAsync();
    void PlayPad(int slotIndex);
    void StopAll();
    void AssignPad(int slotIndex, string filePath, string? label = null, string? color = null, string? hotkey = null);
    void ClearPad(int slotIndex);
    void UpdatePad(SamplePadItem item);
    void SetOutputDevice(string deviceId);
}
