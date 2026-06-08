using System;
using System.Collections.Generic;
using System.IO;
using Playnite.SDK.Data;
using Playnite.SDK.Models;

namespace PlayniteAudioSwitcher
{
    public sealed class GameAudioProfileStore
    {
        private readonly string filePath;
        private readonly object syncRoot = new object();
        private Dictionary<Guid, string> profiles = new Dictionary<Guid, string>();

        public GameAudioProfileStore(string pluginDataPath)
        {
            Directory.CreateDirectory(pluginDataPath);
            filePath = Path.Combine(pluginDataPath, "gameProfiles.json");
            Load();
        }

        public string GetDeviceId(Game game)
        {
            if (game == null)
            {
                return null;
            }

            lock (syncRoot)
            {
                return profiles.TryGetValue(game.Id, out var deviceId) ? deviceId : null;
            }
        }

        public void SetDevice(Game game, string deviceId)
        {
            if (game == null)
            {
                return;
            }

            lock (syncRoot)
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    profiles.Remove(game.Id);
                }
                else
                {
                    profiles[game.Id] = deviceId;
                }

                Save();
            }
        }

        private void Load()
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            if (Serialization.TryFromJsonFile<Dictionary<Guid, string>>(filePath, out var loadedProfiles))
            {
                profiles = loadedProfiles ?? new Dictionary<Guid, string>();
            }
        }

        private void Save()
        {
            File.WriteAllText(filePath, Serialization.ToJson(profiles, true));
        }
    }
}
