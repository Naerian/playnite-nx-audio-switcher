namespace PlayniteAudioSwitcher
{
    public sealed class AudioDevice
    {
        private string settingsDisplayName;

        public string Id { get; set; }

        public string Name { get; set; }

        public bool IsDefault { get; set; }

        public string CustomName { get; set; }

        public string Icon { get; set; }

        public bool IsIconSuggested { get; set; }

        public bool IsVisible { get; set; } = true;

        public int? DefaultVolumePercent { get; set; }

        public string EffectiveName => string.IsNullOrWhiteSpace(CustomName) ? Name : CustomName;

        public string EffectiveIcon => string.IsNullOrWhiteSpace(Icon) ? string.Empty : Icon;

        public string DisplayName => IsDefault ? $"★ {EffectiveName}" : EffectiveName;

        public string TechnicalDisplayName => IsDefault ? $"★ {Name}" : Name;

        public string SettingsDisplayName
        {
            get => string.IsNullOrWhiteSpace(settingsDisplayName) ? TechnicalDisplayName : settingsDisplayName;
            set => settingsDisplayName = value;
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
