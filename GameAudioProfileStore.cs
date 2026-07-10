using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Playnite.SDK.Data;
using Playnite.SDK.Models;

namespace PlayniteAudioSwitcher
{
    public sealed class GameAudioProfileStore
    {
        private readonly string filePath;
        private readonly object syncRoot = new object();
        private Dictionary<Guid, GameAudioProfile> profiles = new Dictionary<Guid, GameAudioProfile>();

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
                return GetProfile(game)?.DeviceId;
            }
        }

        public string GetInputDeviceId(Game game)
        {
            if (game == null)
            {
                return null;
            }

            lock (syncRoot)
            {
                return GetProfile(game)?.InputDeviceId;
            }
        }

        public GameAudioProfile GetProfile(Game game)
        {
            if (game == null)
            {
                return null;
            }

            lock (syncRoot)
            {
                return profiles.TryGetValue(game.Id, out var profile) ? profile : null;
            }
        }

        public Dictionary<Guid, GameAudioProfile> GetProfilesSnapshot()
        {
            lock (syncRoot)
            {
                return profiles.ToDictionary(a => a.Key, a => CloneProfile(a.Value));
            }
        }

        public void ReplaceProfiles(Dictionary<Guid, GameAudioProfile> importedProfiles)
        {
            lock (syncRoot)
            {
                profiles = (importedProfiles ?? new Dictionary<Guid, GameAudioProfile>())
                    .Where(a => !IsEmpty(a.Value))
                    .ToDictionary(a => a.Key, a => CloneProfile(a.Value));
                Save();
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
                if (!profiles.TryGetValue(game.Id, out var profile))
                {
                    profile = new GameAudioProfile();
                }

                profile.DeviceId = deviceId;

                if (IsEmpty(profile))
                {
                    profiles.Remove(game.Id);
                }
                else
                {
                    profiles[game.Id] = profile;
                }

                Save();
            }
        }

        public void SetInputDevice(Game game, string deviceId)
        {
            if (game == null)
            {
                return;
            }

            lock (syncRoot)
            {
                if (!profiles.TryGetValue(game.Id, out var profile))
                {
                    profile = new GameAudioProfile();
                }

                profile.InputDeviceId = deviceId;

                if (IsEmpty(profile))
                {
                    profiles.Remove(game.Id);
                }
                else
                {
                    profiles[game.Id] = profile;
                }

                Save();
            }
        }

        public void SetSpatialSoundMode(Game game, string spatialSoundMode)
        {
            if (game == null)
            {
                return;
            }

            lock (syncRoot)
            {
                if (!profiles.TryGetValue(game.Id, out var profile))
                {
                    profile = new GameAudioProfile();
                }

                profile.SpatialSoundMode = spatialSoundMode;

                if (IsEmpty(profile))
                {
                    profiles.Remove(game.Id);
                }
                else
                {
                    profiles[game.Id] = profile;
                }

                Save();
            }
        }

        public void SetGameVolumePercent(Game game, int? volumePercent)
        {
            if (game == null)
            {
                return;
            }

            lock (syncRoot)
            {
                if (!profiles.TryGetValue(game.Id, out var profile))
                {
                    profile = new GameAudioProfile();
                }

                profile.GameVolumePercent = volumePercent.HasValue
                    ? Math.Max(0, Math.Min(100, volumePercent.Value))
                    : (int?)null;

                if (IsEmpty(profile))
                {
                    profiles.Remove(game.Id);
                }
                else
                {
                    profiles[game.Id] = profile;
                }

                Save();
            }
        }

        public void ClearProfile(Game game)
        {
            if (game == null)
            {
                return;
            }

            lock (syncRoot)
            {
                profiles.Remove(game.Id);
                Save();
            }
        }

        private void Load()
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            if (Serialization.TryFromJsonFile<Dictionary<Guid, GameAudioProfile>>(filePath, out var loadedProfiles))
            {
                profiles = loadedProfiles ?? new Dictionary<Guid, GameAudioProfile>();
                return;
            }

            if (Serialization.TryFromJsonFile<Dictionary<Guid, string>>(filePath, out var legacyProfiles))
            {
                profiles = new Dictionary<Guid, GameAudioProfile>();
                foreach (var profile in legacyProfiles ?? new Dictionary<Guid, string>())
                {
                    profiles[profile.Key] = new GameAudioProfile
                    {
                        DeviceId = profile.Value
                    };
                }
            }
        }

        private void Save()
        {
            File.WriteAllText(filePath, Serialization.ToJson(profiles, true));
        }

        private static bool IsEmpty(GameAudioProfile profile)
        {
            return profile == null ||
                string.IsNullOrWhiteSpace(profile.DeviceId) &&
                string.IsNullOrWhiteSpace(profile.InputDeviceId) &&
                string.IsNullOrWhiteSpace(profile.SpatialSoundMode) &&
                !profile.GameVolumePercent.HasValue;
        }

        private static GameAudioProfile CloneProfile(GameAudioProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            return new GameAudioProfile
            {
                DeviceId = profile.DeviceId,
                InputDeviceId = profile.InputDeviceId,
                SpatialSoundMode = profile.SpatialSoundMode,
                GameVolumePercent = profile.GameVolumePercent
            };
        }
    }
}
