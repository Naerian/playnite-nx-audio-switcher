using System;
using System.Windows;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioOpenSelectorButtonControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;
        private AudioDeviceListControl deviceListControl;

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
            if (deviceListControl == null)
            {
                deviceListControl = new AudioDeviceListControl(plugin);
                deviceListControl.DeviceSelected += DeviceListControl_DeviceSelected;
                SelectorPopupContent.Child = deviceListControl;
            }

            deviceListControl.Refresh();
            SelectorPopup.IsOpen = true;
        }

        private void DeviceListControl_DeviceSelected(object sender, EventArgs e)
        {
            SelectorPopup.IsOpen = false;
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                OpenButton.Content = plugin.GetCurrentDeviceDisplayLabel();
            }
            catch (Exception)
            {
                OpenButton.Content = plugin.Loc("LOCAS_Audio");
            }
        }
    }
}
