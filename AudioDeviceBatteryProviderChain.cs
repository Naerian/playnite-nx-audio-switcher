using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlayniteAudioSwitcher
{
    internal sealed class AudioDeviceBatteryReader
    {
        private readonly IReadOnlyList<IAudioDeviceBatteryProvider> providers = new IAudioDeviceBatteryProvider[]
        {
            new WindowsDevicePropertyBatteryProvider(),
            new BluetoothPnpBatteryProvider(),
            new StandardHidBatteryProvider()
        };

        public async Task<IReadOnlyDictionary<Guid, AudioDeviceBatteryInfo>> ReadAsync(IEnumerable<Guid> containerIds)
        {
            var requestedIds = new HashSet<Guid>((containerIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty));
            var result = new Dictionary<Guid, AudioDeviceBatteryInfo>();
            foreach (var provider in providers)
            {
                var remaining = requestedIds.Where(id => !result.ContainsKey(id)).ToList();
                if (remaining.Count == 0)
                {
                    break;
                }

                IReadOnlyDictionary<Guid, AudioDeviceBatteryInfo> readings;
                try
                {
                    readings = await provider.ReadAsync(remaining).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                foreach (var reading in readings ?? new Dictionary<Guid, AudioDeviceBatteryInfo>())
                {
                    if (requestedIds.Contains(reading.Key) && reading.Value != null && !result.ContainsKey(reading.Key))
                    {
                        reading.Value.Source = string.IsNullOrWhiteSpace(reading.Value.Source) ? provider.Name : reading.Value.Source;
                        result[reading.Key] = reading.Value;
                    }
                }
            }

            return result;
        }

        public IReadOnlyList<string> GetDiagnostics()
        {
            return providers.SelectMany(provider => provider.GetDiagnostics() ?? Array.Empty<string>()).ToList();
        }
    }
}
