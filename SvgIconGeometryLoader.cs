using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace PlayniteAudioSwitcher
{
    internal static class SvgIconGeometryLoader
    {
        private static readonly Dictionary<string, string> cache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object cacheLock = new object();

        public static string GetPathData(string iconFileName)
        {
            var safeFileName = Path.GetFileName(iconFileName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                return string.Empty;
            }

            lock (cacheLock)
            {
                if (cache.TryGetValue(safeFileName, out var cachedData))
                {
                    return cachedData;
                }

                var pathData = LoadPathData(safeFileName);
                cache[safeFileName] = pathData;
                return pathData;
            }
        }

        private static string LoadPathData(string iconFileName)
        {
            try
            {
                var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var iconPath = Path.Combine(assemblyDirectory, "Icons", iconFileName);
                if (!File.Exists(iconPath))
                {
                    return string.Empty;
                }

                var document = XDocument.Load(iconPath);
                return string.Join(" ", document
                    .Descendants()
                    .Where(element => string.Equals(element.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase))
                    .Where(element => !string.Equals((string)element.Attribute("stroke"), "none", StringComparison.OrdinalIgnoreCase))
                    .Select(element => (string)element.Attribute("d"))
                    .Where(data => !string.IsNullOrWhiteSpace(data)));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
