using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Win32;
using IoFile = System.IO.File;
using IoPath = System.IO.Path;

namespace PlayniteAudioSwitcher
{
    public partial class AudioSwitcherSettingsView : UserControl
    {
        private ScrollViewer hostScrollViewer;
        private Window hostWindow;

        public AudioSwitcherSettingsView()
        {
            InitializeComponent();
            var iconTemplate = CreateIconTemplate();
            DesktopTopPanelIconBox.ItemTemplate = iconTemplate;
            BatteryIndicatorIconBox.ItemTemplate = iconTemplate;
            AboutVersionText.Text = string.Format(
                TryFindResource("LOCAS_VersionAuthorFormat") as string ?? "Version {0} | Narian",
                GetInstalledVersion());
            DataContextChanged += (_, __) =>
            {
                RebuildDeviceRows();
                UpdateOverview();
            };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            ApplyPreferredWindowSize();
            AttachToHost();
            Dispatcher.BeginInvoke(new Action(AttachToHost), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(AttachToHost), DispatcherPriority.ApplicationIdle);
            Dispatcher.BeginInvoke(new Action(FillSelectedContentHosts), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(FillSelectedContentHosts), DispatcherPriority.ApplicationIdle);
            RefreshOnLoad();
        }

        private async void RefreshOnLoad()
        {
            var settings = DataContext as AudioSwitcherSettings;
            if (settings != null && settings.Plugin != null)
            {
                await settings.Plugin.RefreshDeviceBatteriesAsync();
                settings.RefreshDevices();
            }

            RebuildDeviceRows();
            RebuildGameProfileRows();
            UpdateSpatialSoundToolStatus();
            UpdateOverview();
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            DetachFromHost();
        }

        private void AttachToHost()
        {
            DetachFromHost();
            hostScrollViewer = FindAncestorScrollViewer();
            if (hostScrollViewer != null)
            {
                hostScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                hostScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                hostScrollViewer.SizeChanged += OnHostSizeChanged;
            }

            hostWindow = Window.GetWindow(this);
            if (hostWindow != null)
            {
                hostWindow.SizeChanged += OnHostSizeChanged;
            }

            ApplyViewportSize();
        }

        private void DetachFromHost()
        {
            if (hostScrollViewer != null)
            {
                hostScrollViewer.SizeChanged -= OnHostSizeChanged;
                hostScrollViewer = null;
            }

            if (hostWindow != null)
            {
                hostWindow.SizeChanged -= OnHostSizeChanged;
                hostWindow = null;
            }
        }

        private void OnHostSizeChanged(object sender, SizeChangedEventArgs args)
        {
            ApplyViewportSize();
            FillSelectedContentHosts();
        }

        private void RootTabsSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            Dispatcher.BeginInvoke(new Action(FillSelectedContentHosts), DispatcherPriority.Loaded);
        }

        private void FillSelectedContentHosts()
        {
            StretchSelectedContent(this);
        }

