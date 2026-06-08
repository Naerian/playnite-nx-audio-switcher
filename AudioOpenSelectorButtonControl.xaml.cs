using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            var menu = new ContextMenu
            {
                PlacementTarget = OpenButton
            };

            foreach (var device in plugin.GetThemeSelectorDevices().OrderBy(a => a.EffectiveName))
            {
                var item = new MenuItem
                {
                    Header = device.SettingsDisplayName,
                    Tag = device.Id
                };
                item.Click += DeviceMenuItem_Click;
                menu.Items.Add(item);
            }

            OpenButton.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void DeviceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuItem;
            var deviceId = item?.Tag as string;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            plugin.SetThemeSelectedDevice(deviceId);
            Refresh();
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
