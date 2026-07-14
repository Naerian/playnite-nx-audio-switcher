using System;
using System.Windows.Threading;

namespace PlayniteAudioSwitcher
{
    internal sealed class DeferredVolumeWriter
    {
        private readonly Action<float> writer;
        private readonly Action synchronizer;
        private readonly DispatcherTimer writeTimer;
        private readonly DispatcherTimer synchronizeTimer;
        private float pendingValue;
        private bool hasPendingValue;

        public DeferredVolumeWriter(Action<float> writer, Action synchronizer)
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

        public void Queue(float value)
        {
            pendingValue = Math.Max(0f, Math.Min(1f, value));
            hasPendingValue = true;
            synchronizeTimer.Stop();

            if (!writeTimer.IsEnabled)
            {
                writeTimer.Start();
            }
        }

        private void WriteTimer_Tick(object sender, EventArgs e)
        {
            if (!hasPendingValue)
            {
                writeTimer.Stop();
                return;
            }

            var value = pendingValue;
            hasPendingValue = false;
            writer(value);

            if (!hasPendingValue)
            {
                writeTimer.Stop();
                synchronizeTimer.Stop();
                synchronizeTimer.Start();
            }
        }

        private void SynchronizeTimer_Tick(object sender, EventArgs e)
        {
            synchronizeTimer.Stop();
            synchronizer();
        }
    }
}
