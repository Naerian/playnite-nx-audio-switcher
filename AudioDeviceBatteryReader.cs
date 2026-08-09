using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PlayniteAudioSwitcher
{
    internal sealed class WindowsDevicePropertyBatteryProvider : IAudioDeviceBatteryProvider
    {
        private const string BatteryLifeProperty = "System.Devices.BatteryLife";
        private const string BatteryPlusChargingProperty = "System.Devices.BatteryPlusCharging";

        private static readonly string[] BatteryProperties =
        {
            BatteryLifeProperty,
            BatteryPlusChargingProperty
        };

        private static readonly Type DeviceInformationType = Type.GetType(
            "Windows.Devices.Enumeration.DeviceInformation, Windows, ContentType=WindowsRuntime",
            false);
        private static readonly Type DeviceInformationKindType = Type.GetType(
            "Windows.Devices.Enumeration.DeviceInformationKind, Windows, ContentType=WindowsRuntime",
            false);

        public string Name => "Windows.DeviceProperty";

        public IReadOnlyList<string> GetDiagnostics()
        {
            return Array.Empty<string>();
        }

        public async Task<IReadOnlyDictionary<Guid, AudioDeviceBatteryInfo>> ReadAsync(IEnumerable<Guid> containerIds)
        {
            var result = new Dictionary<Guid, AudioDeviceBatteryInfo>();
            foreach (var containerId in (containerIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct())
            {
                var battery = await ReadContainerAsync(containerId).ConfigureAwait(false);
                if (battery != null)
                {
                    result[containerId] = battery;
                }
            }

            return result;
        }

        private static async Task<AudioDeviceBatteryInfo> ReadContainerAsync(Guid containerId)
        {
            try
            {
                var device = await CreateDeviceContainerAsync(containerId).ConfigureAwait(false);

                if (device == null)
                {
                    return null;
                }

                var properties = DeviceInformationType.GetProperty("Properties")?.GetValue(device) as IReadOnlyDictionary<string, object>;
                if (TryGetByte(properties, BatteryPlusChargingProperty, out var combined))
                {
                    if (combined <= 100)
                    {
                        return Create(combined, false);
                    }

                    if (combined <= 200)
                    {
                        return Create(combined - 100, true);
                    }
                }

                return TryGetByte(properties, BatteryLifeProperty, out var percent) && percent <= 100
                    ? Create(percent, false)
                    : null;
            }
            catch
            {
                // Battery reporting is optional and may be unavailable for a device or Windows version.
                return null;
            }
        }

        private static async Task<object> CreateDeviceContainerAsync(Guid containerId)
        {
            if (DeviceInformationType == null || DeviceInformationKindType == null)
            {
                return null;
            }

            var createMethod = DeviceInformationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "CreateFromIdAsync" &&
                    method.GetParameters().Length == 3 &&
                    method.GetParameters()[2].ParameterType == DeviceInformationKindType);
            if (createMethod == null)
            {
                return null;
            }

            var containerKind = Enum.Parse(DeviceInformationKindType, "DeviceContainer");
            var operation = createMethod.Invoke(null, new object[]
            {
                containerId.ToString("B"),
                BatteryProperties,
                containerKind
            });
            if (operation == null)
            {
                return null;
            }

            var extensionsType = typeof(System.WindowsRuntimeSystemExtensions);
            var asTaskMethod = extensionsType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "AsTask" &&
                    method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1 &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType.IsGenericType &&
                    method.GetParameters()[0].ParameterType.GetGenericTypeDefinition().FullName == "Windows.Foundation.IAsyncOperation`1");
            if (asTaskMethod == null)
            {
                return null;
            }

            var task = asTaskMethod.MakeGenericMethod(DeviceInformationType).Invoke(null, new[] { operation }) as Task;
            if (task == null)
            {
                return null;
            }

            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        private static AudioDeviceBatteryInfo Create(int percent, bool isCharging)
        {
            return new AudioDeviceBatteryInfo
            {
                Percent = Math.Max(0, Math.Min(100, percent)),
                IsCharging = isCharging,
                Source = "Windows.DeviceProperty"
            };
        }

        private static bool TryGetByte(IReadOnlyDictionary<string, object> properties, string key, out int value)
        {
            value = 0;
            if (properties == null || !properties.TryGetValue(key, out var raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
