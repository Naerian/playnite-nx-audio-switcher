using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
        private readonly bool themeStandaloneWindow;
        private ScrollViewer hostScrollViewer;
        private Window hostWindow;
        private Window layoutHostWindow;
        private AudioSwitcherPlugin subscribedPlugin;
        private bool restoringWindowLayout;

        public AudioSwitcherSettingsView() : this(false)
        {
        }

        public AudioSwitcherSettingsView(bool themeStandaloneWindow)
        {
            this.themeStandaloneWindow = themeStandaloneWindow;
            InitializeComponent();
            var iconTemplate = CreateIconTemplate();
            DesktopTopPanelIconBox.ItemTemplate = iconTemplate;
            AboutVersionText.Text = string.Format(
                TryFindResource("LOCAS_VersionAuthorFormat") as string ?? "Audio Switcher {0} · Narian",
                GetInstalledVersion());
            DataContextChanged += (_, __) =>
            {
                SubscribeLiveAudioGraph();
                ApplyAppearancePreset();
                BindAppearancePresetSelector();
                BindNotificationLookSelectors();
                RebuildDeviceRows();
                UpdateOverview();
            };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            ApplyAppearancePreset();
            BindAppearancePresetSelector();
            BindNotificationLookSelectors();
            ApplyPreferredWindowSize();
            AttachWindowLayoutPersistence();
            AttachToHost();
            Dispatcher.BeginInvoke(new Action(AttachToHost), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(AttachToHost), DispatcherPriority.ApplicationIdle);
            Dispatcher.BeginInvoke(new Action(FillSelectedContentHosts), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(FillSelectedContentHosts), DispatcherPriority.ApplicationIdle);
            SubscribeLiveAudioGraph();
            RefreshOnLoad();
        }

        private bool suppressNotificationPresetChange;
        private bool applyingNotificationPreset;
        private AudioNotificationSurface watchedDesktopSurface;
        private AudioNotificationSurface watchedFullscreenSurface;

        private void BindNotificationLookSelectors()
        {
            var settings = DataContext as AudioSwitcherSettings;
            WatchNotificationSurface(ref watchedDesktopSurface, settings?.DesktopNotification);
            WatchNotificationSurface(ref watchedFullscreenSurface, settings?.FullscreenNotification);
            RefreshNotificationPresetSelectors();
        }

        private void RefreshNotificationPresetSelectors()
        {
            if (DesktopNotificationPresetSelector == null || FullscreenNotificationPresetSelector == null)
            {
                return;
            }

            var settings = DataContext as AudioSwitcherSettings;
            suppressNotificationPresetChange = true;
            try
            {
                FillNotificationPresetSelector(DesktopNotificationPresetSelector, settings?.DesktopNotification, true);
                FillNotificationPresetSelector(FullscreenNotificationPresetSelector, settings?.FullscreenNotification, false);
            }
            finally
            {
                suppressNotificationPresetChange = false;
            }
        }

        private void WatchNotificationSurface(ref AudioNotificationSurface watched, AudioNotificationSurface next)
        {
            if (ReferenceEquals(watched, next))
            {
                return;
            }

            if (watched != null)
            {
                watched.PropertyChanged -= NotificationSurface_OnPropertyChanged;
            }

            watched = next;
            if (watched != null)
            {
                watched.PropertyChanged += NotificationSurface_OnPropertyChanged;
            }
        }

        private void UnwatchNotificationSurfaces()
        {
            WatchNotificationSurface(ref watchedDesktopSurface, null);
            WatchNotificationSurface(ref watchedFullscreenSurface, null);
        }

        private void NotificationSurface_OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var surface = sender as AudioNotificationSurface;
            if (surface == null || applyingNotificationPreset)
            {
                return;
            }

            var name = e.PropertyName;
            if (string.Equals(name, nameof(AudioNotificationSurface.StylePreset), StringComparison.Ordinal))
            {
                RefreshNotificationPresetSelectors();
                return;
            }

            if (string.Equals(name, nameof(AudioNotificationSurface.UsePlayniteThemeAppearance), StringComparison.Ordinal))
            {
                AudioThemeLayoutPack.InvalidateCache();
                RefreshNotificationPresetSelectors();
                return;
            }

            if (string.Equals(name, nameof(AudioNotificationSurface.ShowLowBatteryNotifications), StringComparison.Ordinal))
            {
                return;
            }

            // Any manual tweak turns the named look into "Custom" (same behavior as Controller Manager).
            var current = surface.StylePreset ?? string.Empty;
            if (AudioNotificationPresets.IsNamedPluginPreset(current) ||
                current.StartsWith(ImportedVisualProfileCatalog.IdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                surface.StylePreset = AudioNotificationPresets.Custom;
            }
        }

        private void FillNotificationPresetSelector(ComboBox combo, AudioNotificationSurface surface, bool desktop)
        {
            var settings = DataContext as AudioSwitcherSettings;
            var plugin = settings?.Plugin;
            var options = new System.Collections.Generic.List<NotificationPresetOption>();
            var themeActive = plugin != null && plugin.UsesEmbeddedThemeLayout(desktop);
            if (themeActive || (plugin != null && AudioThemeLayoutPack.HasLayout(plugin.PlayniteApi, !desktop)))
            {
                options.Add(new NotificationPresetOption
                {
                    Key = AudioThemeLayoutPack.PlayniteThemeLookKey,
                    DisplayName = plugin == null
                        ? "Playnite theme pack"
                        : AudioThemeLayoutPack.GetDisplayName(plugin.PlayniteApi, !desktop)
                });
            }

            options.Add(CreatePresetOption(AudioNotificationPresets.Custom));
            options.Add(new NotificationPresetOption
            {
                DisplayName = TryFindResource("LOCAS_PresetGroupPlugin") as string ?? "Plugin presets",
                IsHeader = true
            });
            foreach (var preset in AudioNotificationPresets.Named)
            {
                options.Add(CreatePresetOption(preset));
            }

            var imported = ImportedVisualProfileCatalog.GetIds();
            if (imported.Length > 0)
            {
                options.Add(new NotificationPresetOption
                {
                    DisplayName = TryFindResource("LOCAS_ImportedDesigns") as string ?? "Imported designs",
                    IsHeader = true
                });
                foreach (var id in imported)
                {
                    options.Add(new NotificationPresetOption
                    {
                        Key = id,
                        DisplayName = ImportedVisualProfileCatalog.GetName(id),
                        CanDelete = true
                    });
                }
            }

            combo.ItemsSource = options;
            combo.IsEnabled = !themeActive;
            combo.SelectedValue = themeActive
                ? AudioThemeLayoutPack.PlayniteThemeLookKey
                : AudioNotificationPresets.Normalize(surface?.StylePreset);
            var editor = desktop ? DesktopNotificationStyleEditor : FullscreenNotificationStyleEditor;
            if (editor != null)
            {
                editor.IsEnabled = !themeActive;
                editor.Opacity = themeActive ? 0.42 : 1.0;
            }
        }

        private NotificationPresetOption CreatePresetOption(string preset)
        {
            var key = AudioNotificationPresets.LocKey(preset);
            return new NotificationPresetOption
            {
                Key = preset,
                DisplayName = (key == null ? null : TryFindResource(key) as string) ?? preset
            };
        }

        internal sealed class NotificationPresetOption
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public bool IsHeader { get; set; }
            public bool CanDelete { get; set; }
        }

        private void NotificationPresetSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressNotificationPresetChange)
            {
                return;
            }

            var settings = DataContext as AudioSwitcherSettings;
            var combo = sender as ComboBox;
            var preset = combo?.SelectedValue as string;
            if (settings == null || string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            var desktop = ReferenceEquals(combo, DesktopNotificationPresetSelector);
            if (string.Equals(preset, AudioThemeLayoutPack.PlayniteThemeLookKey, StringComparison.OrdinalIgnoreCase))
            {
                var surface = desktop ? settings.DesktopNotification : settings.FullscreenNotification;
                if (surface != null)
                {
                    surface.UsePlayniteThemeAppearance = true;
                }

                RefreshNotificationPresetSelectors();
                return;
            }

            if (settings.Plugin != null && settings.Plugin.UsesEmbeddedThemeLayout(desktop))
            {
                RefreshNotificationPresetSelectors();
                return;
            }

            if (ImportedVisualProfileCatalog.Contains(preset))
            {
                applyingNotificationPreset = true;
                try
                {
                    settings.Plugin?.ApplyImportedVisualProfile(settings, preset, null);
                }
                finally
                {
                    applyingNotificationPreset = false;
                }

                RefreshNotificationPresetSelectors();
                return;
            }

            var target = desktop ? settings.DesktopNotification : settings.FullscreenNotification;
            applyingNotificationPreset = true;
            try
            {
                if (target != null)
                {
                    target.UsePlayniteThemeAppearance = false;
                }

                AudioNotificationPresets.Apply(target, preset, desktop);
            }
            finally
            {
                applyingNotificationPreset = false;
            }
        }

        private void PreviewNotificationClick(object sender, RoutedEventArgs e)
        {
            var settings = DataContext as AudioSwitcherSettings;
            var desktop = string.Equals((sender as FrameworkElement)?.Tag as string, "Desktop", StringComparison.Ordinal);
            settings?.Plugin?.PreviewLowBatteryNotification(desktop);
        }

        private void CopyNotificationStyleClick(object sender, RoutedEventArgs e)
        {
            var settings = DataContext as AudioSwitcherSettings;
            if (settings?.DesktopNotification == null || settings.FullscreenNotification == null)
            {
                return;
            }

            var tag = (sender as FrameworkElement)?.Tag as string;
            var toDesktop = string.Equals(tag, "Desktop", StringComparison.Ordinal);
            var confirmKey = toDesktop
                ? "LOCAS_CopyFullscreenStyleConfirm"
                : "LOCAS_CopyDesktopStyleConfirm";
            var message = TryFindResource(confirmKey) as string ?? confirmKey;
            if (MessageBox.Show(message,
                    TryFindResource("LOCAS_CopyNotificationStyle") as string ?? "Copy design",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            applyingNotificationPreset = true;
            try
            {
                if (toDesktop)
                {
                    var theme = settings.DesktopNotification.UsePlayniteThemeAppearance;
                    var alerts = settings.DesktopNotification.ShowLowBatteryNotifications;
                    settings.DesktopNotification.CopyFrom(settings.FullscreenNotification);
                    settings.DesktopNotification.UsePlayniteThemeAppearance = theme;
                    settings.DesktopNotification.ShowLowBatteryNotifications = alerts;
                }
                else
                {
                    var theme = settings.FullscreenNotification.UsePlayniteThemeAppearance;
                    var alerts = settings.FullscreenNotification.ShowLowBatteryNotifications;
                    settings.FullscreenNotification.CopyFrom(settings.DesktopNotification);
                    settings.FullscreenNotification.UsePlayniteThemeAppearance = theme;
                    settings.FullscreenNotification.ShowLowBatteryNotifications = alerts;
                }
            }
            finally
            {
                applyingNotificationPreset = false;
            }

            RefreshNotificationPresetSelectors();
        }

        private void ExportVisualProfileClick(object sender, RoutedEventArgs e)
        {
            var settings = DataContext as AudioSwitcherSettings;
            settings?.Plugin?.ExportVisualProfile(settings);
        }

        private void ImportVisualProfileClick(object sender, RoutedEventArgs e)
        {
            var settings = DataContext as AudioSwitcherSettings;
            settings?.Plugin?.ImportVisualProfile(settings, RefreshNotificationPresetSelectors);
        }

        private void DeleteImportedVisualProfileClick(object sender, RoutedEventArgs e)
        {
            var settings = DataContext as AudioSwitcherSettings;
            var profileId = (sender as FrameworkElement)?.Tag as string;
            if (settings?.Plugin == null || string.IsNullOrWhiteSpace(profileId))
            {
                return;
            }

            if (settings.Plugin.DeleteImportedVisualProfile(settings, profileId))
            {
                RefreshNotificationPresetSelectors();
            }
        }

        private void SelectColorClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var surface = button?.DataContext as AudioNotificationSurface;
            var propertyName = button?.Tag as string;
            var property = string.IsNullOrWhiteSpace(propertyName) || surface == null
                ? null
                : typeof(AudioNotificationSurface).GetProperty(propertyName);
            if (property == null || property.PropertyType != typeof(string))
            {
                return;
            }

            Color current;
            try
            {
                current = (Color)ColorConverter.ConvertFromString(property.GetValue(surface, null) as string);
            }
            catch
            {
                current = Colors.White;
            }

            var settings = DataContext as AudioSwitcherSettings;
            var dialog = new ColorPickerDialog(current, key => TryFindResource(key) as string ?? key);
            var owner = Window.GetWindow(this);
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            SettingsAppearance.ApplyWindow(dialog, settings != null ? settings.AppearancePreset : SettingsAppearance.Default);
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var selected = dialog.SelectedColor;
            property.SetValue(surface, ColorPickerMath.ToHex(selected.A, selected.R, selected.G, selected.B), null);
        }

        private void SelectNotificationBackgroundImageClick(object sender, RoutedEventArgs e)
        {
            var surface = GetSurfaceForTag(sender);
            if (surface == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif|All files|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            surface.BackgroundImagePath = dialog.FileName;
            surface.UseBackgroundImage = true;
        }

        private void ClearNotificationBackgroundImageClick(object sender, RoutedEventArgs e)
        {
            var surface = GetSurfaceForTag(sender);
            if (surface == null)
            {
                return;
            }

            surface.BackgroundImagePath = string.Empty;
            surface.UseBackgroundImage = false;
        }

        private AudioNotificationSurface GetSurfaceForTag(object sender)
        {
            var settings = DataContext as AudioSwitcherSettings;
            if (settings == null)
            {
                return null;
            }

            var tag = (sender as FrameworkElement)?.Tag as string;
            return string.Equals(tag, "Desktop", StringComparison.Ordinal)
                ? settings.DesktopNotification
                : settings.FullscreenNotification;
        }

        private bool suppressAppearancePresetChange;

        private void ApplyAppearancePreset()
        {
            var settings = DataContext as AudioSwitcherSettings;
            var preset = settings != null
                ? settings.AppearancePreset
                : SettingsAppearance.Default;
            SettingsAppearance.Apply(this, preset);

            if (themeStandaloneWindow)
            {
                SettingsAppearance.ApplyWindow(Window.GetWindow(this), preset);
            }

            SyncAppearancePresetSelector(preset);
        }

        private void BindAppearancePresetSelector()
        {
            if (AppearancePresetSelector == null)
            {
                return;
            }

            var settings = DataContext as AudioSwitcherSettings;
            suppressAppearancePresetChange = true;
            try
            {
                AppearancePresetSelector.ItemsSource = settings != null
                    ? settings.AppearancePresetOptions
                    : null;
                SyncAppearancePresetSelector(settings != null
                    ? settings.AppearancePreset
                    : SettingsAppearance.Default);
            }
            finally
            {
                suppressAppearancePresetChange = false;
            }
        }

        private void SyncAppearancePresetSelector(string preset)
        {
            if (AppearancePresetSelector == null)
            {
                return;
            }

            var normalized = SettingsAppearance.Normalize(preset);
            if (Equals(AppearancePresetSelector.SelectedValue, normalized))
            {
                return;
            }

            suppressAppearancePresetChange = true;
            try
            {
                AppearancePresetSelector.SelectedValue = normalized;
            }
            finally
            {
                suppressAppearancePresetChange = false;
            }
        }

        private void AppearancePresetSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressAppearancePresetChange)
            {
                return;
            }

            var settings = DataContext as AudioSwitcherSettings;
            var preset = AppearancePresetSelector?.SelectedValue as string;
            if (settings == null || string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            settings.AppearancePreset = preset;
            ApplyAppearancePreset();
        }

        private void SubscribeLiveAudioGraph()
        {
            UnsubscribeLiveAudioGraph();
            if (DataContext is AudioSwitcherSettings settings && settings.Plugin != null)
            {
                subscribedPlugin = settings.Plugin;
                subscribedPlugin.LiveAudioGraphChanged += OnLiveAudioGraphChanged;
            }
        }

        private void UnsubscribeLiveAudioGraph()
        {
            if (subscribedPlugin != null)
            {
                subscribedPlugin.LiveAudioGraphChanged -= OnLiveAudioGraphChanged;
                subscribedPlugin = null;
            }
        }

        private void OnLiveAudioGraphChanged(object sender, EventArgs args)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateOverview));
                return;
            }

            UpdateOverview();
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
            PersistWindowLayout();
            DetachWindowLayoutPersistence();
            UnsubscribeLiveAudioGraph();
            UnwatchNotificationSurfaces();
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

        private void ExpanderChevronButton_OnClick(object sender, RoutedEventArgs e)
        {
            for (var parent = VisualTreeHelper.GetParent(sender as DependencyObject);
                 parent != null;
                 parent = VisualTreeHelper.GetParent(parent))
            {
                var expander = parent as Expander;
                if (expander == null)
                {
                    continue;
                }

                expander.IsExpanded = !expander.IsExpanded;
                e.Handled = true;
                return;
            }
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

            restoringWindowLayout = true;
            try
            {
                window.SizeToContent = SizeToContent.Manual;
                if (window.MinWidth < 1000)
                {
                    window.MinWidth = 1000;
                }
                if (window.MinHeight < 700)
                {
                    window.MinHeight = 700;
                }

                var settings = DataContext as AudioSwitcherSettings;
                var savedWidth = settings != null ? settings.SettingsWindowWidth : 0;
                var savedHeight = settings != null ? settings.SettingsWindowHeight : 0;
                var savedMaximized = settings != null && settings.SettingsWindowMaximized;

                if (savedWidth >= 1000)
                {
                    window.Width = savedWidth;
                }
                else if (window.ActualWidth < 1100 && window.Width < 1100)
                {
                    window.Width = 1100;
                }

                if (savedHeight >= 700)
                {
                    window.Height = savedHeight;
                }
                else if (window.ActualHeight < 780 && window.Height < 780)
                {
                    window.Height = 780;
                }

                if (savedMaximized)
                {
                    window.WindowState = WindowState.Maximized;
                }
            }
            finally
            {
                restoringWindowLayout = false;
            }
        }

        private void AttachWindowLayoutPersistence()
        {
            DetachWindowLayoutPersistence();
            layoutHostWindow = Window.GetWindow(this);
            if (layoutHostWindow == null)
            {
                return;
            }

            layoutHostWindow.Closing += OnLayoutHostWindowClosing;
            layoutHostWindow.StateChanged += OnLayoutHostWindowStateChanged;
        }

        private void DetachWindowLayoutPersistence()
        {
            if (layoutHostWindow == null)
            {
                return;
            }

            layoutHostWindow.Closing -= OnLayoutHostWindowClosing;
            layoutHostWindow.StateChanged -= OnLayoutHostWindowStateChanged;
            layoutHostWindow = null;
        }

        private void OnLayoutHostWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PersistWindowLayout();
        }

        private void OnLayoutHostWindowStateChanged(object sender, EventArgs e)
        {
            if (restoringWindowLayout)
            {
                return;
            }

            PersistWindowLayout();
        }

        private void PersistWindowLayout()
        {
            var window = layoutHostWindow ?? Window.GetWindow(this);
            var settings = DataContext as AudioSwitcherSettings;
            if (window == null || settings == null || window.WindowState == WindowState.Minimized)
            {
                return;
            }

            double width;
            double height;
            var maximized = window.WindowState == WindowState.Maximized;
            if (maximized)
            {
                var restore = window.RestoreBounds;
                width = restore.Width > 0 ? restore.Width : window.Width;
                height = restore.Height > 0 ? restore.Height : window.Height;
            }
            else
            {
                width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
                height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
            }

            if (width < 1000 || height < 700)
            {
                return;
            }

            if (Math.Abs(settings.SettingsWindowWidth - width) < 0.5 &&
                Math.Abs(settings.SettingsWindowHeight - height) < 0.5 &&
                settings.SettingsWindowMaximized == maximized)
            {
                return;
            }

            settings.PersistSettingsWindowLayout(width, height, maximized);
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
            if (!spatialEnabled)
            {
                ApplyStatusBadgeAppearance(OverviewSpatialSoundPill, "GlyphBrush", 0.65);
            }
            else if (IsSpatialSoundToolReady())
            {
                ApplyStatusBadgeAppearance(OverviewSpatialSoundPill, "PositiveRatingBrush");
            }
            else
            {
                ApplyStatusBadgeAppearance(OverviewSpatialSoundPill, "WarningBrush");
            }

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

            BuildDeviceRows(DeviceRowsPanel, settings.AvailablePlaybackDevices, settings, "LOCAS_DefaultVolume", true);
            BuildDeviceRows(InputDeviceRowsPanel, settings.AvailableRecordingDevices, settings, "LOCAS_DefaultInputVolume", false);
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

        private void BuildDeviceRows(
            StackPanel panel,
            IEnumerable<AudioDevice> devices,
            AudioSwitcherSettings settings,
            string defaultVolumeLabelResource,
            bool showQuickSwitchOption)
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
                    Style = TryFindResource("HintText") as Style
                };
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
                    index,
                    showQuickSwitchOption));
            }

            panel.Children.Add(grid);
        }

        private UIElement CreateDeviceCard(
            AudioDevice device,
            AudioSwitcherSettings settings,
            string defaultVolumeLabelResource,
            int index,
            bool showQuickSwitchOption)
        {
            var card = new Border
            {
                Style = TryFindResource("DeviceCard") as Style ?? TryFindResource("SummaryCard") as Style,
                Margin = new Thickness(index % 2 == 0 ? 0 : 8, 0, index % 2 == 0 ? 8 : 0, 16),
                MinHeight = 0
            };

            var root = new StackPanel();
            var title = new TextBlock
            {
                Style = TryFindResource("SummaryCardTitle") as Style,
                TextWrapping = TextWrapping.Wrap,
                Text = ResolveDeviceCardTitle(device)
            };
            root.Children.Add(new Border
            {
                Style = TryFindResource("SummaryTitleSeparator") as Style,
                Child = title
            });

            var pills = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            pills.Children.Add(CreateStatusPill(device));
            pills.Children.Add(CreateMetricPill(
                ResourceText("LOCAS_Battery", "Battery"),
                device.HasBattery ? device.BatteryLabel : "\u2014",
                BatteryColorPalette.GetBrush(device)));
            root.Children.Add(pills);

            var visiblePanel = new StackPanel();
            var visibleBox = new CheckBox
            {
                IsChecked = device.IsVisible,
                VerticalAlignment = VerticalAlignment.Center
            };
            visibleBox.SetResourceReference(ContentControl.ContentProperty, "LOCAS_Visible");
            visibleBox.Checked += (_, __) => device.IsVisible = true;
            visibleBox.Unchecked += (_, __) => device.IsVisible = false;
            visiblePanel.Children.Add(visibleBox);
            var visibleHelp = new TextBlock
            {
                Style = TryFindResource("IndentedHintText") as Style ?? TryFindResource("HintText") as Style
            };
            visibleHelp.SetResourceReference(TextBlock.TextProperty, "LOCAS_VisibleHelp");
            visiblePanel.Children.Add(visibleHelp);
            root.Children.Add(visiblePanel);

            if (showQuickSwitchOption)
            {
                var quickSwitchPanel = new StackPanel();
                var quickSwitchBox = new CheckBox
                {
                    IsChecked = ResolveIncludeInQuickSwitch(device),
                    VerticalAlignment = VerticalAlignment.Center
                };
                quickSwitchBox.SetResourceReference(ContentControl.ContentProperty, "LOCAS_IncludeInQuickSwitch");
                quickSwitchBox.Checked += (_, __) => device.IncludeInQuickSwitch = true;
                quickSwitchBox.Unchecked += (_, __) => device.IncludeInQuickSwitch = false;
                quickSwitchPanel.Children.Add(quickSwitchBox);
                var quickSwitchHelp = new TextBlock
                {
                    Style = TryFindResource("IndentedHintText") as Style ?? TryFindResource("HintText") as Style
                };
                quickSwitchHelp.SetResourceReference(TextBlock.TextProperty, "LOCAS_IncludeInQuickSwitchHelp");
                quickSwitchPanel.Children.Add(quickSwitchHelp);
                root.Children.Add(quickSwitchPanel);
            }

            var iconPanel = new StackPanel();
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
            var iconHelp = new TextBlock
            {
                Style = TryFindResource("HintText") as Style
            };
            iconHelp.SetResourceReference(TextBlock.TextProperty, "LOCAS_IconHelp");
            iconPanel.Children.Add(iconHelp);
            root.Children.Add(iconPanel);

            var defaultVolumePanel = new StackPanel();
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
            var defaultVolumeHelp = new TextBlock
            {
                Style = TryFindResource("HintText") as Style
            };
            defaultVolumeHelp.SetResourceReference(TextBlock.TextProperty, "LOCAS_DefaultVolumeHelp");
            defaultVolumePanel.Children.Add(defaultVolumeHelp);
            root.Children.Add(defaultVolumePanel);

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
            root.Children.Add(customNamePanel);

            card.Child = root;
            return card;
        }

        private static bool ResolveIncludeInQuickSwitch(AudioDevice device)
        {
            if (device == null)
            {
                return false;
            }

            if (device.IncludeInQuickSwitch.HasValue)
            {
                return device.IncludeInQuickSwitch.Value;
            }

            return !string.IsNullOrWhiteSpace(AudioSwitcherSettings.SanitizeCustomName(device.CustomName));
        }

        private static string ResolveDeviceCardTitle(AudioDevice device)
        {
            if (device == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(device.Name))
            {
                return device.Name;
            }

            if (!string.IsNullOrWhiteSpace(device.SettingsDisplayName))
            {
                return device.SettingsDisplayName;
            }

            return device.Id ?? string.Empty;
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

        private Border CreateStatusPill(AudioDevice device)
        {
            var status = string.IsNullOrWhiteSpace(device.StatusDisplayName)
                ? "\u2014"
                : device.StatusDisplayName;
            var text = new TextBlock
            {
                Text = $"{ResourceText("LOCAS_Status", "Status")}: {status}"
            };
            var badge = new Border
            {
                Style = TryFindResource("DeviceStatusPill") as Style,
                Child = text
            };

            var brushKey = "GlyphBrush";
            var opacity = 0.65;
            if (device.State == AudioEndpointState.Active)
            {
                brushKey = "PositiveRatingBrush";
                opacity = 1.0;
            }
            else if (device.State == AudioEndpointState.Disabled)
            {
                brushKey = "WarningBrush";
                opacity = 1.0;
            }

            ApplyStatusBadgeAppearance(text, brushKey, opacity);
            return badge;
        }

        private static void ApplyStatusBadgeAppearance(TextBlock textBlock, string brushKey, double opacity = 1.0)
        {
            if (textBlock == null || string.IsNullOrWhiteSpace(brushKey))
            {
                return;
            }

            textBlock.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            textBlock.Opacity = 1.0;

            var badge = textBlock.Parent as Border;
            if (badge == null)
            {
                for (var parent = VisualTreeHelper.GetParent(textBlock);
                     parent != null;
                     parent = VisualTreeHelper.GetParent(parent))
                {
                    badge = parent as Border;
                    if (badge != null)
                    {
                        break;
                    }
                }
            }

            if (badge == null)
            {
                return;
            }

            badge.BorderThickness = new Thickness(0);
            badge.BorderBrush = Brushes.Transparent;
            badge.Effect = null;
            badge.Opacity = opacity;

            string backgroundKey;
            if (string.Equals(brushKey, "PositiveRatingBrush", StringComparison.Ordinal))
            {
                backgroundKey = "Narian.BadgeSuccessBg";
            }
            else if (string.Equals(brushKey, "WarningBrush", StringComparison.Ordinal))
            {
                backgroundKey = "Narian.BadgeWarningBg";
            }
            else
            {
                backgroundKey = "Narian.BadgeMutedBg";
            }

            badge.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        }

        private void GameProfilesSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildGameProfileRows();
        }

        private void RebuildGameProfileRows()
        {
            if (GameProfileRowsPanel == null || NoGameProfilesText == null || !(DataContext is AudioSwitcherSettings settings))
            {
                return;
            }

            GameProfileRowsPanel.Children.Clear();
            var query = GameProfilesSearchBox?.Text?.Trim() ?? string.Empty;
            var profiles = settings.AvailableGameProfiles
                .OrderBy(profile => profile.GameName)
                .Where(profile =>
                    string.IsNullOrWhiteSpace(query) ||
                    (!string.IsNullOrWhiteSpace(profile.GameName) &&
                     profile.GameName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0))
                .ToList();

            if (settings.AvailableGameProfiles.Count == 0)
            {
                NoGameProfilesText.Text = ResourceText("LOCAS_NoConfiguredGameProfiles", "No game profiles are configured.");
                NoGameProfilesText.Visibility = Visibility.Visible;
            }
            else if (profiles.Count == 0)
            {
                NoGameProfilesText.Text = ResourceText("LOCAS_NoMatchingGameProfiles", "No game profiles match this search.");
                NoGameProfilesText.Visibility = Visibility.Visible;
            }
            else
            {
                NoGameProfilesText.Visibility = Visibility.Collapsed;
            }

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

            var titleText = new TextBlock
            {
                Text = profile.GameName,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            var removeButton = CreatePreviewButton("LOCAS_RemoveProfile");
            removeButton.MinWidth = 90;
            removeButton.HorizontalAlignment = HorizontalAlignment.Right;
            removeButton.VerticalAlignment = VerticalAlignment.Center;
            removeButton.Margin = new Thickness(12, 0, 0, 0);
            removeButton.Click += (_, __) =>
            {
                var confirmed = settings.Plugin != null
                    ? settings.Plugin.ConfirmRemoveGameProfile(profile.GameName, false)
                    : MessageBox.Show(
                        string.Format(
                            ResourceText("LOCAS_ConfirmRemoveProfilePendingMessage", "Remove the Audio Switcher profile for \"{0}\" when settings are saved? If you cancel, the profile is kept."),
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

            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(titleText);
            Grid.SetColumn(removeButton, 1);
            header.Children.Add(removeButton);

            var fields = new Grid { Margin = new Thickness(0, 0, 0, 0) };
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
                Style = TryFindResource("HintText") as Style
            };
            processHelp.SetResourceReference(TextBlock.TextProperty, "LOCAS_AudioProcessHelp");
            processSection.Children.Add(processHelp);

            var layout = new StackPanel();
            layout.Children.Add(header);
            layout.Children.Add(fields);
            layout.Children.Add(processSection);

            container.Child = layout;
            return container;
        }

        private Button CreatePreviewButton(string contentResource)
        {
            var button = new Button();
            button.SetResourceReference(ContentControl.ContentProperty, contentResource);
            return button;
        }

        private static FrameworkElement CreateProfileField(string labelResource, FrameworkElement control, int column)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, column == 3 ? 0 : 8, 0) };
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

        private void OpenSetupWizardClick(object sender, RoutedEventArgs e)
        {
            (DataContext as AudioSwitcherSettings)?.Plugin?.OpenSetupWizard();
        }

        private void OpenSupportLogFile_OnClick(object sender, RoutedEventArgs e)
        {
            var settings = DataContext as AudioSwitcherSettings;
            var plugin = settings?.Plugin;
            if (plugin == null)
            {
                return;
            }

            if (!plugin.TryOpenSupportLogFile(out var error))
            {
                MessageBox.Show(
                    (TryFindResource("LOCAS_DebugLogOpenFailed") as string ?? "Could not open the debug log.") +
                    (string.IsNullOrWhiteSpace(error) ? string.Empty : "\n\n" + error),
                    TryFindResource("LOCAS_PluginName") as string ?? "Audio Switcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ClearSupportLog_OnClick(object sender, RoutedEventArgs e)
        {
            var settings = DataContext as AudioSwitcherSettings;
            var plugin = settings?.Plugin;
            if (plugin == null)
            {
                return;
            }

            var title = TryFindResource("LOCAS_PluginName") as string ?? "Audio Switcher";
            var confirm = MessageBox.Show(
                TryFindResource("LOCAS_DebugLogClearConfirm") as string
                    ?? "Clear the debug log? This cannot be undone.",
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            if (plugin.TryClearSupportLog(out var error))
            {
                MessageBox.Show(
                    TryFindResource("LOCAS_DebugLogCleared") as string ?? "Debug log cleared.",
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    (TryFindResource("LOCAS_DebugLogClearFailed") as string ?? "Could not clear the debug log.") +
                    (string.IsNullOrWhiteSpace(error) ? string.Empty : "\n\n" + error),
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
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

        private bool IsSpatialSoundToolReady()
        {
            var path = (DataContext as AudioSwitcherSettings)?.SpatialSoundToolPath;
            if (string.IsNullOrWhiteSpace(path) || !IoFile.Exists(path))
            {
                return false;
            }

            var fileName = IoPath.GetFileName(path);
            return string.Equals(fileName, "SoundVolumeView.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "svcl.exe", StringComparison.OrdinalIgnoreCase);
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
