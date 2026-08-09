using System.Windows;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioBatteryTopPanelControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioBatteryTopPanelControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            DataContext = plugin.Theme;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            plugin.Theme.Refresh();
        }
    }
}
