using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK.Controls;

namespace PlayniteAudioSwitcher
{
    public partial class AudioMediaSessionListControl : PluginUserControl
    {
        private readonly AudioSwitcherPlugin plugin;

        public AudioMediaSessionListControl(AudioSwitcherPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            Loaded += AudioMediaSessionListControl_Loaded;
        }

        private void AudioMediaSessionListControl_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            SessionButtonsPanel.Children.Clear();

            try
            {
                var currentId = plugin.GetCurrentMediaSessionId();
                foreach (var session in plugin.GetMediaAudioSessions())
                {
                    var isCurrent = string.Equals(session.Id, currentId, StringComparison.OrdinalIgnoreCase);
                    var button = new Button
                    {
                        Content = isCurrent ? $"✓ {plugin.GetMediaSessionDisplayName(session)}" : plugin.GetMediaSessionDisplayName(session),
                        Tag = session.Id,
                        Focusable = true,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 0, 0, 6),
                        Padding = new Thickness(12, 8, 12, 8),
                        MinWidth = 240
                    };

                    KeyboardNavigation.SetIsTabStop(button, true);
                    KeyboardNavigation.SetDirectionalNavigation(button, KeyboardNavigationMode.Continue);
                    button.Click += SessionButton_Click;
                    button.PreviewKeyDown += SessionButton_PreviewKeyDown;
                    SessionButtonsPanel.Children.Add(button);
                }
            }
            catch
            {
                SessionButtonsPanel.Children.Clear();
            }
        }

        private void SessionButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            e.Handled = true;
            SelectSession(sender as Button);
        }

        private void SessionButton_Click(object sender, RoutedEventArgs e)
        {
            SelectSession(sender as Button);
        }

        private void SelectSession(Button button)
        {
            var sessionId = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            plugin.SetThemeSelectedMediaSession(sessionId);
            Refresh();
        }
    }
}
