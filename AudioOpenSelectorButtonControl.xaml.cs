using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioOpenSelectorButtonControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;
        private AudioDeviceListControl deviceList;

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
            OpenSelector();
        }

        private void OpenButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseSelector();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            e.Handled = true;
            OpenSelector();
        }

        public void OpenSelector()
        {
            EnsureDeviceList();
            ApplyPanelStyle();
            deviceList.Refresh();
            SelectorPopup.IsOpen = true;
        }

        public void CloseSelector()
        {
            SelectorPopup.IsOpen = false;
        }

        private void Refresh()
        {
            try
            {
                DeviceIconPath.Data = GetCurrentIconGeometry();
                AudioSwitcherThemeOpenSelectorButton.ToolTip = plugin.GetCurrentDeviceDisplayName();
            }
            catch (Exception)
            {
                DeviceIconPath.Data = plugin.GetIconGeometry("volume-2");
                AudioSwitcherThemeOpenSelectorButton.ToolTip = plugin.Loc("LOCAS_Audio");
            }
        }

        private Geometry GetCurrentIconGeometry()
        {
            var themeIcon = TryFindResource("AudioSwitcher_DefaultIconGeometry") as Geometry;
            var iconGeometry = plugin.GetCurrentDeviceIconGeometry();
            return iconGeometry ?? themeIcon ?? plugin.GetIconGeometry("volume-2");
        }

        private void EnsureDeviceList()
        {
            if (deviceList != null)
            {
                return;
            }

            deviceList = new AudioDeviceListControl(plugin);
            deviceList.DeviceSelected += (_, __) =>
            {
                CloseSelector();
                Refresh();
            };
            SelectorHost.Content = deviceList;
        }

        private void ApplyPanelStyle()
        {
            var style = TryFindResource("ExtensionsBorder") as Style;
            if (style != null && (style.TargetType == null || style.TargetType.IsAssignableFrom(typeof(System.Windows.Controls.Border))))
            {
                SelectorPanel.Style = style;
            }

            SelectorPanel.Background = ResolvePanelBrush();
            SelectorPanel.BorderBrush = TryFindResource("GlyphBrush") as Brush ??
                                        TryFindResource("SelectionBrush") as Brush ??
                                        Brushes.White;
        }

        private Brush ResolvePanelBrush()
        {
            return TryFindResource("OverlayMenuBackgroundBrush") as Brush ??
                   TryFindResource("ControlBackgroundDarkBrush") as Brush ??
                   TryFindResource("ControlBackgroundBrush") as Brush ??
                   new SolidColorBrush(Color.FromArgb(242, 10, 13, 20));
        }

        private void SelectorPopup_Opened(object sender, EventArgs e)
        {
            plugin.RegisterThemeSelector(deviceList, () => SelectorPopup.IsOpen, CloseSelector);
            Dispatcher.BeginInvoke(new Action(() => deviceList?.FocusFirstDevice()), DispatcherPriority.ApplicationIdle);
        }

        private void SelectorPopup_Closed(object sender, EventArgs e)
        {
            plugin.ClearThemeSelector(deviceList);
            Refresh();
            AudioSwitcherThemeOpenSelectorButton.Focus();
        }
    }
}
