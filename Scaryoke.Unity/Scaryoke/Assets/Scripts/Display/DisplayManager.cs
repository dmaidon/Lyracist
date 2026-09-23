// Created on Sep 23, 2026 @ 11:49:00 -> Multi-monitor and HDMI secondary display manager
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Scaryoke.Unity.Display
{
    public class DisplayManager : MonoBehaviour
    {
        [Header("Target Monitor")]
        public int TargetDisplayIndex = 0;
        public bool FullscreenOnSecondary = true;

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "GetActiveWindow")]
        private static extern IntPtr GetActiveWindow();

        private const uint SwpShowWindow = 0x0040;
        private const uint SwpNoZOrder = 0x0004;

        private void Start()
        {
            InitializeDisplays();
        }

        public void InitializeDisplays()
        {
            int displayCount = UnityEngine.Display.displays.Length;
            Debug.Log($"[DisplayManager] Detected {displayCount} connected physical display(s).");

            for (int i = 0; i < displayCount; i++)
            {
                var d = UnityEngine.Display.displays[i];
                Debug.Log($"[DisplayManager] Display {i}: {d.systemWidth}x{d.systemHeight}");

                // Activate secondary monitors (HDMI / DP / USB-C)
                if (i > 0 && !d.active)
                {
                    d.Activate();
                    Debug.Log($"[DisplayManager] Activated secondary display {i}.");
                }
            }
        }

        public void MoveToDisplay(int displayIndex, int x, int y, int width, int height)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                IntPtr hWnd = GetActiveWindow();
                if (hWnd != IntPtr.Zero)
                {
                    SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, SwpShowWindow | SwpNoZOrder);
                    Debug.Log($"[DisplayManager] Moved window to monitor bounds ({x}, {y}, {width}x{height})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DisplayManager] Win32 SetWindowPos error: {ex.Message}");
            }
#endif
        }
    }
}
