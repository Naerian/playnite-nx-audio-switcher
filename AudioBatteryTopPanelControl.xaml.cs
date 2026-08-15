using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioBatteryTopPanelControl : PluginUserControl
    {
        private const double CompactTopPanelWidth = 58d;
        private readonly AudioSwitcherPlugin plugin;
        private FrameworkElement topPanelContainer;

        public static readonly DependencyProperty IsCompactTopPanelLayoutProperty = DependencyProperty.Register(
            nameof(IsCompactTopPanelLayout),
            typeof(bool),
            typeof(AudioBatteryTopPanelControl),
            new PropertyMetadata(false));

        public bool IsCompactTopPanelLayout
        {
            get => (bool)GetValue(IsCompactTopPanelLayoutProperty);
            private set => SetValue(IsCompactTopPanelLayoutProperty, value);
        }

        public AudioBatteryTopPanelControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            DataContext = plugin.Theme;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            plugin.Theme.Refresh();
            Dispatcher.BeginInvoke(new Action(AttachTopPanelContainer), DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (topPanelContainer != null)
            {
                topPanelContainer.SizeChanged -= OnTopPanelContainerSizeChanged;
                topPanelContainer = null;
            }
        }

        private void AttachTopPanelContainer()
        {
            var container = FindTopPanelContainer(this);
            if (ReferenceEquals(topPanelContainer, container))
            {
                UpdateCompactLayout();
                return;
            }

            if (topPanelContainer != null)
            {
                topPanelContainer.SizeChanged -= OnTopPanelContainerSizeChanged;
            }

            topPanelContainer = container;
            if (topPanelContainer != null)
            {
                topPanelContainer.SizeChanged += OnTopPanelContainerSizeChanged;
            }

            UpdateCompactLayout();
        }

        private void OnTopPanelContainerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCompactLayout();
        }

        private void UpdateCompactLayout()
        {
            if (topPanelContainer == null)
            {
                IsCompactTopPanelLayout = false;
                return;
            }

            var availableWidth = !double.IsNaN(topPanelContainer.Width) && topPanelContainer.Width > 0
                ? topPanelContainer.Width
                : topPanelContainer.ActualWidth;
            IsCompactTopPanelLayout = availableWidth > 0 && availableWidth < CompactTopPanelWidth;
        }

        private static FrameworkElement FindTopPanelContainer(DependencyObject child)
        {
            var current = child;
            while (current != null)
            {
                if (current is FrameworkElement element &&
                    string.Equals(element.GetType().Name, "TopPanelItem", StringComparison.Ordinal))
                {
                    return element;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
