using System;
using System.Collections.Generic;
using System.Reflection;
using Playnite.SDK;

namespace PlayniteAudioSwitcher
{
    /// <summary>
    /// Complete visual + behavior definition of one on-screen notification surface (Desktop or
    /// Fullscreen). Mirrors the Controller Manager toast model, reduced to the single low-battery state.
    /// </summary>
    public sealed class AudioNotificationSurface : ObservableObject
    {
        public static readonly string[] Positions = { "TopRight", "TopLeft", "BottomRight", "BottomLeft" };
        public static readonly string[] Alignments = { "Left", "Center", "Right" };
        public static readonly string[] TextOrders = { "TitleFirst", "MessageFirst" };
        public static readonly string[] IconPositions = { "Left", "Right", "Top", "Bottom", "Hidden" };
        public static readonly string[] AccentModes = { "IconAndBorder", "IconOnly", "TintedBackground", "SolidBackground" };
        public static readonly string[] Animations = { "None", "Fade", "Slide", "Scale" };
        public static readonly string[] BorderPositions = { "Left", "Top", "Right", "Bottom", "Full" };
        public static readonly string[] ImageStretches = { "UniformToFill", "Uniform", "Fill" };
        public static readonly string[] HorizontalAlignments = { "Left", "Center", "Right" };
        public static readonly string[] VerticalAlignments = { "Top", "Center", "Bottom" };

        private string stylePreset = "Soft";
        private bool usePlayniteThemeAppearance = true;
        private bool showLowBatteryNotifications = true;
        private string position = "TopRight";
        private int width = 480;
        private int scalePercent = 104;
        private int durationMilliseconds = 4600;
        private int screenMargin = 24;
        private bool showShadow = true;
        private int padding = 16;
        private int elementSpacing = 8;
        private int iconSpacing = 14;
        private int cornerRadius = 14;
        private string titleFontFamily = "Inter";
        private string titleFontWeight = "SemiBold";
        private string messageFontFamily = "Inter";
        private string messageFontWeight = "Regular";
        private int titleFontSize = 18;
        private int messageFontSize = 14;
        private int messageMaxLines = 2;
        private string textAlignment = "Left";
        private string textOrder = "TitleFirst";
        private bool uppercaseTitle = false;
        private bool showTitle = true;
        private int iconSize = 42;
        private string iconPosition = "Left";
        private bool showIconContainer = false;
        private string iconContainerColor = "#28D24A5A";
        private string iconContainerBorderColor = "#70D24A5A";
        private int iconContainerBorderThickness = 1;
        private int iconContainerCornerRadius = 7;
        private int iconContainerPadding = 8;
        private string accentMode = "TintedBackground";
        private string animation = "Fade";
        private string backgroundColor = "#F2161A22";
        private bool useGradient = true;
        private string gradientColor = "#F2222832";
        private int gradientAngle = 168;
        private string textColor = "#FFF4F6F8";
        private string secondaryTextColor = "#FF9AA7B6";
        private string lowBatteryColor = "#FFD24A5A";
        private bool useStateBackgroundColors = false;
        private string lowBatteryBackgroundColor = "#F2161A22";
        private bool useStateBorderColors = true;
        private string lowBatteryBorderColor = "#FFD24A5A";
        private bool showBorder = true;
        private string borderPosition = "Full";
        private int borderThickness = 1;
        private bool useIndependentBorders = true;
        private int borderLeftThickness = 3;
        private int borderTopThickness = 1;
        private int borderRightThickness = 1;
        private int borderBottomThickness = 1;
        private bool useBorderGradient = false;
        private string borderGradientStartColor = "#FFFFFFFF";
        private string borderGradientEndColor = "#FFD24A5A";
        private int borderGradientAngle = 45;
        private bool showBorderGlow = false;
        private string borderGlowColor = "#80D24A5A";
        private int borderGlowBlur = 12;
        private int borderGlowOpacity = 30;
        private bool useBackgroundImage = false;
        private string backgroundImagePath = "";
        private string backgroundImageStretch = "UniformToFill";
        private string backgroundImageHorizontalAlignment = "Center";
        private string backgroundImageVerticalAlignment = "Center";
        private int backgroundImageOpacity = 45;
        private int backgroundImageTintOpacity = 45;

