namespace PlayniteAudioSwitcher
{
    public sealed class AudioIconOption
    {
        private string geometryData;

        public string Id { get; set; }

        public string Name { get; set; }

        public string Glyph { get; set; }

        public string IconFileName { get; set; }

        public string GeometryData
        {
            get
            {
                if (geometryData == null && !string.IsNullOrWhiteSpace(IconFileName))
                {
                    geometryData = SvgIconGeometryLoader.GetPathData(IconFileName);
                }

                return geometryData;
            }
            set => geometryData = value;
        }

        public string DisplayName => Name;
    }
}
