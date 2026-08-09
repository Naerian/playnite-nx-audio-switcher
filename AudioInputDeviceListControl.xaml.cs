using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioInputDeviceListControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public event EventHandler DeviceSelected;

        public AudioInputDeviceListControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioInputDeviceListControl_Loaded;
            Unloaded += AudioInputDeviceListControl_Unloaded;
        }

        private void AudioInputDeviceListControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
            plugin.RegisterInputDeviceList(this);
        }

        private void AudioInputDeviceListControl_Unloaded(object sender, RoutedEventArgs e)
        {
            plugin.ClearInputDeviceList(this);
        }

        public void Refresh()
        {
            DeviceButtonsPanel.Children.Clear();

            try
            {
                foreach (var device in plugin.GetThemeSelectorInputDevices().OrderBy(a => a.EffectiveName))
                {
                    var button = new Button
                    {
                        Content = device.SettingsDisplayName,
                        Tag = device.Id,
                        Focusable = true,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 0, 0, 6),
                        Padding = new Thickness(12, 8, 12, 8),
                        MinWidth = 240
                    };

                    KeyboardNavigation.SetIsTabStop(button, true);
                    KeyboardNavigation.SetDirectionalNavigation(button, KeyboardNavigationMode.Continue);

                    button.Click += DeviceButton_Click;
                    button.PreviewKeyDown += DeviceButton_PreviewKeyDown;
                    DeviceButtonsPanel.Children.Add(button);
                }
            }
            catch
            {
                DeviceButtonsPanel.Children.Clear();
            }
        }

        public void FocusFirstDevice()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var firstButton = DeviceButtonsPanel.Children
                    .OfType<Button>()
                    .FirstOrDefault();
                if (firstButton == null)
                {
                    return;
                }

                var focusScope = FocusManager.GetFocusScope(firstButton);
                if (focusScope != null)
                {
                    FocusManager.SetFocusedElement(focusScope, firstButton);
                }

                firstButton.Focus();
                Keyboard.Focus(firstButton);
            }), DispatcherPriority.ApplicationIdle);
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

            plugin.SetThemeSelectedInputDevice(deviceId);
            Refresh();
            DeviceSelected?.Invoke(this, EventArgs.Empty);
        }
    }
}