        public string StylePreset
        {
            get => stylePreset;
            set => SetValue(ref stylePreset, AudioNotificationPresets.Normalize(value));
        }

        public bool UsePlayniteThemeAppearance
        {
            get => usePlayniteThemeAppearance;
            set => SetValue(ref usePlayniteThemeAppearance, value);
        }

        public bool ShowLowBatteryNotifications
        {
            get => showLowBatteryNotifications;
            set => SetValue(ref showLowBatteryNotifications, value);
        }

        public string Position
        {
            get => position;
            set
            {
                var choice = Choice(value, Positions);
                if (choice != null)
                {
                    SetValue(ref position, choice);
                }
            }
        }

        public int Width
        {
            get => width;
            set => SetValue(ref width, Math.Max(280, Math.Min(900, value)));
        }

        public int ScalePercent
        {
            get => scalePercent;
            set => SetValue(ref scalePercent, Math.Max(80, Math.Min(160, value)));
        }

        public int DurationMilliseconds
        {
            get => durationMilliseconds;
            set => SetValue(ref durationMilliseconds, Math.Max(2000, Math.Min(15000, value)));
        }

        public int ScreenMargin
        {
            get => screenMargin;
            set => SetValue(ref screenMargin, Math.Max(8, Math.Min(64, value)));
        }

        public bool ShowShadow
        {
            get => showShadow;
            set => SetValue(ref showShadow, value);
        }

        public int Padding
        {
            get => padding;
            set => SetValue(ref padding, Math.Max(0, Math.Min(40, value)));
        }

        public int ElementSpacing
        {
            get => elementSpacing;
            set => SetValue(ref elementSpacing, Math.Max(0, Math.Min(40, value)));
        }

        public int IconSpacing
        {
            get => iconSpacing;
            set => SetValue(ref iconSpacing, Math.Max(0, Math.Min(40, value)));
        }

        public int CornerRadius
        {
            get => cornerRadius;
            set => SetValue(ref cornerRadius, Math.Max(0, Math.Min(40, value)));
        }

        public string TitleFontFamily
        {
            get => titleFontFamily;
            set => SetValue(ref titleFontFamily, AudioNotificationFonts.Normalize(value));
        }

        public string TitleFontWeight
        {
            get => titleFontWeight;
            set => SetValue(ref titleFontWeight, AudioNotificationFonts.NormalizeWeight(value));
        }

        public string MessageFontFamily
        {
            get => messageFontFamily;
            set => SetValue(ref messageFontFamily, AudioNotificationFonts.Normalize(value));
        }

        public string MessageFontWeight
        {
            get => messageFontWeight;
            set => SetValue(ref messageFontWeight, AudioNotificationFonts.NormalizeWeight(value));
        }

        public int TitleFontSize
        {
            get => titleFontSize;
            set => SetValue(ref titleFontSize, Math.Max(12, Math.Min(36, value)));
        }

        public int MessageFontSize
        {
            get => messageFontSize;
            set => SetValue(ref messageFontSize, Math.Max(10, Math.Min(30, value)));
        }

        public int MessageMaxLines
        {
            get => messageMaxLines;
            set => SetValue(ref messageMaxLines, Math.Max(1, Math.Min(6, value)));
        }

        public string TextAlignment
        {
            get => textAlignment;
            set
            {
                var choice = Choice(value, Alignments);
                if (choice != null)
                {
                    SetValue(ref textAlignment, choice);
                }
            }
        }

        public string TextOrder
        {
            get => textOrder;
            set
            {
                var choice = Choice(value, TextOrders);
                if (choice != null)
                {
                    SetValue(ref textOrder, choice);
                }
            }
        }

        public bool UppercaseTitle
        {
            get => uppercaseTitle;
            set => SetValue(ref uppercaseTitle, value);
        }

        public bool ShowTitle
        {
            get => showTitle;
            set => SetValue(ref showTitle, value);
        }

        public int IconSize
        {
            get => iconSize;
            set => SetValue(ref iconSize, Math.Max(16, Math.Min(128, value)));
        }

