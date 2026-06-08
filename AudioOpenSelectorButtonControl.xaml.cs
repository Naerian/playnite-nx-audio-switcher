using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
        private Panel overlayHost;
        private Grid overlayRoot;
        private Border selectorPanel;

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
            deviceList.Refresh();

            if (overlayRoot != null)
            {
                CloseSelector();
                return;
            }

            overlayHost = FindOverlayHost();
            if (overlayHost == null)
            {
                plugin.OpenThemeDeviceSelector(Refresh);
                return;
            }

            overlayRoot = CreateOverlayRoot();
            selectorPanel = CreateSelectorPanel();
            selectorPanel.Child = deviceList;

            PositionSelectorPanel();
            overlayRoot.Children.Add(selectorPanel);
            Panel.SetZIndex(overlayRoot, 10000);

            if (overlayHost is Grid hostGrid)
            {
                Grid.SetRow(overlayRoot, 0);
                Grid.SetColumn(overlayRoot, 0);
                Grid.SetRowSpan(overlayRoot, Math.Max(1, hostGrid.RowDefinitions.Count));
                Grid.SetColumnSpan(overlayRoot, Math.Max(1, hostGrid.ColumnDefinitions.Count));
            }

            overlayHost.Children.Add(overlayRoot);
            plugin.RegisterThemeSelector(deviceList, IsSelectorOpen, CloseSelector);
            Dispatcher.BeginInvoke(new Action(() => deviceList?.FocusFirstDevice()), DispatcherPriority.ApplicationIdle);
        }

        public void CloseSelector()
        {
            if (selectorPanel != null)
            {
                selectorPanel.Child = null;
            }

            if (overlayRoot != null && overlayHost != null)
            {
                overlayHost.Children.Remove(overlayRoot);
            }

            plugin.ClearThemeSelector(deviceList);
            overlayRoot = null;
            selectorPanel = null;
            overlayHost = null;
            Refresh();
            AudioSwitcherThemeOpenSelectorButton.Focus();
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
        }

        private Brush ResolvePanelBrush()
        {
            return TryFindResource("OverlayMenuBackgroundBrush") as Brush ??
                   TryFindResource("ControlBackgroundDarkBrush") as Brush ??
                   TryFindResource("ControlBackgroundBrush") as Brush ??
                   new SolidColorBrush(Color.FromArgb(242, 10, 13, 20));
        }

        private Grid CreateOverlayRoot()
        {
            var root = new Grid
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            root.MouseDown += (_, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, root))
                {
                    CloseSelector();
                }
            };
            return root;
        }

        private Border CreateSelectorPanel()
        {
            var panel = new Border
            {
                Width = 460,
                MaxHeight = 650,
                Padding = new Thickness(22),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Background = ResolvePanelBrush(),
                BorderBrush = TryFindResource("GlyphBrush") as Brush ??
                              TryFindResource("SelectionBrush") as Brush ??
                              Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            TextElement.SetForeground(panel, TryFindResource("TextBrush") as Brush ?? Brushes.White);

            var style = TryFindResource("ExtensionsBorder") as Style;
            if (style != null && (style.TargetType == null || style.TargetType.IsAssignableFrom(typeof(Border))))
            {
                panel.Style = style;
                panel.Width = 460;
                panel.HorizontalAlignment = HorizontalAlignment.Left;
                panel.VerticalAlignment = VerticalAlignment.Top;
            }

            return panel;
        }

        private void PositionSelectorPanel()
        {
            try
            {
                overlayHost.UpdateLayout();
                var point = AudioSwitcherThemeOpenSelectorButton
                    .TransformToAncestor(overlayHost)
                    .Transform(new Point(0, AudioSwitcherThemeOpenSelectorButton.ActualHeight + 8));

                var width = selectorPanel.Width > 0 ? selectorPanel.Width : 460;
                var left = point.X + AudioSwitcherThemeOpenSelectorButton.ActualWidth - width;
                if (overlayHost.ActualWidth > 0)
                {
                    left = Math.Min(left, overlayHost.ActualWidth - width - 24);
                }

                left = Math.Max(24, left);
                selectorPanel.Margin = new Thickness(left, Math.Max(24, point.Y), 0, 0);
            }
            catch (Exception)
            {
                selectorPanel.Margin = new Thickness(24, 72, 0, 0);
            }
        }

        private bool IsSelectorOpen()
        {
            return overlayRoot != null && overlayHost?.Children.Contains(overlayRoot) == true;
        }

        private Panel FindOverlayHost()
        {
            Panel best = null;
            var current = this as DependencyObject;
            while (current != null)
            {
                if (current is Panel panel && panel.ActualWidth >= 400 && panel.ActualHeight >= 300)
                {
                    best = panel;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return best ?? Parent as Panel;
        }
    }
}
