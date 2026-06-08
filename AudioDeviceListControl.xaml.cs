using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioDeviceListControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public event EventHandler DeviceSelected;

        public AudioDeviceListControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioDeviceListControl_Loaded;
        }

        private void AudioDeviceListControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            DeviceButtonsPanel.Children.Clear();

            try
            {
                foreach (var device in plugin.GetThemeSelectorDevices().OrderBy(a => a.EffectiveName))
                {
                    var button = new Button
                    {
                        Content = device.SettingsDisplayName,
                        Tag = device.Id,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 0, 0, 6),
                        Padding = new Thickness(12, 8, 12, 8),
                        MinWidth = 240
                    };

                    button.Click += DeviceButton_Click;
                    DeviceButtonsPanel.Children.Add(button);
                }
            }
            catch (Exception)
            {
                DeviceButtonsPanel.Children.Clear();
            }
        }

        public void FocusFirstDevice()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DeviceButtonsPanel.Children
                    .OfType<Button>()
                    .FirstOrDefault()
                    ?.Focus();
            }), DispatcherPriority.ApplicationIdle);
        }

        private void DeviceButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var deviceId = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            plugin.SetThemeSelectedDevice(deviceId);
            Refresh();
            DeviceSelected?.Invoke(this, EventArgs.Empty);
        }
    }
}
