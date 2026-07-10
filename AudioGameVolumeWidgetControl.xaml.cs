using System.Windows;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioGameVolumeWidgetControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioGameVolumeWidgetControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            DataContext = plugin.Theme;
            Loaded += AudioGameVolumeWidgetControl_Loaded;
        }

        private void AudioGameVolumeWidgetControl_Loaded(object sender, RoutedEventArgs e)
        {
            plugin.Theme.Refresh();
        }
    }
}
