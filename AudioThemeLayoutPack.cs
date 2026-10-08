using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    /// <summary>
    /// Loads layout packs from the active Playnite theme's AudioSwitcher folder
    /// (same idea as Controller Manager's embedded ControllerManager/ pack).
    /// </summary>
    internal static class AudioThemeLayoutPack
    {
        public const string PlayniteThemeLookKey = "PlayniteTheme";

        private static readonly object Sync = new object();
        private static string cachedDesktopDirectory;
        private static string cachedFullscreenDirectory;
        private static LayoutPack cachedDesktop;
        private static LayoutPack cachedFullscreen;

        public sealed class LayoutPack
        {
            public string DisplayName { get; set; }
            public string Directory { get; set; }
            public Dictionary<string, object> Values { get; set; }
        }

        private sealed class ManifestFile
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Author { get; set; }
        }

        public static void InvalidateCache()
        {
            lock (Sync)
            {
                cachedDesktopDirectory = null;
                cachedFullscreenDirectory = null;
                cachedDesktop = null;
                cachedFullscreen = null;
            }
        }

        public static bool HasLayout(IPlayniteAPI api, bool fullscreen)
        {
            LayoutPack pack;
            return TryGet(api, fullscreen, out pack);
        }

        public static string GetDisplayName(IPlayniteAPI api, bool fullscreen)
        {
            LayoutPack pack;
            if (!TryGet(api, fullscreen, out pack) || string.IsNullOrWhiteSpace(pack.DisplayName))
            {
                return "Playnite theme pack";
            }

            return pack.DisplayName;
        }

        public static bool TryGet(IPlayniteAPI api, bool fullscreen, out LayoutPack pack)
        {
            pack = null;
            var themeDirectory = AudioThemeAppearanceBridge.FindThemeDirectory(api, fullscreen);
            if (string.IsNullOrWhiteSpace(themeDirectory))
            {
                return false;
            }

            var packDirectory = Path.Combine(themeDirectory, "AudioSwitcher");
            lock (Sync)
            {
                if (fullscreen)
                {
                    if (!string.Equals(cachedFullscreenDirectory, packDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        cachedFullscreenDirectory = packDirectory;
                        cachedFullscreen = LoadPack(packDirectory, fullscreen);
                    }

                    pack = cachedFullscreen;
                }
                else
                {
                    if (!string.Equals(cachedDesktopDirectory, packDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        cachedDesktopDirectory = packDirectory;
                        cachedDesktop = LoadPack(packDirectory, fullscreen);
                    }

                    pack = cachedDesktop;
                }
            }

            return pack != null && pack.Values != null && pack.Values.Count > 0;
        }

        public static bool TryApply(IPlayniteAPI api, AudioNotificationSurface surface, bool fullscreen)
        {
            LayoutPack pack;
            if (surface == null || !TryGet(api, fullscreen, out pack))
            {
                return false;
            }

            AudioVisualProfileSnapshot.ApplySurface(surface, pack.Values, !fullscreen);
            if (!string.IsNullOrWhiteSpace(surface.BackgroundImagePath) &&
                !Path.IsPathRooted(surface.BackgroundImagePath))
            {
                var resolved = Path.GetFullPath(Path.Combine(pack.Directory, surface.BackgroundImagePath));
                var root = Path.GetFullPath(pack.Directory).TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(resolved))
                {
                    surface.BackgroundImagePath = resolved;
                    surface.UseBackgroundImage = true;
                }
            }

            return true;
        }

        private static LayoutPack LoadPack(string packDirectory, bool fullscreen)
        {
            if (string.IsNullOrWhiteSpace(packDirectory) || !Directory.Exists(packDirectory))
            {
                return null;
            }

            try
            {
                var specific = Path.Combine(packDirectory,
                    fullscreen ? "fullscreen-notification.json" : "desktop-notification.json");
                var shared = Path.Combine(packDirectory, "notification.json");
                var path = File.Exists(specific) ? specific : shared;
                if (!File.Exists(path))
                {
                    return null;
                }

                Dictionary<string, object> values;
                if (!Serialization.TryFromJson(File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF'), out values) ||
                    values == null || values.Count == 0)
                {
                    return null;
                }

                var displayName = "Playnite theme pack";
                var manifestPath = Path.Combine(packDirectory, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    ManifestFile manifest;
                    if (Serialization.TryFromJson(
                            File.ReadAllText(manifestPath, Encoding.UTF8).TrimStart('\uFEFF'), out manifest) &&
                        manifest != null && !string.IsNullOrWhiteSpace(manifest.Name))
                    {
                        displayName = string.IsNullOrWhiteSpace(manifest.Author)
                            ? manifest.Name.Trim()
                            : manifest.Name.Trim() + " — " + manifest.Author.Trim();
                    }
                }

                return new LayoutPack
                {
                    Directory = packDirectory,
                    DisplayName = displayName,
                    Values = values
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
