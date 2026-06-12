using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

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
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0, 0, 0, 12)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
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
                iconBox.SelectionChanged += (_, __) => device.Icon = iconBox.SelectedValue?.ToString();
                iconPanel.Children.Add(iconLabel);
                iconPanel.Children.Add(iconBox);
                Grid.SetColumn(iconPanel, 2);
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
                Grid.SetColumn(customNamePanel, 3);
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

                DeviceRowsPanel.Children.Add(row);
            }
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
