using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace PlayniteAudioSwitcher
{
    public sealed class AudioDeviceManager
    {
        private const int HResultElementNotFound = unchecked((int)0x80070490);

        public IReadOnlyList<AudioDevice> GetPlaybackDevices()
        {
            return GetDevices(EDataFlow.eRender);
        }

        public IReadOnlyList<AudioDevice> GetRecordingDevices()
        {
            return GetDevices(EDataFlow.eCapture);
        }

        public AudioDevice GetDefaultPlaybackDevice()
        {
            return GetDefaultDevice(EDataFlow.eRender);
        }

        public AudioDevice GetDefaultRecordingDevice()
        {
            return GetDefaultDevice(EDataFlow.eCapture);
        }

        public void SetDefaultPlaybackDevice(string deviceId)
        {
            SetDefaultDevice(deviceId);
        }

        public void SetDefaultRecordingDevice(string deviceId)
        {
            SetDefaultDevice(deviceId);
        }

        public AudioVolumeState GetDefaultPlaybackVolume()
        {
            return GetDefaultVolume(EDataFlow.eRender);
        }

        public AudioVolumeState GetDefaultRecordingVolume()
        {
            return GetDefaultVolume(EDataFlow.eCapture);
        }

        public void SetDefaultPlaybackVolume(float volume)
        {
            SetDefaultVolume(EDataFlow.eRender, volume);
        }

        public void SetDefaultRecordingVolume(float volume)
        {
            SetDefaultVolume(EDataFlow.eCapture, volume);
        }

        public void ChangeDefaultPlaybackVolume(float delta)
        {
            var state = GetDefaultPlaybackVolume();
            SetDefaultPlaybackVolume(state.Volume + delta);
        }

        public void ChangeDefaultRecordingVolume(float delta)
        {
            var state = GetDefaultRecordingVolume();
            SetDefaultRecordingVolume(state.Volume + delta);
        }

        public AudioVolumeState GetProcessTreeVolume(int rootProcessId)
        {
            var session = GetProcessTreeVolumeSessions(rootProcessId).FirstOrDefault();
            if (session == null)
            {
                return new AudioVolumeState { IsAvailable = false };
            }

            Marshal.ThrowExceptionForHR(session.GetMasterVolume(out var level));
            Marshal.ThrowExceptionForHR(session.GetMute(out var isMuted));

            return new AudioVolumeState
            {
                IsAvailable = true,
                Volume = Clamp01(level),
                IsMuted = isMuted
            };
        }

        public bool SetProcessTreeVolume(int rootProcessId, float volume)
        {
            var sessions = GetProcessTreeVolumeSessions(rootProcessId);
            return SetVolumeForSessions(sessions, volume);
        }

        public bool ChangeProcessTreeVolume(int rootProcessId, float delta)
        {
            var state = GetProcessTreeVolume(rootProcessId);
            if (!state.IsAvailable)
            {
                return false;
            }

            return SetProcessTreeVolume(rootProcessId, state.Volume + delta);
        }

        public bool SetProcessTreeMute(int rootProcessId, bool isMuted)
        {
            var sessions = GetProcessTreeVolumeSessions(rootProcessId);
            return SetMuteForSessions(sessions, isMuted);
        }

        public bool ToggleProcessTreeMute(int rootProcessId)
        {
            var state = GetProcessTreeVolume(rootProcessId);
            if (!state.IsAvailable)
            {
                return false;
            }

            return SetProcessTreeMute(rootProcessId, !state.IsMuted);
        }

        public IReadOnlyCollection<uint> GetPlaybackAudioSessionProcessIds()
        {
            return GetPlaybackAudioSessionProcessIds(null);
        }

        public IReadOnlyCollection<uint> GetProcessTreeAudioSessionProcessIds(int rootProcessId)
        {
            return GetPlaybackAudioSessionProcessIds(GetProcessTreeIds(rootProcessId));
        }

        public bool SetProcessVolumes(IEnumerable<uint> processIds, float volume)
        {
            return SetVolumeForSessions(GetVolumeSessionsForProcessIds(new HashSet<uint>(processIds ?? Enumerable.Empty<uint>())), volume);
        }

        public bool SetProcessMutes(IEnumerable<uint> processIds, bool isMuted)
        {
            return SetMuteForSessions(GetVolumeSessionsForProcessIds(new HashSet<uint>(processIds ?? Enumerable.Empty<uint>())), isMuted);
        }

        public AudioVolumeState GetProcessVolume(IEnumerable<uint> processIds)
        {
            var session = GetVolumeSessionsForProcessIds(new HashSet<uint>(processIds ?? Enumerable.Empty<uint>())).FirstOrDefault();
            if (session == null)
            {
                return new AudioVolumeState { IsAvailable = false };
            }

            Marshal.ThrowExceptionForHR(session.GetMasterVolume(out var level));
            Marshal.ThrowExceptionForHR(session.GetMute(out var isMuted));

            return new AudioVolumeState
            {
                IsAvailable = true,
                Volume = Clamp01(level),
                IsMuted = isMuted
            };
        }

        public void SetDefaultPlaybackMute(bool isMuted)
        {
            SetDefaultMute(EDataFlow.eRender, isMuted);
        }

        public void SetDefaultRecordingMute(bool isMuted)
        {
            SetDefaultMute(EDataFlow.eCapture, isMuted);
        }

        public void ToggleDefaultPlaybackMute()
        {
            var state = GetDefaultPlaybackVolume();
            SetDefaultPlaybackMute(!state.IsMuted);
        }

        public void ToggleDefaultRecordingMute()
        {
            var state = GetDefaultRecordingVolume();
            SetDefaultRecordingMute(!state.IsMuted);
        }

        private IReadOnlyList<AudioDevice> GetDevices(EDataFlow dataFlow)
        {
            var devices = new List<AudioDevice>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            var defaultId = GetDefaultDeviceId(enumerator, dataFlow);

            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active, out var collection));
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

        private IReadOnlyList<ISimpleAudioVolume> GetProcessTreeVolumeSessions(int rootProcessId)
        {
            var processIds = GetProcessTreeIds(rootProcessId);
            return GetVolumeSessionsForProcessIds(processIds);
        }

        private IReadOnlyCollection<uint> GetPlaybackAudioSessionProcessIds(HashSet<uint> filterProcessIds)
        {
            var processIds = new HashSet<uint>();
            foreach (var session in EnumeratePlaybackSessions(filterProcessIds))
            {
                processIds.Add(session.ProcessId);
            }

            return processIds;
        }

        private IReadOnlyList<ISimpleAudioVolume> GetVolumeSessionsForProcessIds(HashSet<uint> processIds)
        {
            var sessions = new List<ISimpleAudioVolume>();
            if (processIds == null || processIds.Count == 0)
            {
                return sessions;
            }

            foreach (var session in EnumeratePlaybackSessions(processIds))
            {
                if (session.Volume != null)
                {
                    sessions.Add(session.Volume);
                }
            }

            return sessions;
        }

        private IReadOnlyList<AudioSessionInfo> EnumeratePlaybackSessions(HashSet<uint> filterProcessIds)
        {
            var sessions = new List<AudioSessionInfo>();
            foreach (var device in GetEndpointDevices(EDataFlow.eRender))
            {
                try
                {
                    var interfaceId = typeof(IAudioSessionManager2).GUID;
                    Marshal.ThrowExceptionForHR(device.Activate(ref interfaceId, 23, IntPtr.Zero, out var interfacePointer));
                    try
                    {
                        var manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(interfacePointer);
                        Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out var sessionEnumerator));
                        Marshal.ThrowExceptionForHR(sessionEnumerator.GetCount(out var count));

                        for (var i = 0; i < count; i++)
                        {
                            Marshal.ThrowExceptionForHR(sessionEnumerator.GetSession(i, out var control));
                            var control2 = QueryAudioSessionControl2(control);
                            if (control2 == null)
                            {
                                continue;
                            }

                            Marshal.ThrowExceptionForHR(control2.GetProcessId(out var sessionProcessId));
                            if (sessionProcessId == 0 ||
                                filterProcessIds != null && !filterProcessIds.Contains(sessionProcessId))
                            {
                                continue;
                            }

                            var volume = QuerySimpleAudioVolume(control);
                            if (volume != null)
                            {
                                sessions.Add(new AudioSessionInfo
                                {
                                    ProcessId = sessionProcessId,
                                    Volume = volume
                                });
                            }
                        }
                    }
                    finally
                    {
                        Marshal.Release(interfacePointer);
                    }
                }
                catch
                {
                    // Sessions are short-lived; ignore endpoints/sessions that disappear while enumerating.
                }
            }

            return sessions;
        }

        private static bool SetVolumeForSessions(IReadOnlyList<ISimpleAudioVolume> sessions, float volume)
        {
            if (sessions == null || sessions.Count == 0)
            {
                return false;
            }

            var eventContext = Guid.Empty;
            foreach (var session in sessions)
            {
                Marshal.ThrowExceptionForHR(session.SetMasterVolume(Clamp01(volume), ref eventContext));
            }

            return true;
        }

        private static bool SetMuteForSessions(IReadOnlyList<ISimpleAudioVolume> sessions, bool isMuted)
        {
            if (sessions == null || sessions.Count == 0)
            {
                return false;
            }

            var eventContext = Guid.Empty;
            foreach (var session in sessions)
            {
                Marshal.ThrowExceptionForHR(session.SetMute(isMuted, ref eventContext));
            }

            return true;
        }

        private IReadOnlyList<IMMDevice> GetEndpointDevices(EDataFlow dataFlow)
        {
            var devices = new List<IMMDevice>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active, out var collection));
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));

            for (uint i = 0; i < count; i++)
            {
                Marshal.ThrowExceptionForHR(collection.Item(i, out var device));
                devices.Add(device);
            }

            return devices;
        }

        private static ISimpleAudioVolume QuerySimpleAudioVolume(IAudioSessionControl control)
        {
            var unknown = Marshal.GetIUnknownForObject(control);
            try
            {
                var interfaceId = typeof(ISimpleAudioVolume).GUID;
                var result = Marshal.QueryInterface(unknown, ref interfaceId, out var interfacePointer);
                if (result != 0 || interfacePointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return (ISimpleAudioVolume)Marshal.GetObjectForIUnknown(interfacePointer);
                }
                finally
                {
                    Marshal.Release(interfacePointer);
                }
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        private static IAudioSessionControl2 QueryAudioSessionControl2(IAudioSessionControl control)
        {
            var unknown = Marshal.GetIUnknownForObject(control);
            try
            {
                var interfaceId = typeof(IAudioSessionControl2).GUID;
                var result = Marshal.QueryInterface(unknown, ref interfaceId, out var interfacePointer);
                if (result != 0 || interfacePointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return (IAudioSessionControl2)Marshal.GetObjectForIUnknown(interfacePointer);
                }
                finally
                {
                    Marshal.Release(interfacePointer);
                }
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        private static HashSet<uint> GetProcessTreeIds(int rootProcessId)
        {
            var result = new HashSet<uint>();
            if (rootProcessId <= 0)
            {
                return result;
            }

            var root = (uint)rootProcessId;
            result.Add(root);

            var snapshot = CreateToolhelp32Snapshot(0x00000002, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            {
                return result;
            }

            try
            {
                var processes = new List<ProcessEntry32>();
                var entry = new ProcessEntry32
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(ProcessEntry32))
                };

                if (!Process32First(snapshot, ref entry))
                {
                    return result;
                }

                do
                {
                    processes.Add(entry);
                }
                while (Process32Next(snapshot, ref entry));

                var added = true;
                while (added)
                {
                    added = false;
                    foreach (var process in processes)
                    {
                        if (result.Contains(process.th32ProcessID) ||
                            !result.Contains(process.th32ParentProcessID))
                        {
                            continue;
                        }

                        result.Add(process.th32ProcessID);
                        added = true;
                    }
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            return result;
        }

        private sealed class AudioSessionInfo
        {
            public uint ProcessId { get; set; }

            public ISimpleAudioVolume Volume { get; set; }
        }

        private AudioDevice GetDefaultDevice(EDataFlow dataFlow)
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            var result = enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.eMultimedia, out var device);
            if (IsEndpointNotFound(result))
            {
                return null;
            }

            Marshal.ThrowExceptionForHR(result);
            Marshal.ThrowExceptionForHR(device.GetId(out var id));

            return new AudioDevice
            {
                Id = id,
                Name = GetDeviceName(device),
                IsDefault = true
            };
        }

        private void SetDefaultDevice(string deviceId)
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

        private AudioVolumeState GetDefaultVolume(EDataFlow dataFlow)
        {
            var volume = GetDefaultVolumeEndpoint(dataFlow);
            if (volume == null)
            {
                return new AudioVolumeState();
            }

            Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out var level));
            Marshal.ThrowExceptionForHR(volume.GetMute(out var isMuted));

            return new AudioVolumeState
            {
                Volume = Clamp01(level),
                IsMuted = isMuted
            };
        }

        private void SetDefaultVolume(EDataFlow dataFlow, float volume)
        {
            var endpoint = GetDefaultVolumeEndpoint(dataFlow);
            if (endpoint == null)
            {
                return;
            }

            var eventContext = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(Clamp01(volume), ref eventContext));
        }

        private void SetDefaultMute(EDataFlow dataFlow, bool isMuted)
        {
            var endpoint = GetDefaultVolumeEndpoint(dataFlow);
            if (endpoint == null)
            {
                return;
            }

            var eventContext = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.SetMute(isMuted, ref eventContext));
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private IAudioEndpointVolume GetDefaultVolumeEndpoint(EDataFlow dataFlow)
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            var result = enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.eMultimedia, out var device);
            if (IsEndpointNotFound(result))
            {
                return null;
            }

            Marshal.ThrowExceptionForHR(result);
            var interfaceId = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref interfaceId, 23, IntPtr.Zero, out var interfacePointer));
            try
            {
                return (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(interfacePointer);
            }
            finally
            {
                Marshal.Release(interfacePointer);
            }
        }

        public static bool IsEndpointNotFoundException(Exception exception)
        {
            return exception is COMException comException && IsEndpointNotFound(comException.ErrorCode);
        }

        private static bool IsEndpointNotFound(int hresult)
        {
            return hresult == HResultElementNotFound;
        }

        private static string GetDefaultDeviceId(IMMDeviceEnumerator enumerator, EDataFlow dataFlow)
        {
            try
            {
                var result = enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.eMultimedia, out var device);
                if (IsEndpointNotFound(result))
                {
                    return null;
                }

                Marshal.ThrowExceptionForHR(result);
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
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig]
            int RegisterControlChangeNotify(IntPtr client);

            [PreserveSig]
            int UnregisterControlChangeNotify(IntPtr client);

            [PreserveSig]
            int GetChannelCount(out uint channelCount);

            [PreserveSig]
            int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

            [PreserveSig]
            int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

            [PreserveSig]
            int GetMasterVolumeLevel(out float levelDb);

            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float level);

            [PreserveSig]
            int SetChannelVolumeLevel(uint channelNumber, float levelDb, ref Guid eventContext);

            [PreserveSig]
            int SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid eventContext);

            [PreserveSig]
            int GetChannelVolumeLevel(uint channelNumber, out float levelDb);

            [PreserveSig]
            int GetChannelVolumeLevelScalar(uint channelNumber, out float level);

            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);

            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);

            [PreserveSig]
            int GetVolumeStepInfo(out uint step, out uint stepCount);

            [PreserveSig]
            int VolumeStepUp(ref Guid eventContext);

            [PreserveSig]
            int VolumeStepDown(ref Guid eventContext);

            [PreserveSig]
            int QueryHardwareSupport(out uint hardwareSupportMask);

            [PreserveSig]
            int GetVolumeRange(out float volumeMinDb, out float volumeMaxDb, out float volumeIncrementDb);
        }

        [ComImport]
        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            [PreserveSig]
            int GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, out IAudioSessionControl sessionControl);

            [PreserveSig]
            int GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, out ISimpleAudioVolume audioVolume);

            [PreserveSig]
            int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);

            [PreserveSig]
            int RegisterSessionNotification(IntPtr sessionNotification);

            [PreserveSig]
            int UnregisterSessionNotification(IntPtr sessionNotification);

            [PreserveSig]
            int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr duckNotification);

            [PreserveSig]
            int UnregisterDuckNotification(IntPtr duckNotification);
        }

        [ComImport]
        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig]
            int GetCount(out int sessionCount);

            [PreserveSig]
            int GetSession(int sessionIndex, out IAudioSessionControl sessionControl);
        }

        [ComImport]
        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl
        {
            [PreserveSig]
            int GetState(out int state);

            [PreserveSig]
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);

            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);

            [PreserveSig]
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);

            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);

            [PreserveSig]
            int GetGroupingParam(out Guid groupingId);

            [PreserveSig]
            int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);

            [PreserveSig]
            int RegisterAudioSessionNotification(IntPtr newNotifications);

            [PreserveSig]
            int UnregisterAudioSessionNotification(IntPtr newNotifications);
        }

        [ComImport]
        [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            [PreserveSig]
            int GetState(out int state);

            [PreserveSig]
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);

            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);

            [PreserveSig]
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);

            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);

            [PreserveSig]
            int GetGroupingParam(out Guid groupingId);

            [PreserveSig]
            int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);

            [PreserveSig]
            int RegisterAudioSessionNotification(IntPtr newNotifications);

            [PreserveSig]
            int UnregisterAudioSessionNotification(IntPtr newNotifications);

            [PreserveSig]
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);

            [PreserveSig]
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);

            [PreserveSig]
            int GetProcessId(out uint processId);

            [PreserveSig]
            int IsSystemSoundsSession();

            [PreserveSig]
            int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
        }

        [ComImport]
        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            [PreserveSig]
            int SetMasterVolume(float level, ref Guid eventContext);

            [PreserveSig]
            int GetMasterVolume(out float level);

            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);

            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct ProcessEntry32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }
    }
}
