using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioDeviceManager
    {
        public IReadOnlyList<AudioDevice> GetPlaybackDevices()
        {
            var devices = new List<AudioDevice>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            var defaultId = GetDefaultPlaybackDeviceId(enumerator);

            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(EDataFlow.eRender, DeviceState.Active, out var collection));
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));

            for (uint i = 0; i < count; i++)
            {
                Marshal.ThrowExceptionForHR(collection.Item(i, out var device));
                Marshal.ThrowExceptionForHR(device.GetId(out var id));
                var name = GetDeviceName(device);

                devices.Add(new AudioDevice
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) ? id : name,
                    IsDefault = string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase)
                });
            }

            return devices;
        }

        public AudioDevice GetDefaultPlaybackDevice()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device));
            Marshal.ThrowExceptionForHR(device.GetId(out var id));

            return new AudioDevice
            {
                Id = id,
                Name = GetDeviceName(device),
                IsDefault = true
            };
        }

        public void SetDefaultPlaybackDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Se necesita el identificador del dispositivo.", nameof(deviceId));
            }

            var policyConfig = (IPolicyConfig)new PolicyConfigClient();
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications));
        }

        private static string GetDefaultPlaybackDeviceId(IMMDeviceEnumerator enumerator)
        {
            try
            {
                Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device));
                Marshal.ThrowExceptionForHR(device.GetId(out var id));
                return id;
            }
            catch
            {
                return null;
            }
        }

        private static string GetDeviceName(IMMDevice device)
        {
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(StorageAccessMode.Read, out var propertyStore));
            using (var prop = new PropVariantHandle())
            {
                Marshal.ThrowExceptionForHR(propertyStore.GetValue(PropertyKeys.DeviceFriendlyName, prop.Pointer));
                return prop.GetString();
            }
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [Flags]
        private enum DeviceState
        {
            Active = 0x00000001
        }

        private enum StorageAccessMode
        {
            Read = 0
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        [ComImport]
        [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
        private class PolicyConfigClient
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

            [PreserveSig]
            int RegisterEndpointNotificationCallback(IntPtr client);

            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport]
        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig]
            int GetCount(out uint count);

            [PreserveSig]
            int Item(uint index, out IMMDevice device);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid interfaceId, uint clsCtx, IntPtr activationParams, out IntPtr interfacePointer);

            [PreserveSig]
            int OpenPropertyStore(StorageAccessMode accessMode, out IPropertyStore properties);

            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

            [PreserveSig]
            int GetState(out DeviceState state);
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig]
            int GetCount(out uint count);

            [PreserveSig]
            int GetAt(uint propertyIndex, out PropertyKey key);

            [PreserveSig]
            int GetValue(ref PropertyKey key, IntPtr value);

            [PreserveSig]
            int SetValue(ref PropertyKey key, IntPtr value);

            [PreserveSig]
            int Commit();
        }

        [ComImport]
        [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig]
            int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr format);

            [PreserveSig]
            int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, bool defaultFormat, IntPtr format);

            [PreserveSig]
            int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName);

            [PreserveSig]
            int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr endpointFormat, IntPtr mixFormat);

            [PreserveSig]
            int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, bool defaultPeriod, out long defaultDevicePeriod, out long minimumDevicePeriod);

            [PreserveSig]
            int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr period);

            [PreserveSig]
            int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr mode);

            [PreserveSig]
            int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr mode);

            [PreserveSig]
            int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, ref PropertyKey key, IntPtr value);

            [PreserveSig]
            int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, ref PropertyKey key, IntPtr value);

            [PreserveSig]
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceName, ERole role);

            [PreserveSig]
            int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceName, bool visible);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid FormatId;
            public int PropertyId;
        }

        private static class PropertyKeys
        {
            public static PropertyKey DeviceFriendlyName = new PropertyKey
            {
                FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
                PropertyId = 14
            };
        }

        private sealed class PropVariantHandle : IDisposable
        {
            public IntPtr Pointer { get; } = Marshal.AllocCoTaskMem(32);

            public PropVariantHandle()
            {
                for (var i = 0; i < 32; i++)
                {
                    Marshal.WriteByte(Pointer, i, 0);
                }
            }

            public string GetString()
            {
                var variantType = (ushort)Marshal.ReadInt16(Pointer);
                if (variantType != 31)
                {
                    return string.Empty;
                }

                var stringPointer = Marshal.ReadIntPtr(Pointer, 8);
                return stringPointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(stringPointer);
            }

            public void Dispose()
            {
                PropVariantClear(Pointer);
                Marshal.FreeCoTaskMem(Pointer);
            }
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(IntPtr propVariant);
    }
}