        public string IconPosition
        {
            get => iconPosition;
            set
            {
                var choice = Choice(value, IconPositions);
                if (choice != null)
                {
                    SetValue(ref iconPosition, choice);
                }
            }
        }

        public bool ShowIconContainer
        {
            get => showIconContainer;
            set => SetValue(ref showIconContainer, value);
        }

        public string IconContainerColor
        {
            get => iconContainerColor;
            set => SetValue(ref iconContainerColor, value ?? string.Empty);
        }

        public string IconContainerBorderColor
        {
            get => iconContainerBorderColor;
            set => SetValue(ref iconContainerBorderColor, value ?? string.Empty);
        }

        public int IconContainerBorderThickness
        {
            get => iconContainerBorderThickness;
            set => SetValue(ref iconContainerBorderThickness, Math.Max(0, Math.Min(8, value)));
        }

        public int IconContainerCornerRadius
        {
            get => iconContainerCornerRadius;
            set => SetValue(ref iconContainerCornerRadius, Math.Max(0, Math.Min(40, value)));
        }

        public int IconContainerPadding
        {
            get => iconContainerPadding;
            set => SetValue(ref iconContainerPadding, Math.Max(0, Math.Min(24, value)));
        }

        public string AccentMode
        {
            get => accentMode;
            set
            {
                var choice = Choice(value, AccentModes);
                if (choice != null)
                {
                    SetValue(ref accentMode, choice);
                }
            }
        }

        public string Animation
        {
            get => animation;
            set
            {
                var choice = Choice(value, Animations);
                if (choice != null)
                {
                    SetValue(ref animation, choice);
                }
            }
        }

        public string BackgroundColor
        {
            get => backgroundColor;
            set => SetValue(ref backgroundColor, value ?? string.Empty);
        }

        public bool UseGradient
        {
            get => useGradient;
            set => SetValue(ref useGradient, value);
        }

        public string GradientColor
        {
            get => gradientColor;
            set => SetValue(ref gradientColor, value ?? string.Empty);
        }

        public int GradientAngle
        {
            get => gradientAngle;
            set => SetValue(ref gradientAngle, ((value % 360) + 360) % 360);
        }

        public string TextColor
        {
            get => textColor;
            set => SetValue(ref textColor, value ?? string.Empty);
        }

        public string SecondaryTextColor
        {
            get => secondaryTextColor;
            set => SetValue(ref secondaryTextColor, value ?? string.Empty);
        }

        public string LowBatteryColor
        {
            get => lowBatteryColor;
            set => SetValue(ref lowBatteryColor, value ?? string.Empty);
        }

        public bool UseStateBackgroundColors
        {
            get => useStateBackgroundColors;
            set => SetValue(ref useStateBackgroundColors, value);
        }

        public string LowBatteryBackgroundColor
        {
            get => lowBatteryBackgroundColor;
            set => SetValue(ref lowBatteryBackgroundColor, value ?? string.Empty);
        }

        public bool UseStateBorderColors
        {
            get => useStateBorderColors;
            set => SetValue(ref useStateBorderColors, value);
        }

        public string LowBatteryBorderColor
        {
            get => lowBatteryBorderColor;
            set => SetValue(ref lowBatteryBorderColor, value ?? string.Empty);
        }

        public bool ShowBorder
        {
            get => showBorder;
            set => SetValue(ref showBorder, value);
        }

        public string BorderPosition
        {
            get => borderPosition;
            set
            {
                var choice = Choice(value, BorderPositions);
                if (choice != null)
                {
                    SetValue(ref borderPosition, choice);
                }
            }
        }

        public int BorderThickness
        {
            get => borderThickness;
            set => SetValue(ref borderThickness, Math.Max(0, Math.Min(10, value)));
        }

        public bool UseIndependentBorders
        {
            get => useIndependentBorders;
            set => SetValue(ref useIndependentBorders, value);
        }

        public int BorderLeftThickness
        {
            get => borderLeftThickness;
            set => SetValue(ref borderLeftThickness, Math.Max(0, Math.Min(12, value)));
        }

