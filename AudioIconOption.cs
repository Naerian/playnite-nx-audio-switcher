namespace PlayniteAudioSwitcher
{
    public sealed class AudioIconOption
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Glyph { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Glyph) ? Name : $"{Glyph} {Name}";
    }
}
