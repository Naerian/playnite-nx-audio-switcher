using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
                    button.PreviewKeyDown += DeviceButton_PreviewKeyDown;
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

        public bool ActivateFocusedDevice()
        {
            var focusedButton = Keyboard.FocusedElement as Button;
            if (focusedButton == null || !DeviceButtonsPanel.Children.Contains(focusedButton))
            {
                focusedButton = DeviceButtonsPanel.Children
                    .OfType<Button>()
                    .FirstOrDefault(a => a.IsKeyboardFocusWithin || a.IsFocused);
            }

            if (focusedButton == null)
            {
                return false;
            }

            SelectDevice(focusedButton);
            return true;
        }

        private void DeviceButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            e.Handled = true;
            SelectDevice(sender as Button);
        }

        private void DeviceButton_Click(object sender, RoutedEventArgs e)
        {
            SelectDevice(sender as Button);
        }

        private void SelectDevice(Button button)
        {
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