        public int BorderTopThickness
        {
            get => borderTopThickness;
            set => SetValue(ref borderTopThickness, Math.Max(0, Math.Min(12, value)));
        }

        public int BorderRightThickness
        {
            get => borderRightThickness;
            set => SetValue(ref borderRightThickness, Math.Max(0, Math.Min(12, value)));
        }

        public int BorderBottomThickness
        {
            get => borderBottomThickness;
            set => SetValue(ref borderBottomThickness, Math.Max(0, Math.Min(12, value)));
        }

        public bool UseBorderGradient
        {
            get => useBorderGradient;
            set => SetValue(ref useBorderGradient, value);
        }

        public string BorderGradientStartColor
        {
            get => borderGradientStartColor;
            set => SetValue(ref borderGradientStartColor, value ?? string.Empty);
        }

        public string BorderGradientEndColor
        {
            get => borderGradientEndColor;
            set => SetValue(ref borderGradientEndColor, value ?? string.Empty);
        }

        public int BorderGradientAngle
        {
            get => borderGradientAngle;
            set => SetValue(ref borderGradientAngle, ((value % 360) + 360) % 360);
        }

        public bool ShowBorderGlow
        {
            get => showBorderGlow;
            set => SetValue(ref showBorderGlow, value);
        }

        public string BorderGlowColor
        {
            get => borderGlowColor;
            set => SetValue(ref borderGlowColor, value ?? string.Empty);
        }

        public int BorderGlowBlur
        {
            get => borderGlowBlur;
            set => SetValue(ref borderGlowBlur, Math.Max(0, Math.Min(40, value)));
        }

        public int BorderGlowOpacity
        {
            get => borderGlowOpacity;
            set => SetValue(ref borderGlowOpacity, Math.Max(0, Math.Min(100, value)));
        }

        public bool UseBackgroundImage
        {
            get => useBackgroundImage;
            set => SetValue(ref useBackgroundImage, value);
        }

        public string BackgroundImagePath
        {
            get => backgroundImagePath;
            set => SetValue(ref backgroundImagePath, value ?? string.Empty);
        }

        public string BackgroundImageStretch
        {
            get => backgroundImageStretch;
            set
            {
                var choice = Choice(value, ImageStretches);
                if (choice != null)
                {
                    SetValue(ref backgroundImageStretch, choice);
                }
            }
        }

        public string BackgroundImageHorizontalAlignment
        {
            get => backgroundImageHorizontalAlignment;
            set
            {
                var choice = Choice(value, HorizontalAlignments);
                if (choice != null)
                {
                    SetValue(ref backgroundImageHorizontalAlignment, choice);
                }
            }
        }

        public string BackgroundImageVerticalAlignment
        {
            get => backgroundImageVerticalAlignment;
            set
            {
                var choice = Choice(value, VerticalAlignments);
                if (choice != null)
                {
                    SetValue(ref backgroundImageVerticalAlignment, choice);
                }
            }
        }

        public int BackgroundImageOpacity
        {
            get => backgroundImageOpacity;
            set => SetValue(ref backgroundImageOpacity, Math.Max(0, Math.Min(100, value)));
        }

        public int BackgroundImageTintOpacity
        {
            get => backgroundImageTintOpacity;
            set => SetValue(ref backgroundImageTintOpacity, Math.Max(0, Math.Min(100, value)));
        }

        public AudioNotificationSurface Clone()
        {
            var copy = new AudioNotificationSurface();
            copy.CopyFrom(this);
            return copy;
        }

        public void CopyFrom(AudioNotificationSurface other)
        {
            if (other == null)
            {
                return;
            }

            foreach (var property in CopyableProperties)
            {
                property.SetValue(this, property.GetValue(other, null), null);
            }
        }

        private static readonly PropertyInfo[] CopyableProperties = BuildCopyableProperties();

        private static PropertyInfo[] BuildCopyableProperties()
        {
            var list = new List<PropertyInfo>();
            foreach (var property in typeof(AudioNotificationSurface).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    list.Add(property);
                }
            }

            return list.ToArray();
        }

        private static string Choice(string value, string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            foreach (var item in allowed)
            {
                if (string.Equals(item, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }
    }
}
