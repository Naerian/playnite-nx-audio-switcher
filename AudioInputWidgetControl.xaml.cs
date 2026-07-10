using System.Windows;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioInputWidgetControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioInputWidgetControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            DataContext = plugin.Theme;
            Loaded += AudioInputWidgetControl_Loaded;
        }

        private void AudioInputWidgetControl_Loaded(object sender, RoutedEventArgs e)
        {
            plugin.Theme.Refresh();
        }
    }
}
