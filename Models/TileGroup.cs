using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinLauncher.Models
{
    public class TileGroup : INotifyPropertyChanged
    {
        private string _groupTitle;
        public string GroupTitle { get => _groupTitle; set { _groupTitle = value; OnPropertyChanged(); } }

        private double _groupHeight = 150;
        public double GroupHeight { get => _groupHeight; set { _groupHeight = value; OnPropertyChanged(); } }

        public ObservableCollection<AppItem> Tiles { get; set; } = new ObservableCollection<AppItem>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}