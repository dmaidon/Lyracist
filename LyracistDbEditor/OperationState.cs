// Created on Oct 7, 2026 @ 14:00:00 -> Single-slot guard so the metadata scan, rename (and preview), and readiness audit never run together
using System;

namespace LyracistDbEditor;

// The slow metadata scan, Rename Files (and its preview) and the readiness audit all write to the
// same database and share log/progress display state, so only one may run at a time.
internal sealed class OperationState
{
    private readonly object _gate = new();
    private string? _current;

    /// <summary>Name of the operation currently holding the slot, or null when idle.</summary>
    public string? Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Claims the slot for <paramref name="name"/>; false if another operation already holds it.</summary>
    public bool TryBegin(string name)
    {
        lock (_gate)
        {
            if (_current != null) return false;
            _current = name;
            return true;
        }
    }

    /// <summary>Releases the slot, but only if <paramref name="name"/> is the one holding it.</summary>
    public void End(string name)
    {
        lock (_gate)
        {
            if (string.Equals(_current, name, StringComparison.Ordinal)) _current = null;
        }
    }
}
