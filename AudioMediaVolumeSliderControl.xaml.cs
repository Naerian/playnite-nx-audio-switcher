using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioMediaVolumeSliderControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;
        private bool isRefreshing;

        public AudioMediaVolumeSliderControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioMediaVolumeSliderControl_Loaded;
        }

        private void AudioMediaVolumeSliderControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            try
            {
                var state = plugin.GetCurrentMediaSessionVolumeState();
                SetSliderValue(state.VolumePercent, state.IsMuted, state.IsAvailable);
            }
            catch
            {
                SetSliderValue(0, false, false);
            }
        }

        private void SetSliderValue(int value, bool muted, bool isAvailable)
        {
            isRefreshing = true;
            try
            {
                VolumeSlider.IsEnabled = isAvailable;
                VolumeSlider.Value = Math.Max(0, Math.Min(100, value));
                VolumeLabel.Text = isAvailable
                    ? muted ? plugin.Loc("LOCAS_MediaSessionMuted") : $"{(int)Math.Round(VolumeSlider.Value)}%"
                    : plugin.Loc("LOCAS_MediaSessionUnavailable");
            }
            finally
            {
                isRefreshing = false;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isRefreshing || !IsLoaded || !VolumeSlider.IsEnabled)
            {
                return;
            }

            var percent = (int)Math.Round(e.NewValue);
            VolumeLabel.Text = $"{percent}%";
            plugin.SetMediaSessionVolume(percent / 100f, false);
        }

        private void VolumeSlider_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Down && e.Key != Key.Up)
            {
                return;
            }

            e.Handled = true;
            var direction = e.Key == Key.Left || e.Key == Key.Down ? -1 : 1;
            var step = Math.Max(1, plugin.Settings.VolumeStepPercent);
            VolumeSlider.Value = Math.Max(0, Math.Min(100, VolumeSlider.Value + step * direction));
        }
    }
}
