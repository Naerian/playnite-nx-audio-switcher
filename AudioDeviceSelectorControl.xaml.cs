using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioDeviceSelectorControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;
        private bool isRefreshing;

        public AudioDeviceSelectorControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioDeviceSelectorControl_Loaded;
        }

        private void AudioDeviceSelectorControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void DevicesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isRefreshing || DevicesComboBox.SelectedValue == null)
            {
                return;
            }

            plugin.SetThemeSelectedDevice(DevicesComboBox.SelectedValue.ToString());
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                isRefreshing = true;
                var devices = plugin.GetThemeSelectorDevices().ToList();
                DevicesComboBox.ItemsSource = devices;
                DevicesComboBox.SelectedValue = devices.FirstOrDefault(a => a.IsDefault)?.Id;
            }
            catch (Exception)
            {
                DevicesComboBox.ItemsSource = null;
            }
            finally
            {
                isRefreshing = false;
            }
        }
    }
}
