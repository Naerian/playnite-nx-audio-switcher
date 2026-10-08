using System;

namespace PlayniteAudioSwitcher
{
    /// <summary>
    /// Named visual bundles for the Desktop and Fullscreen toasts. Values are the Controller Manager
    /// presets reduced to the single low-battery state.
    /// </summary>
    internal static class AudioNotificationPresets
    {
        public const string Custom = "Custom";
        public const string Soft = "Soft";
        public const string Compact = "Compact";
        public const string Bold = "Bold";
        public const string Arcade = "Arcade";
        public const string Minimal = "Minimal";
        public const string Cinematic = "Cinematic";

        public static readonly string[] Named = { Soft, Compact, Bold, Arcade, Minimal, Cinematic };
        public static readonly string[] All = { Soft, Compact, Bold, Arcade, Minimal, Cinematic, Custom };

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Soft;
            }

            var trimmed = value.Trim();
            if (string.Equals(trimmed, AudioThemeLayoutPack.PlayniteThemeLookKey, StringComparison.OrdinalIgnoreCase))
            {
                return AudioThemeLayoutPack.PlayniteThemeLookKey;
            }

            if (trimmed.StartsWith(ImportedVisualProfileCatalog.IdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            foreach (var name in All)
            {
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            return Custom;
        }

        public static bool IsNamedPluginPreset(string value)
        {
            var normalized = Normalize(value);
            foreach (var name in Named)
            {
                if (string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string LocKey(string preset)
        {
            var normalized = Normalize(preset);
            if (string.Equals(normalized, AudioThemeLayoutPack.PlayniteThemeLookKey, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(ImportedVisualProfileCatalog.IdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            switch (normalized)
            {
                case Compact: return "LOCAS_StylePresetCompact";
                case Bold: return "LOCAS_StylePresetBold";
                case Arcade: return "LOCAS_StylePresetArcade";
                case Minimal: return "LOCAS_StylePresetMinimal";
                case Cinematic: return "LOCAS_StylePresetCinematic";
                case Custom: return "LOCAS_StylePresetCustom";
                default: return "LOCAS_StylePresetSoft";
            }
        }

        public static AudioNotificationSurface CreateDefault(bool desktop)
        {
            var surface = new AudioNotificationSurface();
            Apply(surface, Soft, desktop);
            return surface;
        }

        /// <summary>Applies a preset in place. Behavior flags (theme bridge, alerts) are preserved.</summary>
        public static void Apply(AudioNotificationSurface surface, string presetId, bool desktop)
        {
            if (surface == null)
            {
                return;
            }

            var preset = Normalize(presetId);
            if (preset == Custom)
            {
                surface.StylePreset = Custom;
                return;
            }

            if (string.Equals(preset, AudioThemeLayoutPack.PlayniteThemeLookKey, StringComparison.OrdinalIgnoreCase) ||
                preset.StartsWith(ImportedVisualProfileCatalog.IdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fresh = new AudioNotificationSurface();
            switch (preset)
            {
                case Compact: ApplyCompact(fresh, desktop); break;
                case Bold: ApplyBold(fresh, desktop); break;
                case Arcade: ApplyArcade(fresh, desktop); break;
                case Minimal: ApplyMinimal(fresh, desktop); break;
                case Cinematic: ApplyCinematic(fresh, desktop); break;
                default: ApplySoft(fresh, desktop); break;
            }

            fresh.StylePreset = preset;
            fresh.UsePlayniteThemeAppearance = surface.UsePlayniteThemeAppearance;
            fresh.ShowLowBatteryNotifications = surface.ShowLowBatteryNotifications;
            surface.CopyFrom(fresh);
        }

        // Dark glass toast with a state-colored left rail and Inter hierarchy.
        private static void ApplySoft(AudioNotificationSurface s, bool desktop)
        {
            if (desktop)
            {
                ApplyPair(s, 400, 98, 3800, "BottomRight", "#F4181C24", "#FFF4F6F8", "#FF9AA7B6", "#FFD24A5A",
                    16, 13, 36, "Left", 14, 6, true, "Full", 1, 12, 22, true, 7);
            }
            else
            {
                ApplyPair(s, 480, 104, 4600, "TopRight", "#F2161A22", "#FFF4F6F8", "#FF9AA7B6", "#FFD24A5A",
                    18, 14, 42, "Left", 16, 8, true, "Full", 1, 14, 24, true, 8);
            }

            ApplyIdentity(s, AudioNotificationFonts.Inter, "SemiBold", "Left", "TintedBackground", "Fade");
            ApplyTypeHierarchy(s, AudioNotificationFonts.Inter, "SemiBold", AudioNotificationFonts.Inter, "Regular");
            s.UseGradient = true;
            s.GradientColor = desktop ? "#F2242A34" : "#F2222832";
            s.GradientAngle = 168;
            ApplyAccentRail(s, 3, 1, 1, 1, true);
        }

        // Broadcast ticker: Rajdhani, hard left rail, slide in, no extra chrome.
        private static void ApplyCompact(AudioNotificationSurface s, bool desktop)
        {
            if (desktop)
            {
                ApplyPair(s, 320, 88, 2400, "BottomRight", "#F30B0D14", "#FFF2F5FA", "#FF8E9BB0", "#FFE05252",
                    15, 11, 18, "Left", 14, 5, true, "Left", 0, 4, 14, false, 7);
            }
            else
            {
                ApplyPair(s, 360, 92, 2800, "TopRight", "#F20A0C12", "#FFF2F5FA", "#FF8E9BB0", "#FFE05252",
                    16, 12, 20, "Left", 16, 6, true, "Left", 0, 4, 16, false, 8);
            }

            ApplyIdentity(s, AudioNotificationFonts.Rajdhani, "SemiBold", "Left", "IconAndBorder", "Slide");
            ApplyTypeHierarchy(s, AudioNotificationFonts.Rajdhani, "SemiBold", AudioNotificationFonts.Rajdhani, "Regular");
            ApplyAccentRail(s, 4, 0, 0, 0, true);
            s.IconSpacing = desktop ? 12 : 14;
        }

        // Event-colored poster with a framed icon and Outfit titles.
        private static void ApplyBold(AudioNotificationSurface s, bool desktop)
        {
            if (desktop)
            {
                ApplyPair(s, 470, 106, 4400, "BottomLeft", "#F21A1E2A", "#FFFFFFFF", "#FFD7DEE8", "#FFC92D45",
                    21, 15, 38, "Right", 18, 8, true, "Full", 1, 14, 24, true, 7);
            }
            else
            {
                ApplyPair(s, 560, 114, 5200, "TopLeft", "#F2181C28", "#FFFFFFFF", "#FFD7DEE8", "#FFC92D45",
                    24, 16, 44, "Right", 22, 10, true, "Full", 1, 16, 28, true, 8);
            }

            ApplyIdentity(s, AudioNotificationFonts.Outfit, "Bold", "Left", "SolidBackground", "Scale");
            ApplyTypeHierarchy(s, AudioNotificationFonts.Outfit, "Bold", AudioNotificationFonts.Outfit, "Regular");
            s.ShowIconContainer = true;
            s.IconContainerColor = "#33FFFFFF";
            s.IconContainerBorderColor = "#66FFFFFF";
            s.IconContainerCornerRadius = desktop ? 10 : 12;
            s.IconContainerPadding = desktop ? 8 : 10;
            s.ShowBorderGlow = true;
            s.BorderGlowColor = "#80C92D45";
            s.BorderGlowBlur = desktop ? 14 : 16;
            s.BorderGlowOpacity = desktop ? 18 : 20;
        }

        // One neon accent on indigo, uppercase title, cabinet outline.
        private static void ApplyArcade(AudioNotificationSurface s, bool desktop)
        {
            if (desktop)
            {
                ApplyPair(s, 390, 100, 3800, "BottomLeft", "#F11A0E12", "#FFFFF4EA", "#FFFFC48A", "#FFFF4FA3",
                    16, 12, 30, "Top", 16, 7, true, "Full", 2, 16, 18, true, 7);
            }
            else
            {
                ApplyPair(s, 460, 108, 4400, "BottomRight", "#F1180C10", "#FFFFF4EA", "#FFFFC48A", "#FFFF4FA3",
                    18, 13, 36, "Top", 18, 8, true, "Full", 2, 18, 22, true, 8);
            }

            ApplyIdentity(s, AudioNotificationFonts.Orbitron, "SemiBold", "Center", "IconAndBorder", "Scale");
            ApplyTypeHierarchy(s, AudioNotificationFonts.Orbitron, "SemiBold", AudioNotificationFonts.Exo2, "Regular");
            s.UppercaseTitle = true;
            s.UseBorderGradient = true;
            s.BorderGradientStartColor = "#FFFF9A3C";
            s.BorderGradientEndColor = "#FFFF5A4A";
            s.BorderGradientAngle = 125;
            s.ShowBorderGlow = true;
            s.BorderGlowColor = "#90FF9A3C";
            s.BorderGlowBlur = desktop ? 16 : 18;
            s.BorderGlowOpacity = desktop ? 22 : 26;
            ApplyStateSurfaces(s, true, "#F1281810", 145, "#C03A0818", "#FFFF4FA3");
        }

        // Quiet type-first card. Almost no chrome, name only.
        private static void ApplyMinimal(AudioNotificationSurface s, bool desktop)
        {
            if (desktop)
            {
                ApplyPair(s, 300, 88, 2200, "TopLeft", "#C012151A", "#FFEEF1F5", "#FF9AA6B4", "#FFC98181",
                    14, 11, 16, "Hidden", 12, 3, false, "Full", 0, 4, 14, false, 7);
            }
            else
            {
                ApplyPair(s, 340, 92, 2400, "TopLeft", "#B8101318", "#FFEEF1F5", "#FF9AA6B4", "#FFC98181",
                    15, 12, 16, "Hidden", 14, 4, false, "Full", 0, 4, 16, false, 8);
            }

            ApplyIdentity(s, AudioNotificationFonts.Poppins, "Regular", "Left", "IconOnly", "Fade");
            ApplyTypeHierarchy(s, AudioNotificationFonts.Poppins, "Regular", AudioNotificationFonts.Poppins, "Regular");
        }

        // Letterbox toast: scene gradient, no artwork.
        private static void ApplyCinematic(AudioNotificationSurface s, bool desktop)
        {
            if (desktop)
            {
                ApplyPair(s, 440, 98, 4000, "BottomRight", "#FF050608", "#FFF7FBFC", "#FFB7CBD0", "#FFFF657A",
                    18, 13, 28, "Left", 16, 7, true, "Bottom", 2, 4, 22, true, 7);
            }
            else
            {
                ApplyPair(s, 540, 104, 5000, "TopRight", "#FF050608", "#FFF7FBFC", "#FFB7CBD0", "#FFFF657A",
                    20, 14, 34, "Left", 18, 8, true, "Bottom", 2, 4, 24, true, 8);
            }

            ApplyIdentity(s, AudioNotificationFonts.Outfit, "SemiBold", "Center", "IconAndBorder", "Fade");
            ApplyTypeHierarchy(s, AudioNotificationFonts.Outfit, "SemiBold", AudioNotificationFonts.Outfit, "Regular");
            ApplyAccentRail(s, 0, 0, 0, 2, false);
            s.UseGradient = true;
            s.GradientColor = "#FF101418";
            s.GradientAngle = 45;
            s.UseBorderGradient = true;
            s.BorderGradientStartColor = "#FF57C7E8";
            s.BorderGradientEndColor = "#FFFFC45C";
            s.BorderGradientAngle = 90;
            s.ShowBorderGlow = true;
            s.BorderGlowColor = "#6657C7E8";
            s.BorderGlowBlur = desktop ? 12 : 14;
            s.BorderGlowOpacity = desktop ? 16 : 18;
            ApplyStateSurfaces(s, true, "#FF101418", 45, "#FF16080C", "#FFFF657A");
        }

        private static void ApplyPair(AudioNotificationSurface s,
            int width, int scale, int duration, string position, string bg, string text, string secondary,
            string lowBattery, int titleSize, int messageSize, int iconSize, string iconPosition,
            int padding, int spacing, bool showBorder, string borderPosition, int borderThickness,
            int corner, int margin, bool showShadow, int containerPadding)
        {
            s.Width = width;
            s.ScalePercent = scale;
            s.DurationMilliseconds = duration;
            s.Position = position;
            s.BackgroundColor = bg;
            s.TextColor = text;
            s.UseGradient = false;
            s.GradientColor = bg;
            s.GradientAngle = 0;
            s.UppercaseTitle = false;
            s.SecondaryTextColor = secondary;
            s.LowBatteryColor = lowBattery;
            s.TitleFontSize = titleSize;
            s.MessageFontSize = messageSize;
            s.IconSize = iconSize;
            s.IconPosition = iconPosition;
            s.Padding = padding;
            s.ElementSpacing = spacing;
            s.ShowIconContainer = false;
            s.IconContainerColor = WithAlpha(lowBattery, "28");
            s.IconContainerBorderColor = WithAlpha(lowBattery, "70");
            s.IconContainerBorderThickness = 1;
            s.IconContainerCornerRadius = Math.Max(4, corner / 2);
            s.IconContainerPadding = containerPadding;
            s.IconSpacing = Math.Max(0, Math.Max(spacing, (int)Math.Round(padding * 0.75)));
            s.ShowBorder = showBorder;
            s.BorderPosition = borderPosition;
            s.BorderThickness = borderThickness;
            s.CornerRadius = corner;
            s.UseBorderGradient = false;
            s.UseStateBorderColors = false;
            s.ShowBorderGlow = false;
            s.ScreenMargin = margin;
            s.ShowShadow = showShadow;
            s.TextOrder = "TitleFirst";
            s.UseIndependentBorders = false;
            s.UseStateBackgroundColors = false;
            s.LowBatteryBackgroundColor = bg;
            s.LowBatteryBorderColor = lowBattery;
            s.BorderGradientStartColor = "#FFFFFFFF";
            s.BorderGradientEndColor = lowBattery;
            s.BorderGlowColor = WithAlpha(lowBattery, "80");
            s.UseBackgroundImage = false;
            s.BackgroundImagePath = string.Empty;
        }

        private static void ApplyIdentity(AudioNotificationSurface s,
            string font, string weight, string alignment, string accentMode, string animation)
        {
            s.TitleFontFamily = font;
            s.TitleFontWeight = weight;
            s.MessageFontFamily = font;
            s.MessageFontWeight = "Regular";
            s.MessageMaxLines = 2;
            s.TextAlignment = alignment;
            s.AccentMode = accentMode;
            s.Animation = animation;
            s.ShowTitle = true;
        }

        private static void ApplyTypeHierarchy(AudioNotificationSurface s,
            string titleFamily, string titleWeight, string messageFamily, string messageWeight)
        {
            s.TitleFontFamily = titleFamily;
            s.TitleFontWeight = titleWeight;
            s.MessageFontFamily = messageFamily;
            s.MessageFontWeight = messageWeight;
        }

        private static void ApplyAccentRail(AudioNotificationSurface s, int left, int top, int right, int bottom,
            bool stateColors)
        {
            s.ShowBorder = true;
            s.UseIndependentBorders = true;
            s.UseStateBorderColors = stateColors;
            s.BorderLeftThickness = left;
            s.BorderTopThickness = top;
            s.BorderRightThickness = right;
            s.BorderBottomThickness = bottom;
        }

        private static void ApplyStateSurfaces(AudioNotificationSurface s, bool useGradient, string gradient,
            int angle, string lowBatteryBackground, string lowBatteryBorder)
        {
            s.UseGradient = useGradient;
            s.GradientColor = gradient;
            s.GradientAngle = angle;
            s.UseStateBackgroundColors = true;
            s.LowBatteryBackgroundColor = lowBatteryBackground;
            s.UseStateBorderColors = true;
            s.LowBatteryBorderColor = lowBatteryBorder;
        }

        private static string WithAlpha(string color, string alpha)
        {
            return !string.IsNullOrWhiteSpace(color) && color.Length == 9 && color[0] == '#'
                ? "#" + alpha + color.Substring(3)
                : color;
        }
    }
}
