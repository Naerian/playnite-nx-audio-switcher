using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioOpenSelectorButtonControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioOpenSelectorButtonControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioOpenSelectorButtonControl_Loaded;
        }

        private void AudioOpenSelectorButtonControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            plugin.OpenThemeDeviceSelector(Refresh);
        }

        private void OpenButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            e.Handled = true;
            plugin.OpenThemeDeviceSelector(Refresh);
        }

        private void Refresh()
        {
            try
            {
                DeviceIconPath.Data = GetCurrentIconGeometry();
                OpenButton.ToolTip = plugin.GetCurrentDeviceDisplayName();
            }
            catch (Exception)
            {
                DeviceIconPath.Data = plugin.GetIconGeometry("volume-2");
                OpenButton.ToolTip = plugin.Loc("LOCAS_Audio");
            }
        }

        private Geometry GetCurrentIconGeometry()
        {
            var themeIcon = TryFindResource("AudioSwitcher_DefaultIconGeometry") as Geometry;
            var iconGeometry = plugin.GetCurrentDeviceIconGeometry();
            return iconGeometry ?? themeIcon ?? plugin.GetIconGeometry("volume-2");
        }
    }
}
