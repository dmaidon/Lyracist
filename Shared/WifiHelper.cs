// Edited on Aug 21, 2026 @ 10:05:00 -> Add OperatingSystem.IsWindows check to prevent DllNotFoundException and netsh launch on Android
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Lyracist.Shared;

public static class WifiHelper
{
    public static string? GetConnectedSsid()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            string? ssid = GetConnectedSsidNative();
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                return ssid;
            }
        }
        catch
        {
            // Fallback to netsh if Native API fails
        }

        return GetConnectedSsidNetsh();
    }

    private static string? GetConnectedSsidNetsh()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);

            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    string key = line[..colonIndex].Trim();
                    if (key.Equals("SSID", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line[(colonIndex + 1)..].Trim();
                        if (!string.IsNullOrWhiteSpace(value) && !value.Equals("BSSID", StringComparison.OrdinalIgnoreCase))
                        {
                            return value;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore execution errors
        }

        return null;
    }

    #region Native WLAN API P/Invoke

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanQueryInterface(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        WLAN_INTF_OPCODE OpCode,
        IntPtr pReserved,
        out uint pdwDataSize,
        out IntPtr ppData,
        out WLAN_OPCODE_VALUE_TYPE pWlanOpcodeValueType);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    private enum WLAN_INTF_OPCODE
    {
        wlan_intf_opcode_current_connection = 7
    }

    private enum WLAN_OPCODE_VALUE_TYPE
    {
        wlan_opcode_value_type_query_only = 0,
        wlan_opcode_value_type_set_by_group_policy = 1,
        wlan_opcode_value_type_set_by_user = 2,
        wlan_opcode_value_type_invalid = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_INTERFACE_INFO_LIST
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
        public WLAN_INTERFACE_INFO InterfaceInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public uint isState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_CONNECTION_ATTRIBUTES
    {
        public uint isState;
        public uint wlanConnectionMode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
        public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_ASSOCIATION_ATTRIBUTES
    {
        public DOT11_SSID dot11Ssid;
        public uint dot11BssType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] dot11Bssid;
        public uint dot11PhyType;
        public uint uDot11PhyIndex;
        public uint wlanSignalQuality;
        public uint ulRxRate;
        public uint ulTxRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_SECURITY_ATTRIBUTES
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool bSecurityEnabled;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bOneXEnabled;
        public uint dot11AuthAlgorithm;
        public uint dot11CipherAlgorithm;
    }

    private static string? GetConnectedSsidNative()
    {
        IntPtr hClient = IntPtr.Zero;
        IntPtr pIfList = IntPtr.Zero;
        IntPtr pConnAttr = IntPtr.Zero;

        try
        {
            uint result = WlanOpenHandle(2, IntPtr.Zero, out _, out hClient);
            if (result != 0 || hClient == IntPtr.Zero) return null;

            result = WlanEnumInterfaces(hClient, IntPtr.Zero, out pIfList);
            if (result != 0 || pIfList == IntPtr.Zero) return null;

            uint dwNumberOfItems = (uint)Marshal.ReadInt32(pIfList);
            if (dwNumberOfItems == 0) return null;

            int headerSize = 8; // dwNumberOfItems (4) + dwIndex (4)
            int interfaceInfoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();

            for (int i = 0; i < dwNumberOfItems; i++)
            {
                IntPtr pIfInfo = new IntPtr(pIfList.ToInt64() + headerSize + (i * interfaceInfoSize));
                var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(pIfInfo);

                uint dataSize = 0;
                result = WlanQueryInterface(
                    hClient,
                    ref info.InterfaceGuid,
                    WLAN_INTF_OPCODE.wlan_intf_opcode_current_connection,
                    IntPtr.Zero,
                    out dataSize,
                    out pConnAttr,
                    out _);

                if (result == 0 && pConnAttr != IntPtr.Zero)
                {
                    var connAttr = Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES>(pConnAttr);
                    uint len = connAttr.wlanAssociationAttributes.dot11Ssid.uSSIDLength;
                    byte[] bytes = connAttr.wlanAssociationAttributes.dot11Ssid.ucSSID;

                    if (len > 0 && len <= 32 && bytes != null)
                    {
                        return Encoding.UTF8.GetString(bytes, 0, (int)len);
                    }
                }
            }
        }
        finally
        {
            if (pConnAttr != IntPtr.Zero) WlanFreeMemory(pConnAttr);
            if (pIfList != IntPtr.Zero) WlanFreeMemory(pIfList);
            if (hClient != IntPtr.Zero) WlanCloseHandle(hClient, IntPtr.Zero);
        }

        return null;
    }

    #endregion
}
