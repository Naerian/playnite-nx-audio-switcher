using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioVolumeSliderControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;
        private bool isRefreshing;

        public AudioVolumeSliderControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioVolumeSliderControl_Loaded;
        }

        private void AudioVolumeSliderControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            try
            {
                var state = plugin.GetCurrentVolumeState();
                SetSliderValue(state.VolumePercent, state.IsMuted);
            }
            catch
            {
                SetSliderValue(0, false);
            }
        }

        private void SetSliderValue(int value, bool muted)
        {
            isRefreshing = true;
            try
            {
                VolumeSlider.Value = Math.Max(0, Math.Min(100, value));
                VolumeLabel.Text = muted ? plugin.Loc("LOCAS_Muted") : $"{(int)Math.Round(VolumeSlider.Value)}%";
            }
            finally
            {
                isRefreshing = false;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isRefreshing || !IsLoaded)
            {
                return;
            }

            var percent = (int)Math.Round(e.NewValue);
            VolumeLabel.Text = $"{percent}%";
            plugin.SetVolume(percent / 100f, false);
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
