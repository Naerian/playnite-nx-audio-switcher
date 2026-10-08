using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Playnite.SDK.Data;

namespace PlayniteAudioSwitcher
{
    internal static class AudioVisualProfilePortableStore
    {
        private const string ManifestEntry = "visual-profile.json";

        public static void Export(AudioVisualProfileSnapshot snapshot, string filePath)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A file path is required.", nameof(filePath));
            }

            var json = Serialization.ToJson(snapshot, true);
            using (var stream = File.Create(filePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(ManifestEntry, CompressionLevel.Optimal);
                using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                {
                    writer.Write(json);
                }
            }
        }

        public static AudioVisualProfileSnapshot Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Visual profile file was not found.", filePath);
            }

            string json;
            if (string.Equals(Path.GetExtension(filePath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                json = File.ReadAllText(filePath, Encoding.UTF8);
            }
            else
            {
                using (var stream = File.OpenRead(filePath))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var entry = archive.GetEntry(ManifestEntry);
                    if (entry == null)
                    {
                        throw new InvalidDataException("The selected file is not an Audio Switcher visual profile.");
                    }

                    using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    {
                        json = reader.ReadToEnd();
                    }
                }
            }

            AudioVisualProfileSnapshot snapshot;
            if (!Serialization.TryFromJson(json, out snapshot) || snapshot == null)
            {
                throw new InvalidDataException("The visual profile manifest is empty or invalid.");
            }

            if (snapshot.Version <= 0 || snapshot.Version > AudioVisualProfileSnapshot.CurrentVersion)
            {
                throw new InvalidDataException(
                    "This visual profile was created with a newer version of Audio Switcher.");
            }

            return snapshot;
        }
    }
}
