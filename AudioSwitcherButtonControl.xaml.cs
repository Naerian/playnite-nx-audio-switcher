using System.Windows;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioSwitcherButtonControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioSwitcherButtonControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioSwitcherButtonControl_Loaded;
        }

        private void AudioSwitcherButtonControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void SwitchButton_Click(object sender, RoutedEventArgs e)
        {
            plugin.ToggleCustomDevices();
            Refresh();
        }

        private void Refresh()
        {
            plugin.Theme?.Refresh();
        }
    }
}
