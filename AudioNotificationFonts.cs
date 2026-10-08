using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PlayniteAudioSwitcher
{
    /// <summary>
    /// Resolves the font families shipped in the extension's Fonts folder (same set as Controller
    /// Manager) without requiring them to be installed in Windows.
    /// </summary>
    public static class AudioNotificationFonts
    {
        public const string SystemDefault = "Default";
        public const string ChakraPetch = "ChakraPetch";
        public const string Exo2 = "Exo 2";
        public const string Inter = "Inter";
        public const string Montserrat = "Montserrat";
        public const string Orbitron = "Orbitron";
        public const string Outfit = "Outfit";
        public const string Poppins = "Poppins";
        public const string Rajdhani = "Rajdhani";
        public const string Trebuchet = "Trebuchet MS";

        public static readonly string[] NamedFonts =
        {
            SystemDefault, Inter, Montserrat, Outfit, Poppins, Rajdhani, ChakraPetch, Exo2, Orbitron, Trebuchet
        };

        public static readonly string[] Weights = { "Regular", "SemiBold", "Bold" };

        private static readonly IDictionary<string, string> FolderNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ChakraPetch, "Chakra_Petch" }, { Exo2, "Exo2" }, { Inter, "Inter" }, { Montserrat, "Montserrat" },
                { Orbitron, "Orbitron" }, { Outfit, "Outfit" }, { Poppins, "Poppins" }, { Rajdhani, "Rajdhani" }
            };

        private static readonly IDictionary<string, string> FamilyNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ChakraPetch, "Chakra Petch" }, { Exo2, "Exo 2" }, { Inter, "Inter 18pt 18pt" }, { Montserrat, "Montserrat" },
                { Orbitron, "Orbitron" }, { Outfit, "Outfit" }, { Poppins, "Poppins" }, { Rajdhani, "Rajdhani" }
            };

        private static readonly IDictionary<string, FontFamily> Cache =
            new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

        public static string Normalize(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var trimmed = value.Trim();
                foreach (var font in NamedFonts)
                {
                    if (string.Equals(font, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        return font;
                    }
                }

                // Fonts coming from a theme bridge may be any installed family name.
                return trimmed;
            }

            return SystemDefault;
        }

        public static FontFamily Resolve(string value, string weight)
        {
            var id = Normalize(value);
            if (id == SystemDefault)
            {
                return SystemFonts.MessageFontFamily;
            }

            string folderName;
            string familyName;
            if (!FolderNames.TryGetValue(id, out folderName) || !FamilyNames.TryGetValue(id, out familyName))
            {
                // Trebuchet MS and theme-provided names resolve through the installed fonts.
                try { return new FontFamily(id); }
                catch { return SystemFonts.MessageFontFamily; }
            }

            var normalizedWeight = NormalizeWeight(weight);
            var cacheKey = id + "|" + normalizedWeight;
            FontFamily cached;
            lock (Cache)
            {
                if (Cache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }

            try
            {
                var fontFolder = Path.Combine(GetBinaryFolder(), "Fonts", folderName);
                if (!Directory.Exists(fontFolder))
                {
                    return SystemFonts.MessageFontFamily;
                }

                var folderUri = new Uri(fontFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    UriKind.Absolute);
                if (normalizedWeight == "SemiBold" || normalizedWeight == "Bold")
                {
                    familyName += " SemiBold";
                }

                var family = new FontFamily(folderUri, "./#" + familyName);
                lock (Cache)
                {
                    Cache[cacheKey] = family;
                }

                return family;
            }
            catch
            {
                return SystemFonts.MessageFontFamily;
            }
        }

        public static string NormalizeWeight(string value)
        {
            // Medium was visually indistinguishable from SemiBold in WPF; treat it as SemiBold.
            if (string.Equals(value, "Regular", StringComparison.OrdinalIgnoreCase)) return "Regular";
            if (string.Equals(value, "Bold", StringComparison.OrdinalIgnoreCase)) return "Bold";
            return "SemiBold";
        }

        public static FontWeight ResolveWeight(string value)
        {
            switch (NormalizeWeight(value))
            {
                case "Regular": return FontWeights.Normal;
                case "Bold": return FontWeights.Bold;
                default: return FontWeights.SemiBold;
            }
        }

        public static FontWeight ResolveEffectiveWeight(string fontFamily, string value)
        {
            var id = Normalize(fontFamily);
            if (id == SystemDefault || !FolderNames.ContainsKey(id))
            {
                return ResolveWeight(value);
            }

            // Bundled fonts pick their semi-bold face through the family name.
            return NormalizeWeight(value) == "Bold" ? FontWeights.Bold : FontWeights.Normal;
        }

        public static string NormalizeAlignment(string value)
        {
            if (string.Equals(value, "Center", StringComparison.OrdinalIgnoreCase)) return "Center";
            if (string.Equals(value, "Right", StringComparison.OrdinalIgnoreCase)) return "Right";
            return "Left";
        }

        public static TextAlignment ResolveAlignment(string value)
        {
            switch (NormalizeAlignment(value))
            {
                case "Center": return TextAlignment.Center;
                case "Right": return TextAlignment.Right;
                default: return TextAlignment.Left;
            }
        }

        public static string NormalizeAccentMode(string value)
        {
            if (string.Equals(value, "IconOnly", StringComparison.OrdinalIgnoreCase)) return "IconOnly";
            if (string.Equals(value, "TintedBackground", StringComparison.OrdinalIgnoreCase)) return "TintedBackground";
            if (string.Equals(value, "SolidBackground", StringComparison.OrdinalIgnoreCase)) return "SolidBackground";
            return "IconAndBorder";
        }

        public static string NormalizeAnimation(string value)
        {
            if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase)) return "None";
            if (string.Equals(value, "Slide", StringComparison.OrdinalIgnoreCase)) return "Slide";
            if (string.Equals(value, "Scale", StringComparison.OrdinalIgnoreCase)) return "Scale";
            return "Fade";
        }

        private static string GetBinaryFolder()
        {
            var assembly = typeof(AudioNotificationFonts).Assembly;
            try
            {
                var location = assembly.Location;
                if (!string.IsNullOrWhiteSpace(location))
                {
                    return Path.GetDirectoryName(location) ?? AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            catch
            {
            }

            try
            {
                var uri = new Uri(assembly.CodeBase);
                return Path.GetDirectoryName(uri.LocalPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }
    }

    /// <summary>Shows each font name in its own typeface inside the settings selectors.</summary>
    public sealed class NotificationFontPreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return AudioNotificationFonts.Resolve(value as string, "Regular");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>Hex string (#AARRGGBB / #RRGGBB) to brush for the settings color swatches.</summary>
    public sealed class HexColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var text = value as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(text.Trim()));
                }
            }
            catch
            {
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>Static font lists for XAML bindings.</summary>
    public static class AudioNotificationFontLists
    {
        public static string[] Families => AudioNotificationFonts.NamedFonts;
    }
}
