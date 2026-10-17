using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinLauncher.Helpers;
using WinLauncher.Models;

namespace WinLauncher
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<AppItem> SystemApps { get; set; }
        public ObservableCollection<TileGroup> TileGroups { get; set; }
        private ObservableCollection<AppItem> _metroAllAppsSource = new ObservableCollection<AppItem>();

        private Point _listDragStartPoint;
        private Point _tileDragStartPoint;
        private AppItem _draggedTile;

        private const double CellSize = 56;
        private const int Columns = 6;
        private string[] _tileColors = { "#0078D7", "#E81123", "#005A9E", "#10893E", "#FFB900", "#5C2D91", "#FF8C00", "#00B294", "#8E44AD" };
        private Random _rand = new Random();

        // 记录当前主题
        private string _currentThemeMode = "Dark";

        public MainWindow()
        {
            InitializeComponent();

            SystemApps = new ObservableCollection<AppItem>();
            TileGroups = new ObservableCollection<TileGroup>();

            ICollectionView view = CollectionViewSource.GetDefaultView(SystemApps);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppItem.FirstLetter)));
            lstApps.ItemsSource = view;

            metroTileControl.ItemsSource = TileGroups.Count > 0 ? TileGroups[0].Tiles : new ObservableCollection<AppItem>();
            allAppsMetroControl.ItemsSource = _metroAllAppsSource;

            LoadApps();
            LoadUserAvatar();
            GenerateLetters();

            // 默认深色主题，但稍后会被 LoadConfig 覆盖
            SetThemeColors("Dark", false);

            // 加载本地配置
            LoadConfig();

            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => txtTime.Text = DateTime.Now.ToString("HH:mm  yyyy/MM/dd");
            timer.Start();
            txtTime.Text = DateTime.Now.ToString("HH:mm  yyyy/MM/dd");
        }

        // ==================== 配置持久化核心逻辑 ====================
        private string GetConfigPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinLauncher");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "config.json");
        }

        private void SaveConfig()
        {
            try
            {
                var config = new AppConfigDTO
                {
                    Groups = new List<TileGroupDTO>(),
                    ThemeMode = _currentThemeMode,
                    MetroBgColor = (this.Resources["MetroBgBrush"] as SolidColorBrush)?.Color.ToString()
                };

                foreach (var group in TileGroups)
                {
                    var groupDto = new TileGroupDTO { GroupTitle = group.GroupTitle, Tiles = new List<AppTileDTO>() };
                    foreach (var tile in group.Tiles)
                    {
                        groupDto.Tiles.Add(new AppTileDTO
                        {
                            Name = tile.Name,
                            Path = tile.Path,
                            TileSize = tile.TileSize,
                            TileColor = tile.TileColor
                        });
                    }
                    config.Groups.Add(groupDto);
                }

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(GetConfigPath(), json);
            }
            catch { /* 忽略保存异常，防止影响用户操作 */ }
        }

        private void LoadConfig()
        {
            string path = GetConfigPath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfigDTO>(json);
                    if (config != null)
                    {
                        if (!string.IsNullOrEmpty(config.ThemeMode)) SetThemeColors(config.ThemeMode, false);
                        if (!string.IsNullOrEmpty(config.MetroBgColor)) this.Resources["MetroBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(config.MetroBgColor));

                        TileGroups.Clear();
                        foreach (var groupDto in config.Groups)
                        {
                            var group = new TileGroup { GroupTitle = groupDto.GroupTitle, Tiles = new ObservableCollection<AppItem>() };
                            foreach (var tileDto in groupDto.Tiles)
                            {
                                group.Tiles.Add(new AppItem
                                {
                                    Name = tileDto.Name,
                                    Path = tileDto.Path,
                                    TileSize = tileDto.TileSize,
                                    TileColor = tileDto.TileColor,
                                    Icon = IconHelper.GetIconWithoutArrow(tileDto.Path)
                                });
                            }
                            TileGroups.Add(group);
                        }
                    }
                }
                catch { /* 配置损坏则重新开始 */ }
            }

            if (TileGroups.Count == 0)
            {
                TileGroups.Add(new TileGroup { GroupTitle = "实用工具", Tiles = new ObservableCollection<AppItem>() });
            }

            tileGroupsControl.ItemsSource = TileGroups;
            metroTileControl.ItemsSource = TileGroups[0].Tiles;

            // 加载完成后排版，但不立刻保存
            RecalculateLayout(false);
        }

        // ==================== 初始数据加载 ====================
        private void LoadApps()
        {
            SystemApps.Clear();
            _metroAllAppsSource.Clear();
            string[] paths = {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) + @"\Programs",
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + @"\Programs"
            };

            foreach (var path in paths)
            {
                if (!Directory.Exists(path)) continue;
                try
                {
                    var files = Directory.GetFiles(path, "*.lnk", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        var item = new AppItem { Name = name, Path = file, Icon = IconHelper.GetIconWithoutArrow(file) };
                        SystemApps.Add(item);
                        _metroAllAppsSource.Add(item);
                    }
                }
                catch { }
            }
        }

        private void LoadUserAvatar()
        {
            try
            {
                string avatarPath = SystemHelper.GetCurrentUserPicturePath();
                if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    imgUserAvatar.Source = bitmap;
                    imgMetroAvatar.Source = bitmap;
                    ellipseAvatarBg.Visibility = Visibility.Collapsed;
                    ellipseMetroAvatarBg.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        // ==================== 动态主题与背景切换 ====================
        private void SetThemeColors(string mode, bool save = true)
        {
            _currentThemeMode = mode;
            if (mode == "Dark")
            {
                this.Resources["WindowBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"));
                this.Resources["PanelBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525"));
                this.Resources["TextMainBrush"] = new SolidColorBrush(Colors.White);
                this.Resources["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
                this.Resources["SearchBoxBgBrush"] = new SolidColorBrush(Colors.White);
                this.Resources["SearchBoxFgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111111"));
            }
            else
            {
                this.Resources["WindowBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3"));
                this.Resources["PanelBgBrush"] = new SolidColorBrush(Colors.White);
                this.Resources["TextMainBrush"] = new SolidColorBrush(Colors.Black);
                this.Resources["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
                this.Resources["SearchBoxBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"));
                this.Resources["SearchBoxFgBrush"] = new SolidColorBrush(Colors.White);
            }
            if (save) SaveConfig();
        }

        private void SetDarkTheme_Click(object sender, RoutedEventArgs e) => SetThemeColors("Dark");
        private void SetLightTheme_Click(object sender, RoutedEventArgs e) => SetThemeColors("Light");

        private void MetroBg_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                this.Resources["MetroBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                SaveConfig();
            }
        }

        // ==================== 应用启动与失效检测 ====================
        private void TileClick(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AppItem app)
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = app.Path, UseShellExecute = true });
                }
                catch
                {
                    // 启动失败，检测是不是因为文件被删了
                    bool isValid = true;
                    string realPath = app.Path;
                    if (app.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        realPath = IconHelper.GetRealPath(app.Path);
                    }

                    if (!File.Exists(realPath) && !Directory.Exists(realPath))
                    {
                        isValid = false;
                    }

                    if (!isValid)
                    {
                        var result = MessageBox.Show($"找不到该应用，可能已被卸载或移动。\n是否将其从开始屏幕中移除？\n\n应用名称：{app.Name}",
                                                     "无法启动应用", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            if (app.ParentGroup != null)
                            {
                                app.ParentGroup.Tiles.Remove(app);
                                if (app.ParentGroup.Tiles.Count == 0 && TileGroups.Count > 1)
                                {
                                    TileGroups.Remove(app.ParentGroup);
                                }
                                RecalculateLayout(); // 内部包含 SaveConfig()
                                tileGroupsControl.UpdateLayout();
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show($"启动应用时发生错误：{app.Name}\n可能由于权限不足或快捷方式损坏。", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstApps.SelectedItem is AppItem app)
            {
                try { Process.Start(new ProcessStartInfo { FileName = app.Path, UseShellExecute = true }); } catch { }
            }
        }

        // ==================== 搜索与界面交互 ====================
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string keyword = txtSearch.Text.Trim().ToLower();
            var view = CollectionViewSource.GetDefaultView(SystemApps);
            if (string.IsNullOrEmpty(keyword))
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = obj =>
                {
                    if (obj is AppItem app)
                        return app.Name.ToLower().Contains(keyword);
                    return false;
                };
            }
        }

        private void ToggleSettingsMenu(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = true;

        private void SwitchToWin10(object sender, RoutedEventArgs e)
        {
            pnlLeft.Visibility = Visibility.Visible;
            colLeftPanel.Width = new GridLength(320);
            colSplitter.Width = new GridLength(6);
            gridSplitter.Visibility = Visibility.Visible;
            pnlRight10.Visibility = Visibility.Visible;
            pnlRight8.Visibility = Visibility.Collapsed;
            SettingsPopup.IsOpen = false;
            RecalculateLayout();
        }

        private void SwitchToWin8(object sender, RoutedEventArgs e)
        {
            pnlLeft.Visibility = Visibility.Collapsed;
            colLeftPanel.Width = new GridLength(0);
            colSplitter.Width = new GridLength(0);
            gridSplitter.Visibility = Visibility.Collapsed;
            pnlRight10.Visibility = Visibility.Collapsed;
            pnlRight8.Visibility = Visibility.Visible;
            pnlAllAppsView.Visibility = Visibility.Collapsed;
            SettingsPopup.IsOpen = false;
        }

        private void ToggleAppsView(object sender, RoutedEventArgs e)
        {
            if (pnlAllAppsView.Visibility == Visibility.Collapsed)
            {
                pnlAllAppsView.Visibility = Visibility.Visible;
                btnDownArrow.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlAllAppsView.Visibility = Visibility.Collapsed;
                btnDownArrow.Visibility = Visibility.Visible;
            }
        }

        private void MetroUserMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var lockItem = new MenuItem { Header = "锁定" }; lockItem.Click += (s, args) => SystemHelper.Lock();
            var logoffItem = new MenuItem { Header = "注销" }; logoffItem.Click += (s, args) => SystemHelper.Logoff();
            var sleepItem = new MenuItem { Header = "睡眠" }; sleepItem.Click += (s, args) => SystemHelper.Sleep();
            var shutdownItem = new MenuItem { Header = "关机" }; shutdownItem.Click += (s, args) => SystemHelper.Shutdown();
            var restartItem = new MenuItem { Header = "重启" }; restartItem.Click += (s, args) => SystemHelper.Restart();

            menu.Items.Add(lockItem); menu.Items.Add(logoffItem); menu.Items.Add(new Separator());
            menu.Items.Add(sleepItem); menu.Items.Add(shutdownItem); menu.Items.Add(restartItem);
            menu.PlacementTarget = (Button)sender;
            menu.IsOpen = true;
        }

        // ==================== 拖拽网格算法 ====================
        private void RecalculateLayout(bool save = true)
        {
            foreach (var group in TileGroups)
            {
                var grid = new bool[200, Columns];
                double maxY = 0;

                foreach (var item in group.Tiles)
                {
                    item.ParentGroup = group;
                    int colSpan = 2, rowSpan = 2;
                    if (item.TileSize == "Small") { colSpan = 1; rowSpan = 1; }
                    else if (item.TileSize == "Wide") { colSpan = 4; rowSpan = 2; }
                    else if (item.TileSize == "Large") { colSpan = 4; rowSpan = 4; }

                    int r = 0, c = 0;
                    bool found = false;
                    for (r = 0; r < 200; r++)
                    {
                        for (c = 0; c <= Columns - colSpan; c++)
                        {
                            if (IsAreaFree(grid, r, c, colSpan, rowSpan)) { found = true; break; }
                        }
                        if (found) break;
                    }

                    for (int i = 0; i < rowSpan; i++)
                        for (int j = 0; j < colSpan; j++)
                            grid[r + i, c + j] = true;

                    item.X = c * CellSize + 4; item.Y = r * CellSize + 4;
                    item.Width = colSpan * CellSize - 8; item.Height = rowSpan * CellSize - 8;
                    if (item.Y + item.Height > maxY) maxY = item.Y + item.Height;
                }
                group.GroupHeight = maxY + 10;
            }
            if (save) SaveConfig();
        }

        private bool IsAreaFree(bool[,] grid, int r, int c, int colSpan, int rowSpan)
        {
            for (int i = 0; i < rowSpan; i++)
                for (int j = 0; j < colSpan; j++)
                    if (grid[r + i, c + j]) return false;
            return true;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("AppFormat") || e.Data.GetDataPresent("TileFormat"))
            {
                e.Effects = DragDropEffects.Copy; e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None; e.Handled = true;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("AppFormat"))
            {
                var app = e.Data.GetData("AppFormat") as AppItem;
                if (app != null && !TileGroups.Any(g => g.Tiles.Any(x => x.Name == app.Name)))
                {
                    var newTile = new AppItem { Name = app.Name, Path = app.Path, Icon = app.Icon, TileColor = _tileColors[_rand.Next(_tileColors.Length)] };
                    TileGroups[0].Tiles.Add(newTile);
                    RecalculateLayout();
                }
            }
        }

        private void LstApps_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _listDragStartPoint = e.GetPosition(null);
        private void LstApps_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Vector diff = _listDragStartPoint - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (lstApps.SelectedItem is AppItem selectedApp)
                        DragDrop.DoDragDrop(lstApps, new DataObject("AppFormat", selectedApp), DragDropEffects.Copy);
                }
            }
        }

        private void Tile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _tileDragStartPoint = e.GetPosition(null);
            _draggedTile = (sender as FrameworkElement)?.DataContext as AppItem;
        }

        private void Tile_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedTile != null)
            {
                Vector diff = _tileDragStartPoint - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("TileFormat", _draggedTile), DragDropEffects.Move);
                }
            }
        }

        private void Group_Drop(object sender, DragEventArgs e)
        {
            var targetItemsControl = sender as ItemsControl;
            var targetGroup = targetItemsControl?.DataContext as TileGroup;
            if (targetGroup == null) return;
            Point mousePos = e.GetPosition(targetItemsControl);

            if (e.Data.GetDataPresent("AppFormat"))
            {
                var app = e.Data.GetData("AppFormat") as AppItem;
                if (app != null && !TileGroups.Any(g => g.Tiles.Any(x => x.Name == app.Name)))
                {
                    var newTile = new AppItem { Name = app.Name, Path = app.Path, Icon = app.Icon, TileColor = _tileColors[_rand.Next(_tileColors.Length)] };
                    int index = GetInsertIndex(targetGroup, mousePos);
                    targetGroup.Tiles.Insert(index, newTile);
                    RecalculateLayout();
                }
            }
            else if (e.Data.GetDataPresent("TileFormat"))
            {
                var sourceTile = e.Data.GetData("TileFormat") as AppItem;
                if (sourceTile != null)
                {
                    var sourceGroup = sourceTile.ParentGroup;
                    if (sourceGroup != null)
                    {
                        int index = GetInsertIndex(targetGroup, mousePos);
                        if (sourceGroup == targetGroup)
                        {
                            int oldIndex = targetGroup.Tiles.IndexOf(sourceTile);
                            if (index > oldIndex) index--;
                            if (oldIndex != index) targetGroup.Tiles.Move(oldIndex, index);
                        }
                        else
                        {
                            sourceGroup.Tiles.Remove(sourceTile);
                            targetGroup.Tiles.Insert(index, sourceTile);
                        }
                        RecalculateLayout();
                    }
                }
            }
        }

        private int GetInsertIndex(TileGroup group, Point mousePos)
        {
            if (group.Tiles.Count == 0) return 0;
            int index = group.Tiles.Count;
            double minDist = double.MaxValue;
            for (int i = 0; i < group.Tiles.Count; i++)
            {
                var t = group.Tiles[i];
                double cx = t.X + t.Width / 2; double cy = t.Y + t.Height / 2;
                double dist = (mousePos.X - cx) * (mousePos.X - cx) + (mousePos.Y - cy) * (mousePos.Y - cy);
                if (dist < minDist)
                {
                    minDist = dist;
                    if (mousePos.X < cx || mousePos.Y < cy) index = i;
                    else index = i + 1;
                }
            }
            return index;
        }

        private void NewGroup_Drop(object sender, DragEventArgs e)
        {
            var newGroup = new TileGroup { GroupTitle = "新建分组", Tiles = new ObservableCollection<AppItem>() };
            if (e.Data.GetDataPresent("TileFormat"))
            {
                var sourceTile = e.Data.GetData("TileFormat") as AppItem;
                if (sourceTile != null) { sourceTile.ParentGroup?.Tiles.Remove(sourceTile); newGroup.Tiles.Add(sourceTile); }
            }
            else if (e.Data.GetDataPresent("AppFormat"))
            {
                var app = e.Data.GetData("AppFormat") as AppItem;
                if (app != null) newGroup.Tiles.Add(new AppItem { Name = app.Name, Path = app.Path, Icon = app.Icon, TileColor = _tileColors[_rand.Next(_tileColors.Length)] });
            }
            if (newGroup.Tiles.Count > 0) { TileGroups.Add(newGroup); RecalculateLayout(); }
        }

        private void GenerateLetters()
        {
            var letters = new System.Collections.Generic.List<string>();
            for (char c = 'A'; c <= 'Z'; c++) letters.Add(c.ToString());
            letters.Add("#");
            letterPanel.ItemsSource = letters;
        }

        private void Letter_Click(object sender, RoutedEventArgs e)
        {
            string letter = (sender as Button).Content.ToString();
            var item = SystemApps.FirstOrDefault(x => x.FirstLetter == letter);
            if (item != null) lstApps.ScrollIntoView(item);
        }

        private AppItem GetMenuAppItem(object sender) => (sender as MenuItem)?.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement fe ? fe.DataContext as AppItem : null;

        private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            var app = GetMenuAppItem(sender);
            if (app != null)
            {
                try { string realPath = IconHelper.GetRealPath(app.Path); Process.Start(new ProcessStartInfo { FileName = realPath, UseShellExecute = true, Verb = "runas" }); }
                catch { MessageBox.Show("以管理员身份运行失败！"); }
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            var app = GetMenuAppItem(sender);
            if (app != null)
            {
                try
                {
                    string realPath = IconHelper.GetRealPath(app.Path);
                    if (File.Exists(realPath)) Process.Start("explorer.exe", $"/select,\"{realPath}\"");
                    else Process.Start("explorer.exe", $"/select,\"{app.Path}\"");
                }
                catch { }
            }
        }

        private void SetTileSizeSmall(object sender, RoutedEventArgs e) { var app = GetMenuAppItem(sender); if (app != null) { app.TileSize = "Small"; RecalculateLayout(); } }
        private void SetTileSizeMedium(object sender, RoutedEventArgs e) { var app = GetMenuAppItem(sender); if (app != null) { app.TileSize = "Medium"; RecalculateLayout(); } }
        private void SetTileSizeWide(object sender, RoutedEventArgs e) { var app = GetMenuAppItem(sender); if (app != null) { app.TileSize = "Wide"; RecalculateLayout(); } }
        private void SetTileSizeLarge(object sender, RoutedEventArgs e) { var app = GetMenuAppItem(sender); if (app != null) { app.TileSize = "Large"; RecalculateLayout(); } }

        private void UnpinTile(object sender, RoutedEventArgs e)
        {
            var app = GetMenuAppItem(sender);
            if (app != null)
            {
                var group = app.ParentGroup;
                group?.Tiles.Remove(app);
                if (group != null && group.Tiles.Count == 0 && TileGroups.Count > 1) TileGroups.Remove(group);
                RecalculateLayout();
                tileGroupsControl.UpdateLayout();
            }
        }

        // ==================== 左侧系统按钮与窗口控制 ====================
        private void UserAccount_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var lockItem = new MenuItem { Header = "锁定" }; lockItem.Click += (s, args) => SystemHelper.Lock();
            var logoffItem = new MenuItem { Header = "注销" }; logoffItem.Click += (s, args) => SystemHelper.Logoff();
            menu.Items.Add(lockItem); menu.Items.Add(logoffItem);
            menu.PlacementTarget = (Button)sender; menu.IsOpen = true;
        }

        private void OpenDocuments_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenFolder(Environment.SpecialFolder.MyDocuments);
        private void OpenPictures_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenFolder(Environment.SpecialFolder.MyPictures);
        private void OpenSettings_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenSettings();

        private void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var sleep = new MenuItem { Header = "睡眠" }; sleep.Click += (s, args) => SystemHelper.Sleep();
            var shutdown = new MenuItem { Header = "关机" }; shutdown.Click += (s, args) => SystemHelper.Shutdown();
            var restart = new MenuItem { Header = "重启" }; restart.Click += (s, args) => SystemHelper.Restart();
            menu.Items.Add(sleep); menu.Items.Add(shutdown); menu.Items.Add(restart);
            menu.PlacementTarget = (Button)sender; menu.IsOpen = true;
        }

        private void PinToTaskbar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.DataContext is AppItem app)
            {
                try
                {
                    Type shellType = Type.GetTypeFromProgID("Shell.Application");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic folder = shell.Namespace("shell:::{4234d49b-0245-4df3-b780-3893943456e1}");
                    if (folder != null)
                    {
                        dynamic item = folder.ParseName(app.Name);
                        if (item != null) item.InvokeVerb("taskbarpin");
                    }
                }
                catch { }
            }
        }

        private void GroupTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && (sender as TextBlock)?.DataContext is TileGroup group)
            {
                var inputDialog = new Window { Width = 300, Height = 150, Title = "重命名", WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Brushes.DarkGray, WindowStyle = WindowStyle.ToolWindow };
                var stack = new StackPanel { Margin = new Thickness(20) };
                var tb = new TextBox { Text = group.GroupTitle, Height = 30, Margin = new Thickness(0, 0, 0, 20) };
                var btn = new Button { Content = "确定", Height = 30, Width = 80 };
                btn.Click += (s, args) => { group.GroupTitle = tb.Text; inputDialog.Close(); SaveConfig(); };
                stack.Children.Add(tb); stack.Children.Add(btn);
                inputDialog.Content = stack; inputDialog.ShowDialog();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized) { WindowState = WindowState.Normal; btnMaximize.Content = "□"; }
            else { WindowState = WindowState.Maximized; btnMaximize.Content = "❐"; }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) Maximize_Click(sender, e);
            else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }

    // ==================== 配置数据模型 DTO ====================
    public class AppConfigDTO
    {
        public List<TileGroupDTO> Groups { get; set; }
        public string ThemeMode { get; set; }
        public string MetroBgColor { get; set; }
    }

    public class TileGroupDTO
    {
        public string GroupTitle { get; set; }
        public List<AppTileDTO> Tiles { get; set; }
    }

    public class AppTileDTO
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string TileSize { get; set; }
        public string TileColor { get; set; }
    }
}