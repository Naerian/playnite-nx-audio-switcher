using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace PlayniteAudioSwitcher
{
    internal sealed class DeferredKeyedVolumeWriter
    {
        private readonly Action<string, float> writer;
        private readonly Action<string> synchronizer;
        private readonly DispatcherTimer writeTimer;
        private readonly DispatcherTimer synchronizeTimer;
        private readonly Dictionary<string, float> pendingValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pendingSynchronizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public DeferredKeyedVolumeWriter(Action<string, float> writer, Action<string> synchronizer)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));

            writeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(35)
            };
            writeTimer.Tick += WriteTimer_Tick;

            synchronizeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(140)
            };
            synchronizeTimer.Tick += SynchronizeTimer_Tick;
        }

        public void Queue(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            pendingValues[key] = Math.Max(0f, Math.Min(1f, value));
            pendingSynchronizations.Add(key);
            synchronizeTimer.Stop();

            if (!writeTimer.IsEnabled)
            {
                writeTimer.Start();
            }
        }

        private void WriteTimer_Tick(object sender, EventArgs e)
        {
            if (pendingValues.Count == 0)
            {
                writeTimer.Stop();
                return;
            }

            var values = pendingValues.ToList();
            pendingValues.Clear();
            foreach (var value in values)
            {
                writer(value.Key, value.Value);
            }

            if (pendingValues.Count == 0)
            {
                writeTimer.Stop();
                synchronizeTimer.Stop();
                synchronizeTimer.Start();
            }
        }

        private void SynchronizeTimer_Tick(object sender, EventArgs e)
        {
            synchronizeTimer.Stop();
            var keys = pendingSynchronizations.ToList();
            pendingSynchronizations.Clear();
            foreach (var key in keys)
            {
                synchronizer(key);
            }
        }
    }
}
