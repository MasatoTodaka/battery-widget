using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LogiBatteryWidget.Core.Providers.Vaxee;

/// <summary>
/// Finds the raw HID device path for a VAXEE wireless dongle's command channel: vendor 0x3057,
/// usage page 0xFF05, usage 0x01, 64-byte feature reports. Not filtered by product id - VAXEE
/// ships several dongle/mouse PIDs under the same vendor, all exposing this same collection.
/// </summary>
internal static class VaxeeHidLocator
{
    private const ushort VaxeeVendorId = 0x3057;
    private const ushort CommandUsagePage = 0xFF05;
    private const ushort CommandUsage = 0x01;
    private const int CommandReportLength = 64;

    private const int DigcfPresent = 0x02;
    private const int DigcfDeviceInterface = 0x10;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const int HidpStatusSuccess = 0x00110000;

    public static string? FindCommandChannelPath()
    {
        HidD_GetHidGuid(out var hidGuid);

        var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
        {
            return null;
        }

        try
        {
            var interfaceData = new SP_DEVICE_INTERFACE_DATA();
            interfaceData.cbSize = Marshal.SizeOf(interfaceData);

            for (uint index = 0; SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData); index++)
            {
                var path = GetDevicePath(deviceInfoSet, ref interfaceData);
                if (path is null)
                {
                    continue;
                }

                if (IsCommandChannel(path))
                {
                    return path;
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return null;
    }

    private static bool IsCommandChannel(string path)
    {
        using var handle = CreateFile(path, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return false;
        }

        var attributes = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HIDD_ATTRIBUTES>() };
        if (!HidD_GetAttributes(handle, ref attributes) || attributes.VendorID != VaxeeVendorId)
        {
            return false;
        }

        if (!HidD_GetPreparsedData(handle, out var preparsedData))
        {
            return false;
        }

        try
        {
            if (HidP_GetCaps(preparsedData, out var caps) != HidpStatusSuccess)
            {
                return false;
            }

            return caps.UsagePage == CommandUsagePage && caps.Usage == CommandUsage &&
                   caps.FeatureReportByteLength == CommandReportLength;
        }
        finally
        {
            HidD_FreePreparsedData(preparsedData);
        }
    }

    private static string? GetDevicePath(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA interfaceData)
    {
        SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);
        if (requiredSize <= 0)
        {
            return null;
        }

        var detailBuffer = Marshal.AllocHGlobal(requiredSize);
        try
        {
            Marshal.WriteInt32(detailBuffer, Environment.Is64BitProcess ? 8 : 6);

            if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailBuffer, requiredSize, out _, IntPtr.Zero))
            {
                return null;
            }

            return Marshal.PtrToStringAuto(IntPtr.Add(detailBuffer, 4));
        }
        finally
        {
            Marshal.FreeHGlobal(detailBuffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
        int deviceInterfaceDetailDataSize, out int requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);
}
