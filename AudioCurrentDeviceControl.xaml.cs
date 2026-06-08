using System;
using System.Windows;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioCurrentDeviceControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioCurrentDeviceControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioCurrentDeviceControl_Loaded;
        }

        private void AudioCurrentDeviceControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            try
            {
                CurrentDeviceText.Text = plugin.GetCurrentDeviceDisplayLabel();
            }
            catch (Exception)
            {
                CurrentDeviceText.Text = plugin.Loc("LOCAS_Audio");
            }
        }
    }
}
