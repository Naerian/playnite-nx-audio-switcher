using System.Windows;
using System.Windows.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioSwitcherSettingsView : UserControl
    {
        public AudioSwitcherSettingsView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => RebuildDeviceRows();
            Loaded += (_, __) => RebuildDeviceRows();
        }

        private void RebuildDeviceRows()
        {
            if (DeviceRowsPanel == null || !(DataContext is AudioSwitcherSettings settings))
            {
                return;
            }

            DeviceRowsPanel.Children.Clear();
            foreach (var device in settings.AvailablePlaybackDevices)
            {
                var border = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.Gray,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(0, 0, 0, 12),
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });

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
                    DisplayMemberPath = "DisplayName",
                    SelectedValuePath = "Id",
                    SelectedValue = device.Icon ?? string.Empty
                };
                iconBox.SelectionChanged += (_, __) => device.Icon = iconBox.SelectedValue?.ToString();
                iconPanel.Children.Add(iconLabel);
                iconPanel.Children.Add(iconBox);
                Grid.SetColumn(iconPanel, 1);
                grid.Children.Add(iconPanel);

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
                Grid.SetColumn(customNamePanel, 2);
                grid.Children.Add(customNamePanel);

                border.Child = grid;
                DeviceRowsPanel.Children.Add(border);
            }
        }
    }
}
