using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioMediaMixerControl : PluginUserControl
    {
        private static readonly Dictionary<string, ImageSource> IconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly AudioSwitcherPlugin plugin;
        private bool isRefreshing;

        public AudioMediaMixerControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioMediaMixerControl_Loaded;
        }

        private void AudioMediaMixerControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            isRefreshing = true;
            try
            {
                MixerRowsPanel.Children.Clear();
                var sessions = plugin.GetMediaAudioSessions();
                if (sessions.Count == 0)
                {
                    MixerRowsPanel.Children.Add(new TextBlock
                    {
                        Text = plugin.Loc("LOCAS_MediaSessionUnavailable"),
                        Foreground = TryFindResource("TextBrush") as Brush,
                        Opacity = 0.75,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                    return;
                }

                foreach (var session in sessions)
                {
                    MixerRowsPanel.Children.Add(CreateRow(session));
                }
            }
            finally
            {
                isRefreshing = false;
            }
        }

        private UIElement CreateRow(AudioSessionInfo session)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 12),
                Tag = session.Id
            };

            var appIcon = plugin.Settings.ShowMediaSessionIcons ? TryGetAppIcon(session) : null;
            if (appIcon != null)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentColumn = appIcon != null ? 1 : 0;
            if (appIcon != null)
            {
                var image = new Image
                {
                    Source = appIcon,
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Focusable = false
                };
                Grid.SetColumn(image, 0);
                Grid.SetRow(image, 0);
                row.Children.Add(image);
            }

            var title = new TextBlock
            {
                Text = plugin.GetMediaSessionDisplayName(session),
                Foreground = TryFindResource("TextBrush") as Brush,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, contentColumn);
            Grid.SetRow(title, 0);
            row.Children.Add(title);

            var muteButton = new Button
            {
                Content = session.IsMuted ? plugin.Loc("LOCAS_MediaSessionMuted") : $"{session.VolumePercent}%",
                Tag = session.Id,
                MinWidth = 78,
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Focusable = true
            };
            muteButton.Click += MuteButton_Click;
            muteButton.PreviewKeyDown += MuteButton_PreviewKeyDown;
            Grid.SetColumn(muteButton, contentColumn + 1);
            Grid.SetRow(muteButton, 0);
            row.Children.Add(muteButton);

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = Math.Max(0, Math.Min(100, session.VolumePercent)),
                Tag = session.Id,
                Margin = new Thickness(0, 8, 0, 0),
                Focusable = true,
                IsSnapToTickEnabled = false
            };
            slider.ValueChanged += Slider_ValueChanged;
            slider.PreviewKeyDown += Slider_PreviewKeyDown;
            Grid.SetColumn(slider, contentColumn);
            Grid.SetColumnSpan(slider, 2);
            Grid.SetRow(slider, 1);
            row.Children.Add(slider);

            return row;
        }

        private static ImageSource TryGetAppIcon(AudioSessionInfo session)
        {
            var path = session?.ProcessPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            if (IconCache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null)
                    {
                        return null;
                    }

                    var source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(24, 24));
                    source.Freeze();
                    IconCache[path] = source;
                    return source;
                }
            }
            catch
            {
                return null;
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isRefreshing || !IsLoaded)
            {
                return;
            }

            var slider = sender as Slider;
            var sessionId = slider?.Tag as string;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            plugin.SetMediaSessionVolume(sessionId, (float)(Math.Max(0, Math.Min(100, e.NewValue)) / 100d), false);
            UpdateRowLabel(slider, (int)Math.Round(slider.Value));
        }

        private void Slider_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Down && e.Key != Key.Up)
            {
                return;
            }

            e.Handled = true;
            var slider = sender as Slider;
            if (slider == null)
            {
                return;
            }

            var direction = e.Key == Key.Left || e.Key == Key.Down ? -1 : 1;
            var step = Math.Max(1, plugin.Settings.VolumeStepPercent);
            slider.Value = Math.Max(0, Math.Min(100, slider.Value + step * direction));
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMute(sender as Button);
        }

        private void MuteButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            e.Handled = true;
            ToggleMute(sender as Button);
        }

        private void ToggleMute(Button button)
        {
            var sessionId = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            plugin.ToggleMediaSessionMute(sessionId);
            Refresh();
        }

        private void UpdateRowLabel(Slider slider, int value)
        {
            var row = slider?.Parent as Grid;
            if (row == null)
            {
                return;
            }

            foreach (var child in row.Children)
            {
                var button = child as Button;
                if (button != null)
                {
                    button.Content = $"{value}%";
                    return;
                }
            }
        }
    }
}
