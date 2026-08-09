using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlayniteAudioSwitcher
{
    internal interface IAudioDeviceBatteryProvider
    {
        string Name { get; }

        Task<IReadOnlyDictionary<Guid, AudioDeviceBatteryInfo>> ReadAsync(IEnumerable<Guid> containerIds);

        IReadOnlyList<string> GetDiagnostics();
    }
}
