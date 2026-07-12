using System.Windows;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioMediaWidgetControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioMediaWidgetControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            DataContext = plugin.Theme;
            Loaded += AudioMediaWidgetControl_Loaded;
        }

        private void AudioMediaWidgetControl_Loaded(object sender, RoutedEventArgs e)
        {
            plugin.Theme.Refresh();
        }
    }
}
