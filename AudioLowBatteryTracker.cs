using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlayniteAudioSwitcher
{
    /// <summary>
    /// One on-screen notice per low-battery episode. A missing reading is not low.
    /// The latch clears after two consecutive samples above the threshold.
    /// </summary>
    internal sealed class AudioLowBatteryTracker
    {
        public const string ThresholdLow = "Low";
        public const string ThresholdEmpty = "Empty";
        public const int LowPercent = 20;
        public const int EmptyPercent = 5;
        private const int RecoverSamplesRequired = 2;

        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static string NormalizeThreshold(string value)
        {
            return string.Equals(value, ThresholdEmpty, StringComparison.OrdinalIgnoreCase)
                ? ThresholdEmpty
                : ThresholdLow;
        }

        public static int LimitFor(string threshold)
        {
            return string.Equals(NormalizeThreshold(threshold), ThresholdEmpty, StringComparison.OrdinalIgnoreCase)
                ? EmptyPercent
                : LowPercent;
        }

        public static bool IsAtOrBelowThreshold(int? percent, bool isCharging, string threshold)
        {
            if (!percent.HasValue || isCharging)
            {
                return false;
            }

            return percent.Value <= LimitFor(threshold);
        }

        public void SeedWithoutNotify(IEnumerable<string> deviceIds)
        {
            if (deviceIds == null)
            {
                return;
            }

            foreach (var deviceId in deviceIds)
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    continue;
                }

                entries[deviceId] = new Entry { Latched = true };
            }
        }

        public void Clear()
        {
            entries.Clear();
        }

        public void RetainOnly(IEnumerable<string> connectedDeviceIds)
        {
            var keep = new HashSet<string>(
                connectedDeviceIds ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            foreach (var key in entries.Keys.ToList())
            {
                if (!keep.Contains(key))
                {
                    entries.Remove(key);
                }
            }
        }

        public bool ShouldShow(string deviceId, int? percent, bool isCharging, string threshold, bool connected)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return false;
            }

            if (!connected)
            {
                entries.Remove(deviceId);
                return false;
            }

            Entry entry;
            if (!entries.TryGetValue(deviceId, out entry))
            {
                entry = new Entry();
                entries[deviceId] = entry;
            }

            if (IsAtOrBelowThreshold(percent, isCharging, threshold))
            {
                entry.RecoverSamples = 0;
                if (entry.Latched)
                {
                    return false;
                }

                entry.Latched = true;
                return true;
            }

            if (!entry.Latched)
            {
                return false;
            }

            entry.RecoverSamples++;
            if (entry.RecoverSamples >= RecoverSamplesRequired)
            {
                entry.Latched = false;
                entry.RecoverSamples = 0;
            }

            return false;
        }

        public static string FormatPercent(int percent)
        {
            return percent.ToString(CultureInfo.InvariantCulture) + "%";
        }

        private sealed class Entry
        {
            public bool Latched { get; set; }
            public int RecoverSamples { get; set; }
        }
    }
}