        private static void StretchSelectedContent(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var presenter = child as ContentPresenter;
                if (presenter != null && presenter.Name == "PART_SelectedContentHost")
                {
                    presenter.HorizontalAlignment = HorizontalAlignment.Stretch;
                    presenter.VerticalAlignment = VerticalAlignment.Stretch;
                    var content = presenter.Content as FrameworkElement;
                    if (content == null && VisualTreeHelper.GetChildrenCount(presenter) > 0)
                    {
                        content = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
                    }

                    if (content != null)
                    {
                        content.HorizontalAlignment = HorizontalAlignment.Stretch;
                        content.VerticalAlignment = VerticalAlignment.Stretch;
                        content.ClearValue(WidthProperty);
                        content.ClearValue(HeightProperty);
                    }
                }

                StretchSelectedContent(child);
            }
        }

        private void ApplyViewportSize()
        {
            double width = 0;
            double height = 0;
            if (hostScrollViewer != null)
            {
                width = hostScrollViewer.ViewportWidth > 8
                    ? hostScrollViewer.ViewportWidth
                    : hostScrollViewer.ActualWidth;
                height = hostScrollViewer.ViewportHeight > 8
                    ? hostScrollViewer.ViewportHeight
                    : hostScrollViewer.ActualHeight;
            }

            if (width < 8 || height < 8)
            {
                var slot = FindWindowGridSlot();
                if (slot.Width > 8)
                {
                    width = slot.Width;
                }
                if (slot.Height > 8)
                {
                    height = slot.Height;
                }
            }

            if ((width < 8 || height < 8) && hostWindow != null)
            {
                var content = hostWindow.Content as FrameworkElement;
                if (content != null)
                {
                    if (width < 8)
                    {
                        width = content.ActualWidth;
                    }
                    if (height < 8)
                    {
                        height = content.ActualHeight;
                    }
                }
            }

            if (width > 8 && Math.Abs(Width - width) > 1)
            {
                Width = width;
            }

            if (height > 8 && Math.Abs(Height - height) > 1)
            {
                Height = height;
            }

            FillSelectedContentHosts();
        }

        private Size FindWindowGridSlot()
        {
            for (var parent = VisualTreeHelper.GetParent(this);
                 parent != null;
                 parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is Window)
                {
                    break;
                }

                var grid = parent as Grid;
                if (grid == null || grid.RowDefinitions.Count < 2 || grid.ActualWidth < 400)
                {
                    continue;
                }

                var rowHeight = grid.RowDefinitions[0].ActualHeight;
                if (rowHeight > 200)
                {
                    return new Size(grid.ActualWidth, rowHeight);
                }
            }

            return new Size(0, 0);
        }

        private ScrollViewer FindAncestorScrollViewer()
        {
            for (var parent = VisualTreeHelper.GetParent(this);
                 parent != null;
                 parent = VisualTreeHelper.GetParent(parent))
            {
                var scrollViewer = parent as ScrollViewer;
                if (scrollViewer != null)
                {
                    return scrollViewer;
                }

                if (parent is Window)
                {
                    return null;
                }
            }

            return null;
        }

        private void ApplyPreferredWindowSize()
        {
            var window = Window.GetWindow(this);
            if (window == null)
            {
                return;
            }

            window.SizeToContent = SizeToContent.Manual;
            if (window.MinWidth < 1000)
            {
                window.MinWidth = 1000;
            }
            if (window.MinHeight < 700)
            {
                window.MinHeight = 700;
            }
            if (window.ActualWidth < 1100 && window.Width < 1100)
            {
                window.Width = 1100;
            }
            if (window.ActualHeight < 780 && window.Height < 780)
            {
                window.Height = 780;
            }
        }

        private async void RefreshOverview(object sender, RoutedEventArgs e)
        {
            if (DataContext is AudioSwitcherSettings settings)
            {
                await settings.Plugin.RefreshDeviceBatteriesAsync();
                settings.RefreshDevices();
                RebuildDeviceRows();
            }

            UpdateSpatialSoundToolStatus();
            UpdateOverview();
        }

        private void UpdateOverview()
        {
            if (OverviewOutputText == null || !(DataContext is AudioSwitcherSettings settings) || settings.Plugin == null)
            {
                return;
            }

            var plugin = settings.Plugin;
            var outputDevice = plugin.GetCurrentPlaybackDeviceForTheme();
            UpdateDeviceOverview(
                outputDevice == null ? null : plugin.GetDeviceDisplayNameForTheme(outputDevice),
                GetOverviewVolume(new Func<AudioVolumeState>(plugin.GetCurrentVolumeState)),
                outputDevice,
                OverviewOutputText,
                OverviewOutputVolumeText,
                OverviewOutputVolumePill,
                OverviewOutputBatteryText,
                OverviewOutputBatteryPill,
                OverviewOutputPills);

            var inputDevice = plugin.GetCurrentRecordingDeviceForTheme();
            UpdateDeviceOverview(
                inputDevice == null ? null : plugin.GetInputDeviceDisplayNameForTheme(inputDevice),
                GetOverviewVolume(new Func<AudioVolumeState>(plugin.GetCurrentInputVolumeState)),
                inputDevice,
                OverviewInputText,
                OverviewInputVolumeText,
                OverviewInputVolumePill,
                OverviewInputBatteryText,
                OverviewInputBatteryPill,
                OverviewInputPills);

            var profileCount = settings.AvailableGameProfiles.Count;
            var manualProcessCount = settings.AvailableGameProfiles.Count(profile => !string.IsNullOrWhiteSpace(profile.AudioProcessName));
            OverviewProfilesCountPill.Text = profileCount.ToString();
            OverviewProfilesText.Text = string.Format(
                ResourceText("LOCAS_OverviewProfilesFormat", "{0} configured profiles | {1} manual audio processes"),
                profileCount,
                manualProcessCount);

            var spatialEnabled = settings.SpatialSoundIntegrationEnabled;
            OverviewSpatialSoundPill.Text = spatialEnabled
                ? ResourceText("LOCAS_SpatialOn", "On")
                : ResourceText("LOCAS_SpatialOff", "Off");
            OverviewSpatialSoundText.Text = spatialEnabled
                ? GetSpatialSoundToolStatus()
                : ResourceText("LOCAS_SpatialOff", "Off");

            try
            {
                var activeSessions = plugin.AudioDevices.GetPlaybackAudioSessions().Count(session => session.IsActive);
                OverviewSessionsCountPill.Text = activeSessions.ToString();
                OverviewSessionsText.Text = string.Format(
                    ResourceText("LOCAS_OverviewSessionsFormat", "{0} active playback sessions"),
                    activeSessions);
            }
            catch
            {
                OverviewSessionsCountPill.Text = "0";
                OverviewSessionsText.Text = string.Format(
                    ResourceText("LOCAS_OverviewSessionsFormat", "{0} active playback sessions"),
                    0);
            }
        }

        private void UpdateDeviceOverview(
            string deviceName,
            AudioVolumeState volumeState,
            AudioDevice device,
            TextBlock deviceText,
            TextBlock volumeText,
            Border volumePill,
            TextBlock batteryText,
            Border batteryPill,
            Panel pillsPanel)
        {
            if (device == null)
            {
                deviceText.Text = ResourceText("LOCAS_OverviewNoDevice", "No default device");
                CollapseOverviewPills(pillsPanel, volumePill, batteryPill);
                return;
            }

            deviceText.Text = string.IsNullOrWhiteSpace(deviceName)
                ? (string.IsNullOrWhiteSpace(device.Name) ? device.Id : device.Name)
                : deviceName;

            var showVolume = volumeState != null && volumeState.IsAvailable;
            if (showVolume)
            {
                volumeText.Text = string.Format(
                    ResourceText("LOCAS_OverviewVolumeFormat", "{0}% volume"),
                    volumeState.VolumePercent);
                if (volumeState.IsMuted)
                {
                    volumeText.Text += $" | {ResourceText("LOCAS_Muted", "Muted")}";
                }
            }

            batteryText.ClearValue(TextBlock.ForegroundProperty);
            batteryText.Opacity = 1;
            batteryText.Inlines.Clear();
            batteryText.Inlines.Add(new System.Windows.Documents.Run(
                $"{ResourceText("LOCAS_Battery", "Battery")}: "));
            batteryText.Inlines.Add(new System.Windows.Documents.Run(
                device != null && device.HasBattery ? device.BatteryLabel : "\u2014")
            {
                Foreground = BatteryColorPalette.GetBrush(device),
                FontWeight = FontWeights.SemiBold
            });

            SetElementVisibility(volumePill, showVolume);
            SetElementVisibility(batteryPill, true);
            SetElementVisibility(pillsPanel, true);
        }

        private static void CollapseOverviewPills(Panel pillsPanel, Border volumePill, Border batteryPill)
        {
            SetElementVisibility(volumePill, false);
            SetElementVisibility(batteryPill, false);
            SetElementVisibility(pillsPanel, false);
        }

        private static void SetElementVisibility(UIElement element, bool visible)
        {
            if (element != null)
            {
                element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static AudioVolumeState GetOverviewVolume(Func<AudioVolumeState> getVolume)
        {
            try
            {
                var volumeState = getVolume();
                return volumeState ?? new AudioVolumeState { IsAvailable = false };
            }
            catch
            {
                return new AudioVolumeState { IsAvailable = false };
            }
        }

        private string ResourceText(string key, string fallback)
        {
            return TryFindResource(key) as string ?? fallback;
        }

        private void RebuildDeviceRows()
        {
            if (DeviceRowsPanel == null || InputDeviceRowsPanel == null || !(DataContext is AudioSwitcherSettings settings))
            {
                return;
            }

            BuildDeviceRows(DeviceRowsPanel, settings.AvailablePlaybackDevices, settings, "LOCAS_DefaultVolume");
            BuildDeviceRows(InputDeviceRowsPanel, settings.AvailableRecordingDevices, settings, "LOCAS_DefaultInputVolume");
            RebuildGameProfileRows();
        }

        private void ShowDisabledSystemDevicesChanged(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is AudioSwitcherSettings settings))
            {
                return;
            }

            settings.RefreshDevices();
            RebuildDeviceRows();
        }

        private void BuildDeviceRows(StackPanel panel, IEnumerable<AudioDevice> devices, AudioSwitcherSettings settings, string defaultVolumeLabelResource)
        {
            panel.Children.Clear();

            var deviceList = (devices ?? Enumerable.Empty<AudioDevice>()).ToList();
            if (deviceList.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = ResourceText(
                        "LOCAS_NoSystemDevices",
                        "Windows did not report any devices here. Check Playnite.log and avoid running Playnite as Administrator."),
                    FontSize = 12,
                    Opacity = 0.78,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                panel.Children.Add(empty);
                return;
            }
            var grid = new UniformGrid
            {
                Columns = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            for (var index = 0; index < deviceList.Count; index++)
            {
                grid.Children.Add(CreateDeviceCard(
                    deviceList[index],
                    settings,
                    defaultVolumeLabelResource,
                    index));
            }

            panel.Children.Add(grid);
        }

        private UIElement CreateDeviceCard(
            AudioDevice device,
            AudioSwitcherSettings settings,
            string defaultVolumeLabelResource,
            int index)
        {
            var card = new Border
            {
                Style = TryFindResource("DeviceCard") as Style ?? TryFindResource("SummaryCard") as Style,
                Margin = new Thickness(index % 2 == 0 ? 0 : 8, 0, index % 2 == 0 ? 8 : 0, 16),
                MinHeight = 0
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var infoPanel = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            var nameText = new TextBlock
            {
                Style = TryFindResource("SummaryCardValue") as Style,
                TextWrapping = TextWrapping.Wrap,
                Text = string.IsNullOrWhiteSpace(device.SettingsDisplayName)
                    ? (string.IsNullOrWhiteSpace(device.Name) ? device.Id : device.Name)
                    : device.SettingsDisplayName
            };
            infoPanel.Children.Add(nameText);

            var pills = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            pills.Children.Add(CreateMetricPill(
                ResourceText("LOCAS_Status", "Status"),
                device.StatusDisplayName ?? "\u2014"));
            pills.Children.Add(CreateMetricPill(
                ResourceText("LOCAS_Battery", "Battery"),
                device.HasBattery ? device.BatteryLabel : "\u2014",
                BatteryColorPalette.GetBrush(device)));
            infoPanel.Children.Add(pills);
            top.Children.Add(infoPanel);

            var controlsPanel = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };

            var visibleBox = new CheckBox
            {
                IsChecked = device.IsVisible,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            visibleBox.SetResourceReference(ContentControl.ContentProperty, "LOCAS_Visible");
            visibleBox.Checked += (_, __) => device.IsVisible = true;
            visibleBox.Unchecked += (_, __) => device.IsVisible = false;
            controlsPanel.Children.Add(visibleBox);

            var iconPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            iconPanel.Children.Add(CreateFieldLabel("LOCAS_Icon"));
            var iconBox = new ComboBox
            {
                ItemsSource = settings.IconOptions,
                SelectedValuePath = "Id",
                SelectedValue = settings.ResolveIconId(device.Icon),
                ItemTemplate = CreateIconTemplate()
            };
            iconBox.SelectionChanged += (_, __) =>
            {
                device.Icon = iconBox.SelectedValue?.ToString();
                device.IsIconSuggested = false;
            };
            iconPanel.Children.Add(iconBox);
            controlsPanel.Children.Add(iconPanel);

            var defaultVolumePanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            defaultVolumePanel.Children.Add(CreateFieldLabel(defaultVolumeLabelResource));
            var defaultVolumeGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            defaultVolumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            defaultVolumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            defaultVolumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var defaultVolumeEnabled = new CheckBox
            {
                IsChecked = device.DefaultVolumePercent.HasValue,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var defaultVolumeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = device.DefaultVolumePercent ?? 50,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = device.DefaultVolumePercent.HasValue
            };
            var defaultVolumeValue = new TextBlock
            {
                MinWidth = 40,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                Opacity = 0.85
            };
            UpdateDefaultVolume(device, defaultVolumeEnabled, defaultVolumeSlider, defaultVolumeValue);
            defaultVolumeEnabled.Checked += (_, __) =>
            {
                device.DefaultVolumePercent = (int)Math.Round(defaultVolumeSlider.Value);
                UpdateDefaultVolume(device, defaultVolumeEnabled, defaultVolumeSlider, defaultVolumeValue);
            };
            defaultVolumeEnabled.Unchecked += (_, __) =>
            {
                device.DefaultVolumePercent = null;
                UpdateDefaultVolume(device, defaultVolumeEnabled, defaultVolumeSlider, defaultVolumeValue);
            };
            defaultVolumeSlider.ValueChanged += (_, __) =>
            {
                if (defaultVolumeEnabled.IsChecked == true)
                {
                    device.DefaultVolumePercent = (int)Math.Round(defaultVolumeSlider.Value);
                }

                UpdateDefaultVolume(device, defaultVolumeEnabled, defaultVolumeSlider, defaultVolumeValue);
            };
            Grid.SetColumn(defaultVolumeSlider, 1);
            Grid.SetColumn(defaultVolumeValue, 2);
            defaultVolumeGrid.Children.Add(defaultVolumeEnabled);
            defaultVolumeGrid.Children.Add(defaultVolumeSlider);
            defaultVolumeGrid.Children.Add(defaultVolumeValue);
            defaultVolumePanel.Children.Add(defaultVolumeGrid);
            controlsPanel.Children.Add(defaultVolumePanel);

            Grid.SetColumn(controlsPanel, 1);
            top.Children.Add(controlsPanel);
            root.Children.Add(top);

            var customNamePanel = new StackPanel();
            customNamePanel.Children.Add(CreateFieldLabel("LOCAS_PlayniteName"));
            var customNameBox = new TextBox
            {
                Text = AudioSwitcherSettings.SanitizeCustomName(device.CustomName) ?? string.Empty
            };
            customNameBox.TextChanged += (_, __) => device.CustomName = customNameBox.Text;
            customNamePanel.Children.Add(customNameBox);
            var customNameHelp = new TextBlock
            {
                Style = TryFindResource("HintText") as Style
            };
            customNameHelp.SetResourceReference(TextBlock.TextProperty, "LOCAS_CustomNameHelp");
            customNamePanel.Children.Add(customNameHelp);
            Grid.SetRow(customNamePanel, 1);
            root.Children.Add(customNamePanel);

            card.Child = root;
            return card;
        }

        private static TextBlock CreateFieldLabel(string resourceKey)
        {
            var label = new TextBlock
            {
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            label.SetResourceReference(TextBlock.TextProperty, resourceKey);
            return label;
        }

        private Border CreateMetricPill(string label, string value, Brush valueBrush = null)
        {
            var valueRun = new System.Windows.Documents.Run(string.IsNullOrWhiteSpace(value) ? "\u2014" : value);
            if (valueBrush != null)
            {
                valueRun.Foreground = valueBrush;
                valueRun.FontWeight = FontWeights.SemiBold;
            }

            var text = new TextBlock();
            text.Inlines.Add(new System.Windows.Documents.Run($"{label}: "));
            text.Inlines.Add(valueRun);

            return new Border
            {
                Style = TryFindResource("SummaryMetricPill") as Style,
                Child = text
            };
        }

        private void RebuildGameProfileRows()
        {
            if (GameProfileRowsPanel == null || NoGameProfilesText == null || !(DataContext is AudioSwitcherSettings settings))
            {
                return;
            }

            GameProfileRowsPanel.Children.Clear();
            var profiles = settings.AvailableGameProfiles.OrderBy(profile => profile.GameName).ToList();
            NoGameProfilesText.Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var profile in profiles)
            {
                GameProfileRowsPanel.Children.Add(CreateGameProfileRow(profile, settings));
            }
        }

        private UIElement CreateGameProfileRow(GameAudioProfileEntry profile, AudioSwitcherSettings settings)
        {
            var container = new Border
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 8, 16),
                Margin = new Thickness(0, 0, 0, 24)
            };
            container.SetResourceReference(Border.BorderBrushProperty, "GlyphBrush");

            var content = new StackPanel();
            var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.Children.Add(new TextBlock
            {
                Text = profile.GameName,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });
            var removeButton = CreatePreviewButton("LOCAS_RemoveProfile");
            removeButton.MinWidth = 90;
            removeButton.HorizontalAlignment = HorizontalAlignment.Right;
            removeButton.Margin = new Thickness(12, 0, 0, 0);
            removeButton.Click += (_, __) =>
            {
                var confirmed = settings.Plugin != null
                    ? settings.Plugin.ConfirmRemoveGameProfile(profile.GameName, false)
                    : MessageBox.Show(
                        string.Format(
                            ResourceText("LOCAS_ConfirmRemoveProfilePendingMessage", "Remove the Audio Switcher profile for \"{0}\" when settings are saved? Canceling settings keeps the profile."),
                            profile.GameName),
                        ResourceText("LOCAS_ConfirmRemoveProfileTitle", "Remove game profile"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) == MessageBoxResult.Yes;
                if (!confirmed)
                {
                    return;
                }

                settings.AvailableGameProfiles.Remove(profile);
                RebuildGameProfileRows();
            };
            Grid.SetColumn(removeButton, 1);
            titleRow.Children.Add(removeButton);
            content.Children.Add(titleRow);

            var fields = new Grid();
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            var outputBox = new ComboBox
            {
                ItemsSource = settings.GetProfileDeviceOptions(false, profile.DeviceId),
                DisplayMemberPath = "ProfileDisplayName",
                SelectedValuePath = "Id",
                SelectedValue = profile.DeviceId ?? string.Empty
            };
            outputBox.SelectionChanged += (_, __) => profile.DeviceId = outputBox.SelectedValue?.ToString();
            fields.Children.Add(CreateProfileField("LOCAS_MenuChooseOutput", outputBox, 0));

            var inputBox = new ComboBox
            {
                ItemsSource = settings.GetProfileDeviceOptions(true, profile.InputDeviceId),
                DisplayMemberPath = "ProfileDisplayName",
                SelectedValuePath = "Id",
                SelectedValue = profile.InputDeviceId ?? string.Empty
            };
            inputBox.SelectionChanged += (_, __) => profile.InputDeviceId = inputBox.SelectedValue?.ToString();
            fields.Children.Add(CreateProfileField("LOCAS_MenuChooseInput", inputBox, 1));

            var spatialBox = new ComboBox
            {
                ItemsSource = settings.SpatialSoundModeOptions,
                DisplayMemberPath = "Name",
                SelectedValuePath = "Id",
                SelectedValue = profile.SpatialSoundMode ?? string.Empty
            };
            spatialBox.SelectionChanged += (_, __) => profile.SpatialSoundMode = spatialBox.SelectedValue?.ToString();
            fields.Children.Add(CreateProfileField("LOCAS_SpatialSoundTitle", spatialBox, 2));

            var volumeGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            volumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            volumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var volumeEnabled = new CheckBox
            {
                IsChecked = profile.GameVolumePercent.HasValue,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var volumeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Value = profile.GameVolumePercent ?? 50,
                IsEnabled = profile.GameVolumePercent.HasValue,
                VerticalAlignment = VerticalAlignment.Center
            };
            var volumeValue = new TextBlock
            {
                MinWidth = 40,
                Margin = new Thickness(8, 0, 0, 0),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Action updateVolume = () =>
            {
                var enabled = volumeEnabled.IsChecked == true;
                volumeSlider.IsEnabled = enabled;
                profile.GameVolumePercent = enabled ? (int?)Math.Round(volumeSlider.Value) : null;
                volumeValue.Text = enabled ? $"{profile.GameVolumePercent}%" : "-";
            };
            volumeEnabled.Checked += (_, __) => updateVolume();
            volumeEnabled.Unchecked += (_, __) => updateVolume();
            volumeSlider.ValueChanged += (_, __) => updateVolume();
            Grid.SetColumn(volumeSlider, 1);
            Grid.SetColumn(volumeValue, 2);
            volumeGrid.Children.Add(volumeEnabled);
            volumeGrid.Children.Add(volumeSlider);
            volumeGrid.Children.Add(volumeValue);
            updateVolume();
            fields.Children.Add(CreateProfileField("LOCAS_GameVolumeTitle", volumeGrid, 3));

            content.Children.Add(fields);

            var processSection = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            var processLabel = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            processLabel.SetResourceReference(TextBlock.TextProperty, "LOCAS_AudioProcessTitle");
            processSection.Children.Add(processLabel);

            var processGrid = new Grid();
            processGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            processGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            processGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var processBox = new ComboBox
            {
                ItemsSource = settings.GetAudioProcessOptions(profile.AudioProcessName),
                DisplayMemberPath = "DisplayName",
                SelectedValuePath = "ProcessName",
                SelectedValue = profile.AudioProcessName ?? string.Empty,
                Margin = new Thickness(0, 0, 8, 0)
            };
            processBox.SelectionChanged += (_, __) => profile.AudioProcessName = processBox.SelectedValue?.ToString();
            processGrid.Children.Add(processBox);

            var detectProcessButton = CreatePreviewButton("LOCAS_AudioProcessDetect");
            detectProcessButton.MinWidth = 100;
            detectProcessButton.HorizontalAlignment = HorizontalAlignment.Left;
            detectProcessButton.Margin = new Thickness(0, 0, 8, 0);
            detectProcessButton.Click += (_, __) =>
            {
                processBox.ItemsSource = settings.GetAudioProcessOptions(profile.AudioProcessName);
                processBox.SelectedValue = profile.AudioProcessName ?? string.Empty;
                processBox.IsDropDownOpen = true;
            };
            Grid.SetColumn(detectProcessButton, 1);
            processGrid.Children.Add(detectProcessButton);

            var browseProcessButton = CreatePreviewButton("LOCAS_Browse");
            browseProcessButton.MinWidth = 100;
            browseProcessButton.HorizontalAlignment = HorizontalAlignment.Left;
            browseProcessButton.Click += (_, __) =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = ResourceText("LOCAS_AudioProcessBrowseTitle", "Select the game's audio executable"),
                    Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var processName = IoPath.GetFileNameWithoutExtension(dialog.FileName);
                if (string.IsNullOrWhiteSpace(processName))
                {
                    return;
                }

                profile.AudioProcessName = processName;
                processBox.ItemsSource = settings.GetAudioProcessOptions(processName);
                processBox.SelectedValue = processName;
            };
            Grid.SetColumn(browseProcessButton, 2);
            processGrid.Children.Add(browseProcessButton);
            processSection.Children.Add(processGrid);
            var processHelp = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.78,
                Margin = new Thickness(0, 8, 0, 0)
            };
            processHelp.SetResourceReference(TextBlock.TextProperty, "LOCAS_AudioProcessHelp");
            processSection.Children.Add(processHelp);
            content.Children.Add(processSection);

            var layout = new Grid();
            var imageSource = LoadGameProfileImage(profile.GameImagePath);
            if (imageSource != null)
            {
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var image = new Image
                {
                    Source = imageSource,
                    Width = 68,
                    Height = 96,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 14, 0)
                };
                layout.Children.Add(image);
                Grid.SetColumn(content, 1);
            }
            else
            {
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            layout.Children.Add(content);
            container.Child = layout;
            return container;
        }

        private static ImageSource LoadGameProfileImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !IoFile.Exists(path))
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private Button CreatePreviewButton(string contentResource)
        {
            var button = new Button
            {
                Style = TryFindResource("PreviewActionButton") as Style
            };
            button.SetResourceReference(ContentControl.ContentProperty, contentResource);
            return button;
        }

        private static FrameworkElement CreateProfileField(string labelResource, FrameworkElement control, int column)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, column == 3 ? 0 : 10, 0) };
            var label = CreateFieldLabel(labelResource);
            panel.Children.Add(label);
            panel.Children.Add(control);
            Grid.SetColumn(panel, column);
            return panel;
        }

        private static void UpdateDefaultVolume(AudioDevice device, CheckBox enabled, Slider slider, TextBlock value)
        {
            var hasValue = enabled.IsChecked == true;
            slider.IsEnabled = hasValue;
            value.Text = hasValue ? $"{Math.Max(0, Math.Min(100, device.DefaultVolumePercent ?? (int)Math.Round(slider.Value)))}%" : "-";
        }

        private void BrowseSpatialSoundToolPath(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is AudioSwitcherSettings settings))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = TryFindResource("LOCAS_SpatialSoundBrowseTitle") as string ?? "Select SoundVolumeView.exe or svcl.exe",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(settings.SpatialSoundToolPath) && IoFile.Exists(settings.SpatialSoundToolPath))
            {
                dialog.InitialDirectory = IoPath.GetDirectoryName(settings.SpatialSoundToolPath);
                dialog.FileName = IoPath.GetFileName(settings.SpatialSoundToolPath);
            }

            if (dialog.ShowDialog() == true)
            {
                settings.SpatialSoundToolPath = dialog.FileName;
                UpdateSpatialSoundToolStatus();
            }
        }

        private void SpatialSoundToolPathChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSpatialSoundToolStatus();
        }

        private void TestSpatialSoundToolPath(object sender, RoutedEventArgs e)
        {
            var title = TryFindResource("LOCAS_SpatialSoundTitle") as string ?? "Spatial sound";
            MessageBox.Show(GetSpatialSoundToolStatus(), title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportAudioSessionDiagnostics(object sender, RoutedEventArgs e)
        {
            (DataContext as AudioSwitcherSettings)?.ExportAudioSessionDiagnostics();
        }

        private void ExportSettingsBackup(object sender, RoutedEventArgs e)
        {
            (DataContext as AudioSwitcherSettings)?.ExportSettingsBackup();
        }

        private void ImportSettingsBackup(object sender, RoutedEventArgs e)
        {
            (DataContext as AudioSwitcherSettings)?.ImportSettingsBackup();
        }

        private void UpdateSpatialSoundToolStatus()
        {
            if (SpatialSoundToolStatus == null)
            {
                return;
            }

            SpatialSoundToolStatus.Text = GetSpatialSoundToolStatus();
        }

        private string GetSpatialSoundToolStatus()
        {
            var path = (DataContext as AudioSwitcherSettings)?.SpatialSoundToolPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return TryFindResource("LOCAS_SpatialSoundToolStatusEmpty") as string ?? "No Spatial Sound tool selected.";
            }

            if (!IoFile.Exists(path))
            {
                return TryFindResource("LOCAS_SpatialSoundToolStatusMissing") as string ?? "The selected file does not exist.";
            }

            var fileName = IoPath.GetFileName(path);
            if (string.Equals(fileName, "SoundVolumeView.exe", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(TryFindResource("LOCAS_SpatialSoundToolStatusReady") as string ?? "{0} detected.", "SoundVolumeView.exe");
            }

            if (string.Equals(fileName, "svcl.exe", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(TryFindResource("LOCAS_SpatialSoundToolStatusReady") as string ?? "{0} detected.", "svcl.exe");
            }

            return TryFindResource("LOCAS_SpatialSoundToolStatusUnknownExe") as string ??
                   "The selected file does not look like SoundVolumeView.exe or svcl.exe.";
        }

        private void OpenExternalLink(object sender, RequestNavigateEventArgs e)
        {
            OpenExternalUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void OpenExternalButton(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url && !string.IsNullOrWhiteSpace(url))
            {
                OpenExternalUrl(url);
            }
        }

        private static void OpenExternalUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }

        private static string GetInstalledVersion()
        {
            try
            {
                var assemblyPath = typeof(AudioSwitcherSettingsView).Assembly.Location;
                var manifestPath = IoPath.Combine(IoPath.GetDirectoryName(assemblyPath), "extension.yaml");
                if (IoFile.Exists(manifestPath))
                {
                    foreach (var line in IoFile.ReadLines(manifestPath))
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                        {
                            var version = trimmedLine.Substring("Version:".Length).Trim().Trim('\'', '"');
                            if (!string.IsNullOrWhiteSpace(version))
                            {
                                return version;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return typeof(AudioSwitcherSettingsView).Assembly.GetName().Version.ToString(3);
        }

        private static DataTemplate CreateIconTemplate()
        {
            var template = new DataTemplate(typeof(AudioIconOption));

            var panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            panel.SetValue(FrameworkElement.MinHeightProperty, 24d);

            var viewbox = new FrameworkElementFactory(typeof(Viewbox));
            viewbox.SetValue(FrameworkElement.WidthProperty, 20d);
            viewbox.SetValue(FrameworkElement.HeightProperty, 20d);
            viewbox.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            viewbox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            var path = new FrameworkElementFactory(typeof(Path));
            path.SetValue(Path.StretchProperty, Stretch.Uniform);
            path.SetResourceReference(Path.StrokeProperty, "TextBrush");
            path.SetValue(Path.StrokeThicknessProperty, 2d);
            path.SetValue(Path.StrokeStartLineCapProperty, PenLineCap.Round);
            path.SetValue(Path.StrokeEndLineCapProperty, PenLineCap.Round);
            path.SetValue(Path.StrokeLineJoinProperty, PenLineJoin.Round);
            path.SetValue(Path.FillProperty, Brushes.Transparent);
            path.SetBinding(Path.DataProperty, new Binding("GeometryData") { Converter = new IconGeometryConverter() });
            viewbox.AppendChild(path);
            panel.AppendChild(viewbox);

            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            text.SetBinding(TextBlock.TextProperty, new Binding("DisplayName"));
            panel.AppendChild(text);

            template.VisualTree = panel;
            return template;
        }
    }
}
