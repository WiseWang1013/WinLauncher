namespace WinLauncher.Models
{
    public class SkinModel
    {
        public string SkinName { get; set; }
        public string LayoutMode { get; set; } // "Win7", "Win8", "Win10", "Win11"

        public bool HasTiles { get; set; } = true;
        public double TileCornerRadius { get; set; } = 0;
        public string TileDefaultColor { get; set; } = "#0078D7";

        public string WindowBgColor { get; set; } = "#1A1A1A";
        public string PanelBgColor { get; set; } = "#252525";
        public string TextMainColor { get; set; } = "#FFFFFF";
        public string TextMutedColor { get; set; } = "#888888";
        public string ButtonBgColor { get; set; } = "#333333";
        public string ButtonFgColor { get; set; } = "#FFFFFF";

        public string AcrylicTintColor { get; set; } = "#101010";
        public byte AcrylicOpacity { get; set; } = 160;
    }
}