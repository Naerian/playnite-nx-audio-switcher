using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private readonly Dictionary<string, VolumeSliderAcceleration> sliderAccelerations = new Dictionary<string, VolumeSliderAcceleration>(StringComparer.OrdinalIgnoreCase);

        public AudioMediaMixerControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioMediaMixerControl_Loaded;
            Unloaded += AudioMediaMixerControl_Unloaded;
        }

        private void AudioMediaMixerControl_Loaded(object sender, RoutedEventArgs e)
        {
            plugin.Theme.MediaSessions.CollectionChanged -= MediaSessions_CollectionChanged;
            plugin.Theme.MediaSessions.CollectionChanged += MediaSessions_CollectionChanged;
            Refresh();
        }

        private void AudioMediaMixerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            plugin.Theme.MediaSessions.CollectionChanged -= MediaSessions_CollectionChanged;
        }

        private void MediaSessions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SynchronizeRows();
        }

        public void Refresh()
        {
            plugin.Theme.RefreshMediaSessions();
            SynchronizeRows();
        }

        private void SynchronizeRows()
        {
            var sessions = plugin.Theme.MediaSessions.ToList();
            if (sessions.Count == 0)
            {
                MixerRowsPanel.Children.Clear();
                MixerRowsPanel.Children.Add(CreateEmptyState());
                return;
            }

            if (MixerRowsPanel.Children.Count == 1 && !(MixerRowsPanel.Children[0] is Grid))
            {
                MixerRowsPanel.Children.Clear();
            }

            for (var desiredIndex = 0; desiredIndex < sessions.Count; desiredIndex++)
            {
                var session = sessions[desiredIndex];
                var existingIndex = -1;
                for (var currentIndex = desiredIndex; currentIndex < MixerRowsPanel.Children.Count; currentIndex++)
                {
                    if (MixerRowsPanel.Children[currentIndex] is FrameworkElement element &&
                        string.Equals(element.Tag as string, session.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        existingIndex = currentIndex;
                        break;
                    }
                }

                if (existingIndex < 0)
                {
                    MixerRowsPanel.Children.Insert(desiredIndex, CreateRow(session));
                    continue;
                }

                var existingRow = MixerRowsPanel.Children[existingIndex] as FrameworkElement;
                if (existingRow != null)
                {
                    existingRow.DataContext = session;
                }

                if (existingIndex != desiredIndex)
                {
                    var row = MixerRowsPanel.Children[existingIndex];
                    MixerRowsPanel.Children.RemoveAt(existingIndex);
                    MixerRowsPanel.Children.Insert(desiredIndex, row);
                }
            }

            while (MixerRowsPanel.Children.Count > sessions.Count)
            {
                MixerRowsPanel.Children.RemoveAt(MixerRowsPanel.Children.Count - 1);
            }
        }

        private TextBlock CreateEmptyState()
        {
            return new TextBlock
            {
                Text = plugin.Loc("LOCAS_MediaSessionUnavailable"),
                Foreground = TryFindResource("TextBrush") as Brush,
                Opacity = 0.75,
                Margin = new Thickness(0, 4, 0, 0)
            };
        }

        private UIElement CreateRow(AudioSwitcherThemeMediaSession session)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 12),
                Tag = session.Id,
                DataContext = session
            };

            var appIcon = session.ShowIcon ? TryGetAppIcon(session) : null;
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
                Foreground = TryFindResource("TextBrush") as Brush,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            title.SetBinding(TextBlock.TextProperty, new Binding(nameof(AudioSwitcherThemeMediaSession.Name)));
            Grid.SetColumn(title, contentColumn);
            Grid.SetRow(title, 0);
            row.Children.Add(title);

            var muteButton = new Button
            {
                MinWidth = 78,
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Focusable = true
            };
            muteButton.SetBinding(ContentControl.ContentProperty, new Binding(nameof(AudioSwitcherThemeMediaSession.VolumeLabel)));
            muteButton.SetBinding(Button.CommandProperty, new Binding(nameof(AudioSwitcherThemeMediaSession.ToggleMuteCommand)));
            Grid.SetColumn(muteButton, contentColumn + 1);
            Grid.SetRow(muteButton, 0);
            row.Children.Add(muteButton);

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Tag = session.Id,
                Margin = new Thickness(0, 8, 0, 0),
                Focusable = true,
                IsSnapToTickEnabled = false
            };
            slider.SetBinding(Slider.ValueProperty, new Binding(nameof(AudioSwitcherThemeMediaSession.VolumePercent))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            slider.PreviewKeyDown += Slider_PreviewKeyDown;
            Grid.SetColumn(slider, contentColumn);
            Grid.SetColumnSpan(slider, 2);
            Grid.SetRow(slider, 1);
            row.Children.Add(slider);

            return row;
        }

        private static ImageSource TryGetAppIcon(AudioSwitcherThemeMediaSession session)
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
            var sessionId = slider.Tag as string ?? string.Empty;
            if (!sliderAccelerations.TryGetValue(sessionId, out var acceleration))
            {
                acceleration = new VolumeSliderAcceleration();
                sliderAccelerations[sessionId] = acceleration;
            }

            var step = acceleration.GetStep(e.Key, e.IsRepeat, plugin.Settings.VolumeStepPercent);
            slider.Value = Math.Max(0, Math.Min(100, slider.Value + step * direction));
        }
    }
}
