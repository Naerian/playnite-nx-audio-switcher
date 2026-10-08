using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace PlayniteAudioSwitcher
{
    /// <summary>
    /// Portable Desktop + Fullscreen notification layout pack (Controller Manager visual-profile model).
    /// </summary>
    public sealed class AudioVisualProfileSnapshot
    {
        public const int CurrentVersion = 1;
        public const string FileExtension = ".asvisual";

        public int Version { get; set; }
        public string Name { get; set; }
        public string ExportedUtc { get; set; }
        public Dictionary<string, object> Desktop { get; set; }
        public Dictionary<string, object> Fullscreen { get; set; }
        public string DesktopBackgroundImageData { get; set; }
        public string DesktopBackgroundImageExtension { get; set; }
        public string FullscreenBackgroundImageData { get; set; }
        public string FullscreenBackgroundImageExtension { get; set; }

        public static AudioVisualProfileSnapshot FromSettings(AudioSwitcherSettings settings, string name)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var desktop = settings.DesktopNotification ?? AudioNotificationPresets.CreateDefault(true);
            var fullscreen = settings.FullscreenNotification ?? AudioNotificationPresets.CreateDefault(false);
            return new AudioVisualProfileSnapshot
            {
                Version = CurrentVersion,
                Name = string.IsNullOrWhiteSpace(name) ? "Visual profile" : name.Trim(),
                ExportedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Desktop = PackSurface(desktop),
                Fullscreen = PackSurface(fullscreen),
                DesktopBackgroundImageData = ReadImageData(desktop.BackgroundImagePath),
                DesktopBackgroundImageExtension = ImageExtension(desktop.BackgroundImagePath),
                FullscreenBackgroundImageData = ReadImageData(fullscreen.BackgroundImagePath),
                FullscreenBackgroundImageExtension = ImageExtension(fullscreen.BackgroundImagePath)
            };
        }

        public void ApplyTo(AudioSwitcherSettings settings, string imageDirectory)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.DesktopNotification == null)
            {
                settings.DesktopNotification = AudioNotificationPresets.CreateDefault(true);
            }

            if (settings.FullscreenNotification == null)
            {
                settings.FullscreenNotification = AudioNotificationPresets.CreateDefault(false);
            }

            ApplySurface(settings.DesktopNotification, Desktop, true);
            ApplySurface(settings.FullscreenNotification, Fullscreen, false);
            settings.DesktopNotification.BackgroundImagePath = RestoreImage(
                DesktopBackgroundImageData, DesktopBackgroundImageExtension,
                imageDirectory, "desktop", settings.DesktopNotification.BackgroundImagePath);
            settings.FullscreenNotification.BackgroundImagePath = RestoreImage(
                FullscreenBackgroundImageData, FullscreenBackgroundImageExtension,
                imageDirectory, "fullscreen", settings.FullscreenNotification.BackgroundImagePath);
            if (!string.IsNullOrWhiteSpace(settings.DesktopNotification.BackgroundImagePath))
            {
                settings.DesktopNotification.UseBackgroundImage = true;
            }

            if (!string.IsNullOrWhiteSpace(settings.FullscreenNotification.BackgroundImagePath))
            {
                settings.FullscreenNotification.UseBackgroundImage = true;
            }
        }

        internal static Dictionary<string, object> PackSurface(AudioNotificationSurface surface)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (surface == null)
            {
                return values;
            }

            foreach (var property in SurfaceProperties)
            {
                if (string.Equals(property.Name, nameof(AudioNotificationSurface.BackgroundImagePath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                values[property.Name] = property.GetValue(surface, null);
            }

            return values;
        }

        internal static void ApplySurface(AudioNotificationSurface surface, IDictionary<string, object> values,
            bool desktop)
        {
            if (surface == null || values == null || values.Count == 0)
            {
                return;
            }

            foreach (var pair in values)
            {
                if (string.Equals(pair.Key, nameof(AudioNotificationSurface.BackgroundImagePath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var property = typeof(AudioNotificationSurface).GetProperty(pair.Key,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null || !property.CanWrite || pair.Value == null)
                {
                    continue;
                }

                try
                {
                    var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    var text = Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
                    if (type == typeof(string) && pair.Key.EndsWith("Color", StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    object converted = type == typeof(string)
                        ? text
                        : type == typeof(bool)
                            ? Convert.ToBoolean(pair.Value, CultureInfo.InvariantCulture)
                            : type == typeof(int)
                                ? Convert.ToInt32(pair.Value, CultureInfo.InvariantCulture)
                                : type == typeof(double)
                                    ? Convert.ToDouble(pair.Value, CultureInfo.InvariantCulture)
                                    : Convert.ChangeType(pair.Value, type, CultureInfo.InvariantCulture);
                    property.SetValue(surface, converted, null);
                }
                catch
                {
                    // One bad field must not abort the whole pack.
                }
            }

            surface.StylePreset = AudioNotificationPresets.Normalize(surface.StylePreset);
            _ = desktop;
        }

        private static readonly PropertyInfo[] SurfaceProperties = BuildSurfaceProperties();

        private static PropertyInfo[] BuildSurfaceProperties()
        {
            var list = new List<PropertyInfo>();
            foreach (var property in typeof(AudioNotificationSurface).GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    list.Add(property);
                }
            }

            return list.ToArray();
        }

        private static string ReadImageData(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                return Convert.ToBase64String(File.ReadAllBytes(path));
            }
            catch
            {
                return null;
            }
        }

        private static string ImageExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ".png";
            }

            var extension = Path.GetExtension(path);
            return string.IsNullOrWhiteSpace(extension) ? ".png" : extension.ToLowerInvariant();
        }

        private static string RestoreImage(string data, string extension, string directory, string stem,
            string fallback)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return fallback ?? string.Empty;
            }

            try
            {
                Directory.CreateDirectory(directory ?? string.Empty);
                var ext = string.IsNullOrWhiteSpace(extension) ? ".png" : extension;
                if (!ext.StartsWith(".", StringComparison.Ordinal))
                {
                    ext = "." + ext;
                }

                var path = Path.Combine(directory, stem + "-bg" + ext);
                File.WriteAllBytes(path, Convert.FromBase64String(data));
                return path;
            }
            catch
            {
                return fallback ?? string.Empty;
            }
        }
    }
}
