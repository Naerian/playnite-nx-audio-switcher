using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PlayniteAudioSwitcher
{
    internal sealed class BluetoothPnpBatteryProvider : IAudioDeviceBatteryProvider
    {
        private const uint DigcfPresent = 0x00000002;
        private const uint DigcfAllClasses = 0x00000004;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        // DEVPKEY_Device_ContainerId. Windows uses this to group the audio endpoint and
        // the Bluetooth service devnodes that belong to the same physical device.
        private static DevPropKey ContainerIdProperty = new DevPropKey(
            new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"),
            2);

        // Battery percentage exposed by Windows Bluetooth/HFP devnodes. This is the
        // same read-only byte shown by the Windows 11 Bluetooth devices settings page.
        private static DevPropKey BluetoothBatteryLifeProperty = new DevPropKey(
            new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
            2);

        private readonly object diagnosticsLock = new object();
        private IReadOnlyList<string> diagnostics = Array.Empty<string>();

        public string Name => "Windows.BluetoothPnP";

        public IReadOnlyList<string> GetDiagnostics()
        {
            lock (diagnosticsLock)
            {
                return diagnostics.ToList();
            }
        }

        public Task<IReadOnlyDictionary<Guid, AudioDeviceBatteryInfo>> ReadAsync(IEnumerable<Guid> containerIds)
        {
            var requestedIds = new HashSet<Guid>(
                (containerIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty));
            return Task.Run<IReadOnlyDictionary<Guid, AudioDeviceBatteryInfo>>(() => Read(requestedIds));
        }

        private IReadOnlyDictionary<Guid, AudioDeviceBatteryInfo> Read(ISet<Guid> requestedIds)
        {
            var result = new Dictionary<Guid, AudioDeviceBatteryInfo>();
            var newDiagnostics = new List<string>();
            if (requestedIds == null || requestedIds.Count == 0)
            {
                SetDiagnostics(newDiagnostics);
                return result;
            }

            var deviceInfoSet = SetupDiGetClassDevs(
                IntPtr.Zero,
                null,
                IntPtr.Zero,
                DigcfPresent | DigcfAllClasses);
            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == InvalidHandleValue)
            {
                newDiagnostics.Add($"Bluetooth PnP enumeration failed error={Marshal.GetLastWin32Error()}");
                SetDiagnostics(newDiagnostics);
                return result;
            }

            try
            {
                for (uint index = 0; ; index++)
                {
                    var deviceInfo = new SpDevInfoData
                    {
                        Size = (uint)Marshal.SizeOf(typeof(SpDevInfoData))
                    };
                    if (!SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfo))
                    {
                        break;
                    }

                    if (!TryGetGuidProperty(deviceInfoSet, ref deviceInfo, ref ContainerIdProperty, out var containerId) ||
                        !requestedIds.Contains(containerId) ||
                        !TryGetByteProperty(deviceInfoSet, ref deviceInfo, ref BluetoothBatteryLifeProperty, out var percent) ||
                        percent > 100)
                    {
                        continue;
                    }

                    var instanceId = GetDeviceInstanceId(deviceInfoSet, ref deviceInfo);
                    newDiagnostics.Add(
                        $"Bluetooth PnP container={containerId:B} battery={percent}% node=\"{instanceId ?? "unknown"}\"");
                    if (!result.ContainsKey(containerId))
                    {
                        result[containerId] = new AudioDeviceBatteryInfo
                        {
                            Percent = percent,
                            IsCharging = false,
                            Source = Name
                        };
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
                SetDiagnostics(newDiagnostics);
            }

            return result;
        }

        private void SetDiagnostics(IReadOnlyList<string> value)
        {
            lock (diagnosticsLock)
            {
                diagnostics = value ?? Array.Empty<string>();
            }
        }

        private static bool TryGetGuidProperty(
            IntPtr deviceInfoSet,
            ref SpDevInfoData deviceInfo,
            ref DevPropKey propertyKey,
            out Guid value)
        {
            value = Guid.Empty;
            var buffer = new byte[16];
            if (!TryGetProperty(deviceInfoSet, ref deviceInfo, ref propertyKey, buffer, out var requiredSize) ||
                requiredSize < 16)
            {
                return false;
            }

            value = new Guid(buffer);
            return value != Guid.Empty;
        }

        private static bool TryGetByteProperty(
            IntPtr deviceInfoSet,
            ref SpDevInfoData deviceInfo,
            ref DevPropKey propertyKey,
            out int value)
        {
            value = 0;
            var buffer = new byte[1];
            if (!TryGetProperty(deviceInfoSet, ref deviceInfo, ref propertyKey, buffer, out var requiredSize) ||
                requiredSize < 1)
            {
                return false;
            }

            value = buffer[0];
            return true;
        }

        private static bool TryGetProperty(
            IntPtr deviceInfoSet,
            ref SpDevInfoData deviceInfo,
            ref DevPropKey propertyKey,
            byte[] buffer,
            out uint requiredSize)
        {
            return SetupDiGetDeviceProperty(
                deviceInfoSet,
                ref deviceInfo,
                ref propertyKey,
                out _,
                buffer,
                (uint)buffer.Length,
                out requiredSize,
                0);
        }

        private static string GetDeviceInstanceId(IntPtr deviceInfoSet, ref SpDevInfoData deviceInfo)
        {
            var buffer = new StringBuilder(512);
            return SetupDiGetDeviceInstanceId(
                deviceInfoSet,
                ref deviceInfo,
                buffer,
                (uint)buffer.Capacity,
                out _)
                ? buffer.ToString()
                : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDevInfoData
        {
            public uint Size;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DevPropKey
        {
            public DevPropKey(Guid formatId, uint propertyId)
            {
                FormatId = formatId;
                PropertyId = propertyId;
            }

            public Guid FormatId;
            public uint PropertyId;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            IntPtr classGuid,
            string enumerator,
            IntPtr parentWindow,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet,
            uint memberIndex,
            ref SpDevInfoData deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceProperty(
            IntPtr deviceInfoSet,
            ref SpDevInfoData deviceInfoData,
            ref DevPropKey propertyKey,
            out uint propertyType,
            [Out] byte[] propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize,
            uint flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr deviceInfoSet,
            ref SpDevInfoData deviceInfoData,
            StringBuilder deviceInstanceId,
            uint deviceInstanceIdSize,
            out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    }
}
