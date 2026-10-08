using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    /// <summary>
    /// Resolves live Playnite theme colors at display time. Theme authors map their own
    /// resource keys in AudioSwitcher/theme-bridge.json.
    /// </summary>
    internal static class AudioThemeAppearanceBridge
    {
        public const string RelativePath = "AudioSwitcher/theme-bridge.json";

        public static ThemeAppearanceColors Resolve(IPlayniteAPI api, bool fullscreenTheme)
        {
            var colors = new ThemeAppearanceColors();
            var mapping = ReadNotificationMap(FindThemeDirectory(api, fullscreenTheme));
            Bind(mapping, "Background", colors.SetBackground, ResolveStopPreference.First);
            Bind(mapping, "Gradient", colors.SetGradient, ResolveStopPreference.Last);
            Bind(mapping, "Text", colors.SetText, ResolveStopPreference.Any);
            Bind(mapping, "SecondaryText", colors.SetSecondaryText, ResolveStopPreference.Any);
            Bind(mapping, "Accent", colors.SetAccent, ResolveStopPreference.Any);
            BindBorder(mapping, colors);
            Bind(mapping, "Warning", colors.SetWarning, ResolveStopPreference.Any);
            BindStyle(mapping, "TextStyle", colors.ApplyTextStyle);
            BindStyle(mapping, "TitleStyle", colors.ApplyTitleStyle);
            BindStyle(mapping, "MessageStyle", colors.ApplyMessageStyle);
            return colors;
        }

        public static void ApplyLiveColors(IPlayniteAPI api, AudioNotificationSurface surface, bool fullscreenTheme)
        {
            if (surface == null || !surface.UsePlayniteThemeAppearance)
            {
                return;
            }

            var live = Resolve(api, fullscreenTheme);
            if (live == null || !live.HasAny)
            {
                return;
            }

            if (IsUsableHex(live.Background))
            {
                surface.BackgroundColor = live.Background;
                surface.UseStateBackgroundColors = false;
            }

            surface.TextColor = CoalesceHex(live.Text, surface.TextColor);
            surface.SecondaryTextColor = CoalesceHex(live.SecondaryText, surface.SecondaryTextColor);
            if (IsUsableHex(live.Warning))
            {
                surface.LowBatteryColor = live.Warning;
                surface.LowBatteryBorderColor = live.Warning;
            }

            if (IsUsableHex(live.Gradient))
            {
                surface.UseGradient = true;
                surface.GradientColor = live.Gradient;
                surface.UseStateBackgroundColors = false;
            }

            if (IsUsableHex(live.Border))
            {
                surface.BorderGradientStartColor = live.Border;
                surface.BorderGradientEndColor = IsUsableHex(live.BorderEnd)
                    ? live.BorderEnd
                    : live.Border;
                if (!string.Equals(surface.BorderGradientStartColor, surface.BorderGradientEndColor,
                    StringComparison.OrdinalIgnoreCase))
                {
                    surface.UseBorderGradient = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(live.FontFamily))
            {
                surface.MessageFontFamily = live.FontFamily;
                if (!string.IsNullOrWhiteSpace(live.FontWeight))
                {
                    surface.MessageFontWeight = live.FontWeight;
                }
            }

            var titleFamily = !string.IsNullOrWhiteSpace(live.TitleFontFamily)
                ? live.TitleFontFamily
                : live.FontFamily;
            var titleWeight = !string.IsNullOrWhiteSpace(live.TitleFontWeight)
                ? live.TitleFontWeight
                : live.FontWeight;
            if (!string.IsNullOrWhiteSpace(titleFamily))
            {
                surface.TitleFontFamily = titleFamily;
            }

            if (!string.IsNullOrWhiteSpace(titleWeight))
            {
                surface.TitleFontWeight = titleWeight;
            }
        }

        private static string CoalesceHex(string live, string fallback)
        {
            return IsUsableHex(live) ? live : fallback;
        }

        private static bool IsUsableHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex[0] != '#' || hex.Length < 7)
            {
                return false;
            }

            if (hex.Length >= 9 &&
                string.Equals(hex.Substring(1, 2), "00", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static void Bind(IDictionary<string, string> keys, string name,
            Action<string, bool> apply, ResolveStopPreference preference)
        {
            var resourceKey = Lookup(keys, name);
            string hex;
            bool gradient;
            if (!string.IsNullOrWhiteSpace(resourceKey) &&
                TryResolve(resourceKey, out hex, out gradient, preference))
            {
                apply(hex, gradient);
            }
        }

        private static void BindStyle(IDictionary<string, string> keys, string name, Action<ThemeTypeface> apply)
        {
            var resourceKey = Lookup(keys, name);
            ThemeTypeface typeface;
            if (!string.IsNullOrWhiteSpace(resourceKey) && TryResolveStyle(resourceKey, out typeface))
            {
                apply(typeface);
            }
        }

        private static void BindBorder(IDictionary<string, string> keys, ThemeAppearanceColors colors)
        {
            if (colors == null)
            {
                return;
            }

            var resourceKey = Lookup(keys, "Border");
            if (!string.IsNullOrWhiteSpace(resourceKey))
            {
                string start;
                string end;
                bool gradient;
                if (TryResolveStops(resourceKey, out start, out end, out gradient))
                {
                    colors.SetBorder(start, gradient);
                    colors.SetBorderEnd(end);
                }
                else if (TryResolve(resourceKey, out start, out gradient, ResolveStopPreference.Any))
                {
                    colors.SetBorder(start, gradient);
                    colors.SetBorderEnd(start);
                }
            }

            var borderEndKey = Lookup(keys, "BorderEnd");
            string endHex;
            bool endGradient;
            if (!string.IsNullOrWhiteSpace(borderEndKey) &&
                TryResolve(borderEndKey, out endHex, out endGradient, ResolveStopPreference.Any))
            {
                colors.SetBorderEnd(endHex);
                if (string.IsNullOrWhiteSpace(colors.Border))
                {
                    colors.SetBorder(endHex, endGradient);
                }
            }
        }

        private static string Lookup(IDictionary<string, string> keys, string name)
        {
            if (keys == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string value;
            if (keys.TryGetValue(name, out value))
            {
                return value;
            }

            foreach (var pair in keys)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private static bool TryResolveStyle(string resourceKey, out ThemeTypeface typeface)
        {
            typeface = new ThemeTypeface();
            if (string.IsNullOrWhiteSpace(resourceKey) || Application.Current == null)
            {
                return false;
            }

            object resource;
            try
            {
                resource = Application.Current.TryFindResource(resourceKey.Trim());
            }
            catch
            {
                return false;
            }

            var style = resource as Style;
            if (style == null)
            {
                return false;
            }

            ApplyStyleSetters(style, typeface);
            return typeface.HasAny;
        }

        private static void ApplyStyleSetters(Style style, ThemeTypeface typeface)
        {
            if (style == null)
            {
                return;
            }

            if (style.BasedOn != null)
            {
                ApplyStyleSetters(style.BasedOn, typeface);
            }

            foreach (var baseSetter in style.Setters)
            {
                var setter = baseSetter as Setter;
                if (setter == null || setter.Property == null)
                {
                    continue;
                }

                var value = ResolveSetterValue(setter.Value);
                var property = setter.Property.Name;
                if (string.Equals(property, "FontFamily", StringComparison.OrdinalIgnoreCase))
                {
                    var family = FormatFontFamily(value);
                    if (!string.IsNullOrWhiteSpace(family))
                    {
                        typeface.FontFamily = family;
                    }
                }
                else if (string.Equals(property, "FontWeight", StringComparison.OrdinalIgnoreCase))
                {
                    var weight = FormatFontWeight(value);
                    if (!string.IsNullOrWhiteSpace(weight))
                    {
                        typeface.FontWeight = weight;
                    }
                }
                else if (string.Equals(property, "Foreground", StringComparison.OrdinalIgnoreCase))
                {
                    string hex;
                    bool gradient;
                    if (TryConvert(value, out hex, out gradient, ResolveStopPreference.Any))
                    {
                        typeface.Foreground = hex;
                    }
                }
            }
        }

        private static object ResolveSetterValue(object value)
        {
            var dynamicResource = value as DynamicResourceExtension;
            if (dynamicResource != null && dynamicResource.ResourceKey != null && Application.Current != null)
            {
                try
                {
                    var resolved = Application.Current.TryFindResource(dynamicResource.ResourceKey);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
                catch
                {
                }
            }

            return value;
        }

        private static string FormatFontFamily(object value)
        {
            var family = value as FontFamily;
            if (family != null)
            {
                if (!string.IsNullOrWhiteSpace(family.Source) &&
                    family.Source.IndexOf("://", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return family.Source.Trim();
                }

                foreach (var name in family.FamilyNames.Values)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name.Trim();
                    }
                }
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static string FormatFontWeight(object value)
        {
            if (value is FontWeight)
            {
                var weight = (FontWeight)value;
                if (weight >= FontWeights.Bold)
                {
                    return "Bold";
                }

                if (weight >= FontWeights.SemiBold)
                {
                    return "SemiBold";
                }

                return "Regular";
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (text.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("Semi", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "Bold";
            }

            if (text.IndexOf("Semi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "SemiBold";
            }

            return "Regular";
        }

        private static bool TryResolve(string resourceKey, out string hex, out bool isGradient,
            ResolveStopPreference preference)
        {
            hex = null;
            isGradient = false;
            if (string.IsNullOrWhiteSpace(resourceKey) || Application.Current == null)
            {
                return false;
            }

            object resource;
            try
            {
                resource = Application.Current.TryFindResource(resourceKey.Trim());
            }
            catch
            {
                return false;
            }

            return TryConvert(resource, out hex, out isGradient, preference);
        }

        private static bool TryResolveStops(string resourceKey, out string startHex, out string endHex,
            out bool isGradient)
        {
            startHex = null;
            endHex = null;
            isGradient = false;
            if (string.IsNullOrWhiteSpace(resourceKey) || Application.Current == null)
            {
                return false;
            }

            object resource;
            try
            {
                resource = Application.Current.TryFindResource(resourceKey.Trim());
            }
            catch
            {
                return false;
            }

            var gradient = resource as GradientBrush;
            if (gradient == null || gradient.GradientStops == null || gradient.GradientStops.Count < 2)
            {
                return false;
            }

            string first = null;
            string last = null;
            foreach (var stop in gradient.GradientStops)
            {
                if (stop.Color.A == 0)
                {
                    continue;
                }

                var stopHex = Format(stop.Color);
                if (first == null)
                {
                    first = stopHex;
                }

                last = stopHex;
            }

            if (first == null || last == null || string.Equals(first, last, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            startHex = first;
            endHex = last;
            isGradient = true;
            return true;
        }

        private static bool TryConvert(object resource, out string hex, out bool isGradient,
            ResolveStopPreference preference)
        {
            hex = null;
            isGradient = false;
            if (resource is Color)
            {
                var color = (Color)resource;
                if (color.A == 0)
                {
                    return false;
                }

                hex = Format(color);
                return true;
            }

            var solid = resource as SolidColorBrush;
            if (solid != null)
            {
                if (solid.Color.A == 0)
                {
                    return false;
                }

                hex = Format(solid.Color);
                return true;
            }

            var gradient = resource as GradientBrush;
            if (gradient == null || gradient.GradientStops == null || gradient.GradientStops.Count == 0)
            {
                return false;
            }

            string first = null;
            string last = null;
            foreach (var stop in gradient.GradientStops)
            {
                if (stop.Color.A == 0)
                {
                    continue;
                }

                var stopHex = Format(stop.Color);
                if (first == null)
                {
                    first = stopHex;
                }

                last = stopHex;
            }

            if (first == null)
            {
                return false;
            }

            isGradient = true;
            hex = preference == ResolveStopPreference.First ? first : last ?? first;
            return true;
        }

        private static string Format(Color color)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}",
                color.A, color.R, color.G, color.B);
        }

        public static string FindThemeDirectory(IPlayniteAPI api, bool fullscreen)
        {
            if (api == null || api.ApplicationSettings == null)
            {
                return string.Empty;
            }

            var themeId = fullscreen
                ? api.ApplicationSettings.FullscreenTheme
                : api.ApplicationSettings.DesktopTheme;
            if (string.IsNullOrWhiteSpace(themeId))
            {
                return string.Empty;
            }

            var mode = fullscreen ? "Fullscreen" : "Desktop";
            var roots = new List<string>();
            if (api.Paths != null)
            {
                if (!string.IsNullOrWhiteSpace(api.Paths.ApplicationPath))
                {
                    roots.Add(Path.Combine(api.Paths.ApplicationPath, "Themes", mode));
                }

                if (!string.IsNullOrWhiteSpace(api.Paths.ConfigurationPath))
                {
                    roots.Add(Path.Combine(api.Paths.ConfigurationPath, "Themes", mode));
                }
            }

            foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                var exact = Path.Combine(root, themeId.Trim());
                if (Directory.Exists(exact))
                {
                    return exact;
                }

                try
                {
                    var wanted = themeId.Trim();
                    foreach (var directory in Directory.GetDirectories(root))
                    {
                        var name = Path.GetFileName(directory);
                        if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith("_" + wanted, StringComparison.OrdinalIgnoreCase))
                        {
                            return directory;
                        }
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static Dictionary<string, string> ReadNotificationMap(string themeDirectory)
        {
            if (string.IsNullOrWhiteSpace(themeDirectory))
            {
                return null;
            }

            var path = Path.Combine(themeDirectory, "AudioSwitcher", "theme-bridge.json");
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var file = Serialization.FromJson<ThemeBridgeFile>(File.ReadAllText(path).TrimStart('\uFEFF'));
                return file?.Notification;
            }
            catch
            {
                return null;
            }
        }

        private enum ResolveStopPreference
        {
            Any,
            First,
            Last
        }

        private sealed class ThemeBridgeFile
        {
            public Dictionary<string, string> Notification { get; set; }
        }

        internal sealed class ThemeAppearanceColors
        {
            public string Background { get; private set; }
            public string Gradient { get; private set; }
            public string Text { get; private set; }
            public string SecondaryText { get; private set; }
            public string Accent { get; private set; }
            public string Border { get; private set; }
            public string BorderEnd { get; private set; }
            public string Warning { get; private set; }
            public string FontFamily { get; private set; }
            public string FontWeight { get; private set; }
            public string TitleFontFamily { get; private set; }
            public string TitleFontWeight { get; private set; }

            public bool HasAny
            {
                get
                {
                    return !string.IsNullOrWhiteSpace(Background) ||
                        !string.IsNullOrWhiteSpace(Gradient) ||
                        !string.IsNullOrWhiteSpace(Text) ||
                        !string.IsNullOrWhiteSpace(SecondaryText) ||
                        !string.IsNullOrWhiteSpace(Accent) ||
                        !string.IsNullOrWhiteSpace(Border) ||
                        !string.IsNullOrWhiteSpace(BorderEnd) ||
                        !string.IsNullOrWhiteSpace(Warning) ||
                        !string.IsNullOrWhiteSpace(FontFamily) ||
                        !string.IsNullOrWhiteSpace(TitleFontFamily);
                }
            }

            public void SetBackground(string hex, bool gradient) { Background = hex; }
            public void SetGradient(string hex, bool gradient) { Gradient = hex; }
            public void SetText(string hex, bool gradient) { Text = hex; }
            public void SetSecondaryText(string hex, bool gradient) { SecondaryText = hex; }
            public void SetAccent(string hex, bool gradient) { Accent = hex; }
            public void SetBorder(string hex, bool gradient) { Border = hex; }
            public void SetBorderEnd(string hex) { BorderEnd = hex; }
            public void SetWarning(string hex, bool gradient) { Warning = hex; }

            public void ApplyTextStyle(ThemeTypeface typeface)
            {
                if (typeface == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(typeface.FontFamily))
                {
                    FontFamily = typeface.FontFamily;
                }

                if (!string.IsNullOrWhiteSpace(typeface.FontWeight))
                {
                    FontWeight = typeface.FontWeight;
                }

                if (string.IsNullOrWhiteSpace(Text) && !string.IsNullOrWhiteSpace(typeface.Foreground))
                {
                    Text = typeface.Foreground;
                }
            }

            public void ApplyTitleStyle(ThemeTypeface typeface)
            {
                if (typeface == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(typeface.FontFamily))
                {
                    TitleFontFamily = typeface.FontFamily;
                }

                if (!string.IsNullOrWhiteSpace(typeface.FontWeight))
                {
                    TitleFontWeight = typeface.FontWeight;
                }
            }

            public void ApplyMessageStyle(ThemeTypeface typeface)
            {
                if (typeface == null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(FontFamily) && !string.IsNullOrWhiteSpace(typeface.FontFamily))
                {
                    FontFamily = typeface.FontFamily;
                }
            }
        }

        internal sealed class ThemeTypeface
        {
            public string FontFamily { get; set; }
            public string FontWeight { get; set; }
            public string Foreground { get; set; }

            public bool HasAny
            {
                get
                {
                    return !string.IsNullOrWhiteSpace(FontFamily) ||
                        !string.IsNullOrWhiteSpace(FontWeight) ||
                        !string.IsNullOrWhiteSpace(Foreground);
                }
            }
        }
    }
}
