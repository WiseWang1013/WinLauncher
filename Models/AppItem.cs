using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WinLauncher.Models
{
    public class AppItem : INotifyPropertyChanged
    {
        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        public string FirstLetter => string.IsNullOrEmpty(Name) ? "#" : (char.IsLetter(Name[0]) ? Name[0].ToString().ToUpper() : "#");
        public string Path { get; set; }

        private ImageSource _icon;
        public ImageSource Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }

        private string _tileSize = "Medium";
        public string TileSize { get => _tileSize; set { _tileSize = value; OnPropertyChanged(); } }

        private string _tileColor = "#0078D7";
        public string TileColor { get => _tileColor; set { _tileColor = value; OnPropertyChanged(); } }

        public TileGroup ParentGroup { get; set; }

        private double _x;
        public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
        private double _y;
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
        private double _width = 104;
        public double Width { get => _width; set { _width = value; OnPropertyChanged(); } }
        private double _height = 104;
        public double Height { get => _height; set { _height = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}