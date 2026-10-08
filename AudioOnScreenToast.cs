using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PlayniteAudioSwitcher
{
    internal sealed class AudioToastRequest
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public Geometry Icon { get; set; }
        public AudioNotificationSurface Surface { get; set; }

        /// <summary>Drops any queued toast first (used by the settings preview buttons).</summary>
        public bool Replace { get; set; }
    }

    /// <summary>
    /// Top-most, click-through toast. Layer model and styling follow the Controller Manager toast:
    /// card + image + tint + border overlay + content, with an inset that keeps blur/glow from clipping.
    /// </summary>
    internal sealed class AudioOnScreenToast : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x20;
        private const int WsExToolWindow = 0x80;
        private const int WsExNoActivate = 0x08000000;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;
        private const int InterToastDelayMilliseconds = 900;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);

        private static AudioOnScreenToast instance;

        private readonly Queue<AudioToastRequest> pending = new Queue<AudioToastRequest>();
        private readonly TextBlock titleText;
        private readonly TextBlock messageText;
        private readonly Path icon;
        private readonly Border iconContainer;
        private readonly Grid contentLayout;
        private readonly Grid cardShell;
        private readonly StackPanel textPanel;
        private readonly Border card;
        private readonly Border imageLayer;
        private readonly Border tintLayer;
        private readonly Border borderOverlay;
        private readonly Border contentHost;
        private readonly DispatcherTimer holdTimer;
        private readonly DispatcherTimer interToastTimer;
        private bool pauseBeforeQueuedToast;
        private double currentShadowInset;
        private AudioToastRequest current;
        private AudioNotificationSurface currentStyle;

        private AudioOnScreenToast()
        {
            Width = 430;
            Height = 100;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Opacity = 0;

            // Tabler icons are outline glyphs: they must be stroked, never filled.
            icon = new Path
            {
                Width = 28,
                Height = 28,
                Stretch = Stretch.Uniform,
                Stroke = Brushes.White,
                StrokeThickness = 2.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent,
                SnapsToDevicePixels = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconContainer = new Border
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = icon
            };
            titleText = new TextBlock
            {
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            messageText = new TextBlock
            {
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(198, 203, 212)),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            textPanel = new StackPanel();
            textPanel.Children.Add(titleText);
            textPanel.Children.Add(messageText);
            contentLayout = new Grid();
            contentHost = new Border
            {
                Padding = new Thickness(18, 14, 18, 14),
                Child = contentLayout
            };
            card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(244, 18, 20, 24)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0)
            };
            imageLayer = new Border { IsHitTestVisible = false };
            tintLayer = new Border { IsHitTestVisible = false };
            borderOverlay = new Border { Background = Brushes.Transparent, IsHitTestVisible = false };
            cardShell = new Grid();
            cardShell.Children.Add(card);
            cardShell.Children.Add(imageLayer);
            cardShell.Children.Add(tintLayer);
            cardShell.Children.Add(borderOverlay);
            cardShell.Children.Add(contentHost);
            ConfigureContentLayout("Left", 14);
            Content = cardShell;

            SourceInitialized += OnSourceInitialized;
            holdTimer = new DispatcherTimer();
            holdTimer.Tick += OnHoldElapsed;
            interToastTimer = new DispatcherTimer();
            interToastTimer.Tick += OnInterToastElapsed;
        }

        public static void Show(AudioToastRequest request)
        {
            if (request == null || Application.Current == null)
            {
                return;
            }

            var dispatcher = Application.Current.Dispatcher;
            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => Show(request)));
                return;
            }

            if (instance == null)
            {
                instance = new AudioOnScreenToast();
                instance.Closed += (_, __) => instance = null;
            }

            if (request.Replace)
            {
                instance.ResetQueue();
            }

            instance.pending.Enqueue(request);
            if (instance.current == null)
            {
                instance.ShowNext();
            }
        }

        private void ResetQueue()
        {
            pending.Clear();
            holdTimer.Stop();
            interToastTimer.Stop();
            pauseBeforeQueuedToast = false;
            current = null;
            currentStyle = null;
            BeginAnimation(OpacityProperty, null);
            cardShell.RenderTransform = Transform.Identity;
            Opacity = 0;
        }

        private void ShowNext()
        {
            if (pending.Count == 0)
            {
                pauseBeforeQueuedToast = false;
                current = null;
                currentStyle = null;
                Hide();
                return;
            }

            if (pauseBeforeQueuedToast)
            {
                pauseBeforeQueuedToast = false;
                interToastTimer.Interval = TimeSpan.FromMilliseconds(InterToastDelayMilliseconds);
                interToastTimer.Start();
                return;
            }

            current = pending.Dequeue();
            var style = current.Surface ?? AudioNotificationPresets.CreateDefault(false);
            currentStyle = style;
            Present(current, style);
        }

        private void Present(AudioToastRequest request, AudioNotificationSurface style)
        {
            titleText.Text = style.UppercaseTitle
                ? (request.Title ?? string.Empty).ToUpperInvariant()
                : request.Title;
            messageText.Text = request.Message;
            messageText.Visibility = string.IsNullOrWhiteSpace(request.Message) ? Visibility.Collapsed : Visibility.Visible;
            textPanel.Children.Clear();
            var messageFirst = string.Equals(style.TextOrder, "MessageFirst", StringComparison.OrdinalIgnoreCase);
            if (messageFirst)
            {
                textPanel.Children.Add(messageText);
                textPanel.Children.Add(titleText);
            }
            else
            {
                textPanel.Children.Add(titleText);
                textPanel.Children.Add(messageText);
            }

            var scale = style.ScalePercent / 100.0;
            var iconSize = Math.Max(16, style.IconSize * scale);
            var titleSize = Math.Max(10, style.TitleFontSize * scale);
            var messageSize = Math.Max(9, style.MessageFontSize * scale);
            var padding = Math.Max(0, style.Padding * scale);
            var elementSpacing = Math.Max(0, style.ElementSpacing * scale);
            var cardWidth = Math.Max(280, style.Width * scale);
            currentShadowInset = style.ShowShadow || style.ShowBorderGlow
                ? Math.Ceiling(Math.Max(18, style.BorderGlowBlur + 4) * scale)
                : 0;
            cardShell.Margin = new Thickness(currentShadowInset);
            Width = cardWidth + currentShadowInset * 2;

            titleText.FontSize = titleSize;
            messageText.FontSize = messageSize;
            titleText.FontFamily = AudioNotificationFonts.Resolve(style.TitleFontFamily, style.TitleFontWeight);
            titleText.FontWeight = AudioNotificationFonts.ResolveEffectiveWeight(style.TitleFontFamily, style.TitleFontWeight);
            messageText.FontFamily = AudioNotificationFonts.Resolve(style.MessageFontFamily, style.MessageFontWeight);
            messageText.FontWeight = AudioNotificationFonts.ResolveEffectiveWeight(style.MessageFontFamily, style.MessageFontWeight);
            messageText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            messageText.LineHeight = Math.Ceiling(messageSize * 1.28);
            messageText.MaxHeight = messageText.LineHeight * style.MessageMaxLines;
            messageText.TextTrimming = TextTrimming.CharacterEllipsis;
            titleText.TextAlignment = AudioNotificationFonts.ResolveAlignment(style.TextAlignment);
            messageText.TextAlignment = titleText.TextAlignment;
            titleText.Visibility = style.ShowTitle && !string.IsNullOrWhiteSpace(request.Title)
                ? Visibility.Visible : Visibility.Collapsed;
            titleText.Margin = new Thickness(0);
            messageText.Margin = new Thickness(0);
            if (titleText.Visibility == Visibility.Visible && messageText.Visibility == Visibility.Visible)
            {
                if (messageFirst)
                {
                    titleText.Margin = new Thickness(0, elementSpacing, 0, 0);
                }
                else
                {
                    messageText.Margin = new Thickness(0, elementSpacing, 0, 0);
                }
            }

            icon.Width = iconSize;
            icon.Height = iconSize;
            icon.Data = request.Icon;
            icon.StrokeThickness = Math.Max(1.4, iconSize * 0.085);
            var iconContainerPadding = style.ShowIconContainer
                ? Math.Max(0, style.IconContainerPadding * scale) : 0;
            iconContainer.Padding = new Thickness(iconContainerPadding);
            iconContainer.Background = style.ShowIconContainer
                ? Brush(style.IconContainerColor, Color.FromArgb(32, 0, 0, 0))
                : Brushes.Transparent;
            iconContainer.BorderBrush = Brush(style.IconContainerBorderColor, Colors.Transparent);
            iconContainer.BorderThickness = style.ShowIconContainer
                ? new Thickness(Math.Max(0, style.IconContainerBorderThickness * scale))
                : new Thickness(0);
            iconContainer.CornerRadius = new CornerRadius(Math.Max(0, style.IconContainerCornerRadius * scale));
            var iconGap = Math.Max(0, style.IconSpacing * scale);
            var verticalIcon = string.Equals(style.IconPosition, "Top", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(style.IconPosition, "Bottom", StringComparison.OrdinalIgnoreCase);
            var hiddenIcon = string.Equals(style.IconPosition, "Hidden", StringComparison.OrdinalIgnoreCase) ||
                request.Icon == null;
            var textHeight = (titleText.Visibility == Visibility.Visible ? titleSize : 0) +
                (messageText.Visibility == Visibility.Visible
                    ? messageSize * 2 + (titleText.Visibility == Visibility.Visible ? elementSpacing : 0)
                    : 0);
            var contentHeight = hiddenIcon
                ? textHeight
                : verticalIcon
                    ? iconSize + iconContainerPadding * 2 + iconGap + textHeight
                    : Math.Max(iconSize + iconContainerPadding * 2, textHeight);
            Height = Math.Max(1, contentHeight + padding * 2);
            ConfigureContentLayout(hiddenIcon ? "Hidden" : style.IconPosition, iconGap);
            contentHost.Padding = new Thickness(padding);

            card.CornerRadius = new CornerRadius(style.CornerRadius * scale);
            imageLayer.CornerRadius = card.CornerRadius;
            tintLayer.CornerRadius = card.CornerRadius;
            borderOverlay.CornerRadius = card.CornerRadius;
            imageLayer.ClipToBounds = true;
            tintLayer.ClipToBounds = true;

            var primaryTextBrush = Brush(style.TextColor, Colors.White);
            titleText.Foreground = primaryTextBrush;
            messageText.Foreground = Brush(style.SecondaryTextColor, Color.FromRgb(198, 203, 212));

            var accent = ParseColor(style.LowBatteryColor, Color.FromRgb(224, 82, 82));
            var accentBrush = new SolidColorBrush(accent);
            var borderAccent = style.UseStateBorderColors
                ? ParseColor(style.LowBatteryBorderColor, accent)
                : accent;
            var borderAccentBrush = new SolidColorBrush(borderAccent);
            var background = ParseColor(
                style.UseStateBackgroundColors ? style.LowBatteryBackgroundColor : style.BackgroundColor,
                Color.FromArgb(244, 18, 20, 24));
            if (string.Equals(style.AccentMode, "TintedBackground", StringComparison.OrdinalIgnoreCase))
            {
                background = Blend(background, accent, 0.12);
            }
            else if (string.Equals(style.AccentMode, "SolidBackground", StringComparison.OrdinalIgnoreCase))
            {
                background = Color.FromArgb(Math.Max((byte)220, background.A), accent.R, accent.G, accent.B);
            }

            card.Background = CreateCardBackground(style, background, accent);
            ApplyBackgroundImage(style, background);
            if (style.UseGradient && !style.UseBackgroundImage)
            {
                ApplyStateWash(style, accent);
            }

            card.BorderThickness = new Thickness(0);
            borderOverlay.BorderBrush = style.UseBorderGradient
                ? (Brush)new LinearGradientBrush(
                    style.UseStateBorderColors
                        ? Blend(borderAccent, Colors.White, 0.28)
                        : ParseColor(style.BorderGradientStartColor, accent),
                    style.UseStateBorderColors
                        ? borderAccent
                        : ParseColor(style.BorderGradientEndColor, accent),
                    style.BorderGradientAngle)
                : borderAccentBrush;
            borderOverlay.BorderThickness = style.ShowBorder
                ? style.UseIndependentBorders
                    ? new Thickness(style.BorderLeftThickness, style.BorderTopThickness,
                        style.BorderRightThickness, style.BorderBottomThickness)
                    : CreateBorderThickness(style.BorderPosition, style.BorderThickness)
                : new Thickness(0);
            borderOverlay.Effect = style.ShowBorder && style.ShowBorderGlow
                ? new DropShadowEffect
                {
                    BlurRadius = Math.Max(0, style.BorderGlowBlur * scale),
                    ShadowDepth = 0,
                    Opacity = style.BorderGlowOpacity / 100.0,
                    Color = style.UseStateBorderColors
                        ? borderAccent
                        : ParseColor(style.BorderGlowColor, accent),
                    Direction = 0
                }
                : null;

            icon.Stroke = string.Equals(style.AccentMode, "SolidBackground", StringComparison.OrdinalIgnoreCase)
                ? primaryTextBrush
                : accentBrush;
            icon.Fill = Brushes.Transparent;

            // Only the rounded surface casts the shadow; text and icon live in a crisp layer above.
            card.Effect = style.ShowShadow
                ? new DropShadowEffect
                {
                    BlurRadius = Math.Max(8, 12 * scale),
                    ShadowDepth = Math.Max(1, 3 * scale),
                    Opacity = 0.5,
                    Color = Colors.Black,
                    Direction = 300
                }
                : null;

            cardShell.Measure(new Size(Width, double.PositiveInfinity));
            Height = Math.Max(1, Math.Ceiling(cardShell.DesiredSize.Height));
            if (!IsVisible)
            {
                Show();
            }

            ApplyBounds(GetTargetScreen(), style.Position, style.ScreenMargin);
            BeginEntryAnimation(style);
            holdTimer.Interval = TimeSpan.FromMilliseconds(style.DurationMilliseconds);
            holdTimer.Start();
        }

        private void BeginEntryAnimation(AudioNotificationSurface style)
        {
            BeginAnimation(OpacityProperty, null);
            cardShell.RenderTransformOrigin = new Point(0.5, 0.5);
            cardShell.RenderTransform = Transform.Identity;
            var duration = TimeSpan.FromMilliseconds(190);
            if (string.Equals(style.Animation, "None", StringComparison.OrdinalIgnoreCase))
            {
                Opacity = 1;
                return;
            }

            if (string.Equals(style.Animation, "Slide", StringComparison.OrdinalIgnoreCase))
            {
                var from = style.Position.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 ? -42 : 42;
                var transform = new TranslateTransform(from, 0);
                cardShell.RenderTransform = transform;
                transform.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(from, 0, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            }
            else if (string.Equals(style.Animation, "Scale", StringComparison.OrdinalIgnoreCase))
            {
                var transform = new ScaleTransform(0.86, 0.86);
                cardShell.RenderTransform = transform;
                var easing = new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut };
                transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.86, 1, duration) { EasingFunction = easing });
                transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.86, 1, duration) { EasingFunction = easing });
            }

            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
        }

        private void BeginExitAnimation()
        {
            var style = currentStyle ?? new AudioNotificationSurface();
            if (string.Equals(style.Animation, "None", StringComparison.OrdinalIgnoreCase))
            {
                pauseBeforeQueuedToast = pending.Count > 0;
                ShowNext();
                return;
            }

            var duration = TimeSpan.FromMilliseconds(180);
            if (string.Equals(style.Animation, "Slide", StringComparison.OrdinalIgnoreCase))
            {
                var to = style.Position.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 ? -34 : 34;
                var transform = new TranslateTransform(0, 0);
                cardShell.RenderTransform = transform;
                transform.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(0, to, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
            }
            else if (string.Equals(style.Animation, "Scale", StringComparison.OrdinalIgnoreCase))
            {
                var transform = new ScaleTransform(1, 1);
                cardShell.RenderTransform = transform;
                transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.90, duration));
                transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.90, duration));
            }

            var fade = new DoubleAnimation(Opacity, 0, duration);
            fade.Completed += delegate
            {
                pauseBeforeQueuedToast = pending.Count > 0;
                ShowNext();
            };
            BeginAnimation(OpacityProperty, fade);
        }

        private void ApplyBackgroundImage(AudioNotificationSurface style, Color background)
        {
            imageLayer.Background = Brushes.Transparent;
            tintLayer.Background = Brushes.Transparent;
            if (!style.UseBackgroundImage || string.IsNullOrWhiteSpace(style.BackgroundImagePath) ||
                !System.IO.File.Exists(style.BackgroundImagePath))
            {
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(style.BackgroundImagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                imageLayer.Background = new ImageBrush(bitmap)
                {
                    Stretch = ParseImageStretch(style.BackgroundImageStretch),
                    AlignmentX = ParseAlignmentX(style.BackgroundImageHorizontalAlignment),
                    AlignmentY = ParseAlignmentY(style.BackgroundImageVerticalAlignment),
                    Opacity = style.BackgroundImageOpacity / 100.0
                };
                tintLayer.Background = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Round(255 * style.BackgroundImageTintOpacity / 100.0),
                    background.R, background.G, background.B));
            }
            catch
            {
                imageLayer.Background = Brushes.Transparent;
                tintLayer.Background = Brushes.Transparent;
            }
        }

        private static Stretch ParseImageStretch(string value)
        {
            if (string.Equals(value, "Uniform", StringComparison.OrdinalIgnoreCase)) return Stretch.Uniform;
            if (string.Equals(value, "Fill", StringComparison.OrdinalIgnoreCase)) return Stretch.Fill;
            return Stretch.UniformToFill;
        }

        private static AlignmentX ParseAlignmentX(string value)
        {
            if (string.Equals(value, "Left", StringComparison.OrdinalIgnoreCase)) return AlignmentX.Left;
            if (string.Equals(value, "Right", StringComparison.OrdinalIgnoreCase)) return AlignmentX.Right;
            return AlignmentX.Center;
        }

        private static AlignmentY ParseAlignmentY(string value)
        {
            if (string.Equals(value, "Top", StringComparison.OrdinalIgnoreCase)) return AlignmentY.Top;
            if (string.Equals(value, "Bottom", StringComparison.OrdinalIgnoreCase)) return AlignmentY.Bottom;
            return AlignmentY.Center;
        }

        private void ConfigureContentLayout(string position, double gap)
        {
            var normalized = string.IsNullOrWhiteSpace(position) ? "Left" : position;
            contentLayout.Children.Clear();
            contentLayout.ColumnDefinitions.Clear();
            contentLayout.RowDefinitions.Clear();
            icon.Margin = new Thickness(0);
            iconContainer.Margin = new Thickness(0);
            iconContainer.HorizontalAlignment = HorizontalAlignment.Center;
            iconContainer.VerticalAlignment = VerticalAlignment.Center;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            textPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            textPanel.VerticalAlignment = VerticalAlignment.Center;

            if (string.Equals(normalized, "Hidden", StringComparison.OrdinalIgnoreCase))
            {
                iconContainer.Visibility = Visibility.Collapsed;
                contentLayout.Children.Add(textPanel);
                return;
            }

            iconContainer.Visibility = Visibility.Visible;
            if (string.Equals(normalized, "Top", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Bottom", StringComparison.OrdinalIgnoreCase))
            {
                contentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var iconFirst = string.Equals(normalized, "Top", StringComparison.OrdinalIgnoreCase);
                Grid.SetRow(iconContainer, iconFirst ? 0 : 1);
                Grid.SetRow(textPanel, iconFirst ? 1 : 0);
                iconContainer.Margin = iconFirst ? new Thickness(0, 0, 0, gap) : new Thickness(0, gap, 0, 0);
            }
            else
            {
                var iconRight = string.Equals(normalized, "Right", StringComparison.OrdinalIgnoreCase);
                contentLayout.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = iconRight ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
                contentLayout.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = iconRight ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(iconContainer, iconRight ? 1 : 0);
                Grid.SetColumn(textPanel, iconRight ? 0 : 1);
                iconContainer.Margin = iconRight ? new Thickness(gap, 0, 0, 0) : new Thickness(0, 0, gap, 0);
            }

            contentLayout.Children.Add(iconContainer);
            contentLayout.Children.Add(textPanel);
        }

        private void OnHoldElapsed(object sender, EventArgs args)
        {
            holdTimer.Stop();
            BeginExitAnimation();
        }

        private void OnInterToastElapsed(object sender, EventArgs args)
        {
            interToastTimer.Stop();
            ShowNext();
        }

        private void OnSourceInitialized(object sender, EventArgs args)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
            SetWindowLongPtr(handle, GwlExStyle,
                new IntPtr(style | WsExTransparent | WsExToolWindow | WsExNoActivate));
        }

        private void ApplyBounds(Forms.Screen screen, string position, int screenMargin)
        {
            var bounds = (screen ?? Forms.Screen.PrimaryScreen).WorkingArea;
            var dpiScaleX = 1.0;
            var dpiScaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            var pixelWidth = (int)Math.Ceiling(Width * dpiScaleX);
            var pixelHeight = (int)Math.Ceiling(Height * dpiScaleY);
            var shadowInsetX = (int)Math.Ceiling(currentShadowInset * dpiScaleX);
            var shadowInsetY = (int)Math.Ceiling(currentShadowInset * dpiScaleY);
            var margin = Math.Max(8, Math.Min(64, screenMargin));
            var left = string.Equals(position, "TopLeft", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(position, "BottomLeft", StringComparison.OrdinalIgnoreCase)
                ? bounds.Left + margin - shadowInsetX
                : bounds.Right - pixelWidth - margin + shadowInsetX;
            var top = string.Equals(position, "BottomLeft", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(position, "BottomRight", StringComparison.OrdinalIgnoreCase)
                ? bounds.Bottom - pixelHeight - margin + shadowInsetY
                : bounds.Top + margin - shadowInsetY;
            SetWindowPos(new WindowInteropHelper(this).Handle, HwndTopmost, left, top,
                pixelWidth, pixelHeight, SwpNoActivate | SwpShowWindow);
        }

        /// <summary>Shows the toast on the monitor that currently holds the foreground window (the game).</summary>
        private Forms.Screen GetTargetScreen()
        {
            try
            {
                var foreground = GetForegroundWindow();
                if (foreground != IntPtr.Zero && foreground != new WindowInteropHelper(this).Handle)
                {
                    return Forms.Screen.FromHandle(foreground);
                }
            }
            catch
            {
            }

            return Forms.Screen.PrimaryScreen;
        }

        private static Thickness CreateBorderThickness(string position, double value)
        {
            if (string.Equals(position, "Left", StringComparison.OrdinalIgnoreCase)) return new Thickness(value, 0, 0, 0);
            if (string.Equals(position, "Top", StringComparison.OrdinalIgnoreCase)) return new Thickness(0, value, 0, 0);
            if (string.Equals(position, "Right", StringComparison.OrdinalIgnoreCase)) return new Thickness(0, 0, value, 0);
            if (string.Equals(position, "Full", StringComparison.OrdinalIgnoreCase)) return new Thickness(value);
            return new Thickness(0, 0, 0, value);
        }

        private void ApplyStateWash(AudioNotificationSurface style, Color accent)
        {
            var glow = Color.FromArgb(style.UseStateBackgroundColors ? (byte)48 : (byte)36,
                accent.R, accent.G, accent.B);
            var transparent = Color.FromArgb(0, accent.R, accent.G, accent.B);
            var wash = new RadialGradientBrush
            {
                Center = new Point(0.08, 0.92),
                GradientOrigin = new Point(0.08, 0.92),
                RadiusX = 0.85,
                RadiusY = 0.95,
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };
            wash.GradientStops.Add(new GradientStop(glow, 0));
            wash.GradientStops.Add(new GradientStop(Color.FromArgb(18, accent.R, accent.G, accent.B), 0.42));
            wash.GradientStops.Add(new GradientStop(transparent, 1));
            imageLayer.Background = wash;
        }

        private static Brush CreateCardBackground(AudioNotificationSurface style, Color background, Color accent)
        {
            if (!style.UseGradient)
            {
                return new SolidColorBrush(background);
            }

            var start = background;
            var end = ParseColor(style.GradientColor, background);
            if (style.UseStateBackgroundColors)
            {
                start = Opaque(ParseColor(style.LowBatteryBackgroundColor, background));
                end = Opaque(Blend(start, accent, 0.18));
            }

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(1, 0),
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };
            brush.GradientStops.Add(new GradientStop(start, 0));
            brush.GradientStops.Add(new GradientStop(Blend(start, end, 0.45), 0.55));
            brush.GradientStops.Add(new GradientStop(end, 1));
            return brush;
        }

        private static Color Opaque(Color color)
        {
            return Color.FromArgb(255, color.R, color.G, color.B);
        }

        private static Brush Brush(string value, Color fallback)
        {
            return new SolidColorBrush(ParseColor(value, fallback));
        }

        private static Color Blend(Color background, Color accent, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                background.A,
                (byte)Math.Round(background.R * (1 - amount) + accent.R * amount),
                (byte)Math.Round(background.G * (1 - amount) + accent.G * amount),
                (byte)Math.Round(background.B * (1 - amount) + accent.B * amount));
        }

        private static Color ParseColor(string value, Color fallback)
        {
            try { return (Color)ColorConverter.ConvertFromString(value); }
            catch { return fallback; }
        }

        private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hwnd, index, value)
                : new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr newStyle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
            int width, int height, uint flags);
    }
}
