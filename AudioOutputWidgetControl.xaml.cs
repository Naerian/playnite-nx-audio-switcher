using System.Windows;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioOutputWidgetControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioOutputWidgetControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            DataContext = plugin.Theme;
            Loaded += AudioOutputWidgetControl_Loaded;
        }

        private void AudioOutputWidgetControl_Loaded(object sender, RoutedEventArgs e)
        {
            plugin.Theme.Refresh();
        }
    }
}
