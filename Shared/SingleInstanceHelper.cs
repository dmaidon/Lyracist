// Created on Oct 6, 2026 @ 10:16:00 -> Provide single-instance enforcement and window restoration for all suite applications
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace Lyracist.Shared;

/// <summary>
/// Enforces single-instance execution for applications across the Lyracist suite.
/// Uses a session-scoped named mutex. If a second instance is launched, it restores
/// and brings the existing window to the foreground before exiting cleanly.
/// </summary>
public static class SingleInstanceHelper
{
    private static readonly List<Mutex> _heldMutexes = new();
    private static bool _processExitHooked;

    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;
    private const uint ASFW_ANY = unchecked((uint)-1);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    /// <summary>
    /// Ensures that only a single instance of the specified application is running per user session.
    /// If an existing instance is running, this method restores and focuses that instance's window,
    /// then calls <see cref="System.Windows.Application.Shutdown()"/> on the current application and returns false.
    /// </summary>
    /// <param name="appName">Unique application key (e.g. "Lyracist", "KSRotation").</param>
    /// <returns>True if this is the first instance and execution should proceed; false if another instance was found and focused.</returns>
    public static bool EnsureSingleInstance(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        string mutexName = $@"Local\Lyracist_SingleInstance_{appName}";
        Mutex mutex;

        try
        {
            mutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                mutex.Dispose();

                // Bring existing instance to the foreground
                ActivateExistingInstance(appName);

                // Terminate this duplicate instance cleanly
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Shutdown(0);
                }
                else
                {
                    Environment.Exit(0);
                }

                return false;
            }
        }
        catch
        {
            // If mutex creation fails unexpectedly (e.g. restrictive OS policy), do not prevent the app from starting
            return true;
        }

        lock (_heldMutexes)
        {
            _heldMutexes.Add(mutex);

            if (!_processExitHooked)
            {
                _processExitHooked = true;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether an instance of the specified application is currently active.
    /// </summary>
    public static bool IsInstanceRunning(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            return false;

        string mutexName = $@"Local\Lyracist_SingleInstance_{appName}";
        try
        {
            using var testMutex = Mutex.OpenExisting(mutexName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Releases all mutexes held by this process.
    /// </summary>
    public static void Cleanup()
    {
        lock (_heldMutexes)
        {
            foreach (var mutex in _heldMutexes)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch
                {
                    // Ignored if already released or abandoned
                }
                finally
                {
                    mutex.Dispose();
                }
            }
            _heldMutexes.Clear();
        }
    }

    /// <summary>
    /// Locates and brings the main window of another instance of this application to the foreground.
    /// </summary>
    public static void ActivateExistingInstance(string appName)
    {
        try
        {
            var current = Process.GetCurrentProcess();
            var candidateProcesses = Process.GetProcessesByName(current.ProcessName);

            // If launched under a runner or test host where process name differs from appName, try appName
            if (candidateProcesses.Length <= 1 && !string.Equals(current.ProcessName, appName, StringComparison.OrdinalIgnoreCase))
            {
                var byName = Process.GetProcessesByName(appName);
                if (byName.Length > 0)
                {
                    candidateProcesses = byName;
                }
            }

            foreach (var proc in candidateProcesses)
            {
                if (proc.Id == current.Id)
                    continue;

                // Grant foreground activation permission to the target process
                try
                {
                    AllowSetForegroundWindow((uint)proc.Id);
                }
                catch
                {
                    AllowSetForegroundWindow(ASFW_ANY);
                }

                IntPtr handle = proc.MainWindowHandle;
                if (handle == IntPtr.Zero)
                {
                    handle = FindTopWindowForProcess(proc.Id);
                }

                if (handle != IntPtr.Zero)
                {
                    ForceForegroundWindow(handle);
                    break;
                }
            }
        }
        catch
        {
            // Best-effort window activation
        }
    }

    /// <summary>
    /// Reliable foreground activation using Win32 ShowWindowAsync and thread input attachment.
    /// </summary>
    public static void ForceForegroundWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return;

        if (IsIconic(hWnd))
        {
            ShowWindowAsync(hWnd, SW_RESTORE);
        }
        else
        {
            ShowWindowAsync(hWnd, SW_SHOW);
        }

        IntPtr foregroundHwnd = GetForegroundWindow();
        uint foregroundThread = GetWindowThreadProcessId(foregroundHwnd, out _);
        uint currentThread = GetCurrentThreadId();

        if (foregroundThread != 0 && foregroundThread != currentThread)
        {
            AttachThreadInput(currentThread, foregroundThread, true);
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            AttachThreadInput(currentThread, foregroundThread, false);
        }
        else
        {
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
    }

    private static IntPtr FindTopWindowForProcess(int processId)
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)processId && IsWindowVisible(hWnd))
            {
                if (GetWindowTextLength(hWnd) > 0)
                {
                    found = hWnd;
                    return false; // Found candidate with window title
                }

                if (found == IntPtr.Zero)
                {
                    found = hWnd;
                }
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}
