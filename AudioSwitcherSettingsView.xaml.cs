using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Navigation;
using Microsoft.Win32;
using IoFile = System.IO.File;
using IoPath = System.IO.Path;

namespace PlayniteAudioSwitcher
{
    public partial class AudioSwitcherSettingsView : UserControl
    {
        public AudioSwitcherSettingsView()
        {
            InitializeComponent();
            AboutVersionText.Text = string.Format(
                TryFindResource("LOCAS_VersionAuthorFormat") as string ?? "Version {0} | Narian",
                GetInstalledVersion());
            DataContextChanged += (_, __) => RebuildDeviceRows();
            Loaded += (_, __) =>
            {
                RebuildDeviceRows();
                UpdateSpatialSoundToolStatus();
            };
        }

        private void RebuildDeviceRows()
        {
            if (DeviceRowsPanel == null || InputDeviceRowsPanel == null || !(DataContext is AudioSwitcherSettings settings))
            {
                return;
            }

            BuildDeviceRows(DeviceRowsPanel, settings.AvailablePlaybackDevices, settings, "LOCAS_DefaultVolume");
            BuildDeviceRows(InputDeviceRowsPanel, settings.AvailableRecordingDevices, settings, "LOCAS_DefaultInputVolume");
        }

        private void BuildDeviceRows(StackPanel panel, IEnumerable<AudioDevice> devices, AudioSwitcherSettings settings, string defaultVolumeLabelResource)
        {
            panel.Children.Clear();

            foreach (var device in devices)
            {
                panel.Children.Add(CreateDeviceRow(device, settings, defaultVolumeLabelResource));
            }
        }

        private static UIElement CreateDeviceRow(AudioDevice device, AudioSwitcherSettings settings, string defaultVolumeLabelResource)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 0, 0, 12)
            };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

                var namePanel = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
                var windowsName = new TextBlock
                {
                    FontSize = 11,
                    Opacity = 0.7,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                windowsName.SetResourceReference(TextBlock.TextProperty, "LOCAS_WindowsName");
                namePanel.Children.Add(windowsName);
                var deviceName = new TextBlock
                {
                    Text = device.SettingsDisplayName,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                };
                namePanel.Children.Add(deviceName);
                Grid.SetColumn(namePanel, 0);
                grid.Children.Add(namePanel);

                var visiblePanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
                var visibleLabel = new TextBlock
                {
                    FontSize = 11,
                    Opacity = 0.8,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                visibleLabel.SetResourceReference(TextBlock.TextProperty, "LOCAS_Visible");
                var visibleBox = new CheckBox
                {
                    IsChecked = device.IsVisible,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                visibleBox.Checked += (_, __) => device.IsVisible = true;
                visibleBox.Unchecked += (_, __) => device.IsVisible = false;
                visiblePanel.Children.Add(visibleLabel);
                visiblePanel.Children.Add(visibleBox);
                Grid.SetColumn(visiblePanel, 1);
                grid.Children.Add(visiblePanel);

                var iconPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
                var iconLabel = new TextBlock
                {
                    FontSize = 11,
                    Opacity = 0.8,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                iconLabel.SetResourceReference(TextBlock.TextProperty, "LOCAS_Icon");
                var iconBox = new ComboBox
                {
                    ItemsSource = settings.IconOptions,
                    SelectedValuePath = "Id",
                    SelectedValue = device.Icon ?? string.Empty,
                    ItemTemplate = CreateIconTemplate()
                };
                iconBox.SelectionChanged += (_, __) =>
                {
                    device.Icon = iconBox.SelectedValue?.ToString();
                    device.IsIconSuggested = false;
                };
                iconPanel.Children.Add(iconLabel);
                iconPanel.Children.Add(iconBox);
                Grid.SetColumn(iconPanel, 2);
                grid.Children.Add(iconPanel);

                var defaultVolumePanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
                var defaultVolumeLabel = new TextBlock
                {
                    FontSize = 11,
                    Opacity = 0.8,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                defaultVolumeLabel.SetResourceReference(TextBlock.TextProperty, defaultVolumeLabelResource);
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
                defaultVolumePanel.Children.Add(defaultVolumeLabel);
                defaultVolumePanel.Children.Add(defaultVolumeGrid);
                Grid.SetColumn(defaultVolumePanel, 3);
                grid.Children.Add(defaultVolumePanel);

                var customNamePanel = new StackPanel();
                var customNameLabel = new TextBlock
                {
                    FontSize = 11,
                    Opacity = 0.8,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                customNameLabel.SetResourceReference(TextBlock.TextProperty, "LOCAS_PlayniteName");
                var customNameBox = new TextBox
                {
                    Text = device.CustomName ?? string.Empty
                };
                customNameBox.TextChanged += (_, __) => device.CustomName = customNameBox.Text;
                customNamePanel.Children.Add(customNameLabel);
                customNamePanel.Children.Add(customNameBox);
                Grid.SetColumn(customNamePanel, 4);
                grid.Children.Add(customNamePanel);

                border.Child = grid;
                var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                row.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
                row.Children.Add(border);

                var separator = new Line
                {
                    X1 = 0,
                    X2 = 1,
                    Stretch = Stretch.Fill,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 3 },
                    Opacity = 0.35
                };
                Grid.SetRow(separator, 1);
                row.Children.Add(separator);

                return row;
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
            path.SetValue(Path.StrokeProperty, Brushes.Gray);
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
            text.SetBinding(TextBlock.TextProperty, new Binding("DisplayName"));
            panel.AppendChild(text);

            template.VisualTree = panel;
            return template;
        }
    }
}
