using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioDeviceSelectorPanelControl : PluginUserControl
    {
        public AudioDeviceSelectorPanelControl(AudioSwitcherPlugin plugin)
        {
            InitializeComponent();
            SelectorHost.Content = new AudioDeviceSelectorControl(plugin);
        }
    }
}
