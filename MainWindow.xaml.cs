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
using Microsoft.Win32;
using Newtonsoft.Json;
using WinLauncher.Helpers;
using WinLauncher.Models;

namespace WinLauncher
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<AppItem> SystemApps { get; set; }
        public ObservableCollection<TileGroup> TileGroups { get; set; }
        public ObservableCollection<AppItem> AllTiles { get; set; } = new ObservableCollection<AppItem>();
        public ObservableCollection<SkinModel> ImportedSkins { get; set; } = new ObservableCollection<SkinModel>();

        private ObservableCollection<AppItem> _metroAllAppsSource = new ObservableCollection<AppItem>();
        private Point _listDragStartPoint, _tileDragStartPoint;
        private AppItem _draggedTile;
        private const double CellSize = 56;
        private const int Columns = 6;
        private string[] _tileColors = { "#0078D7", "#E81123", "#005A9E", "#10893E", "#FFB900", "#5C2D91", "#FF8C00", "#00B294" };
        private Random _rand = new Random();

        private string _currentThemeMode = "Dark";
        private string _currentLayoutMode = "Win10";

        // 触屏长按
        private DispatcherTimer _touchHoldTimer;
        private bool _isTouchHoldTriggered = false;
        private AppItem _currentTouchDragItem;
        private Point _touchStartPoint;

        public MainWindow()
        {
            InitializeComponent();

            SystemApps = new ObservableCollection<AppItem>();
            TileGroups = new ObservableCollection<TileGroup>();

            ICollectionView view = CollectionViewSource.GetDefaultView(SystemApps);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppItem.FirstLetter)));
            lstApps.ItemsSource = view;

            tileGroupsControl.ItemsSource = TileGroups;
            allAppsMetroControl.ItemsSource = _metroAllAppsSource;
            skinListControl.ItemsSource = ImportedSkins;

            _touchHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _touchHoldTimer.Tick += TouchHoldTimer_Tick;

            LoadApps();
            LoadUserAvatar();
            GenerateLetters();

            LoadConfig();
            SyncWin11Grid();
            SwitchToWin10(null, null); // 初始化默认状态

            // 【修复】全方位监听窗口状态，防止毛玻璃被系统“吃掉”
            this.SourceInitialized += (s, e) => CheckAndApplyCurrentStyle();
            this.Loaded += (s, e) => CheckAndApplyCurrentStyle();
            this.Activated += (s, e) => CheckAndApplyCurrentStyle();
            this.StateChanged += (s, e) => CheckAndApplyCurrentStyle();

            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => txtTime.Text = DateTime.Now.ToString("HH:mm  yyyy/MM/dd");
            timer.Start();
            txtTime.Text = DateTime.Now.ToString("HH:mm  yyyy/MM/dd");
        }

        // ==================== 核心检测与特效应用 ====================
        private void CheckAndApplyCurrentStyle()
        {
            if (new System.Windows.Interop.WindowInteropHelper(this).Handle != IntPtr.Zero)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    switch (_currentLayoutMode)
                    {
                        case "Win7":
                            AcrylicHelper.EnableAcrylic(this, 140, 0xB0D0E0); // Win7 增加透明度，冰蓝玻璃
                            break;
                        case "Win11":
                            if (_currentThemeMode == "Dark")
                                AcrylicHelper.EnableAcrylic(this, 130, 0x202020);
                            else
                                AcrylicHelper.EnableAcrylic(this, 130, 0xF0F0F0);
                            break;
                        case "Win10":
                        case "Win8":
                        default:
                            // 【关键修改】Alpha 值设为 120（约 47% 不透明度），让桌面背景清晰可见
                            if (_currentThemeMode == "Dark")
                                AcrylicHelper.EnableAcrylic(this, 120, 0x101010);
                            else
                                AcrylicHelper.EnableAcrylic(this, 120, 0xF0F0F0);
                            break;
                    }
                }), DispatcherPriority.Render);
            }
        }

        // ==================== 全局色彩与文字逻辑 ====================
        private void SetThemeColors(string mode, bool save = true)
        {
            _currentThemeMode = mode;

            if (mode == "Dark")
            {
                // 【关键修改】WPF 背景色极度透明，只留一层极淡的黑影，不遮挡底层 API 效果
                this.Resources["WindowBgBrush"] = new SolidColorBrush(Color.FromArgb(10, 0, 0, 0));
                this.Resources["PanelBgBrush"] = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                this.Resources["TextMainBrush"] = new SolidColorBrush(Colors.White);
                this.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
                this.Resources["SearchBoxBgBrush"] = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                this.Resources["SearchBoxFgBrush"] = new SolidColorBrush(Colors.White);
                this.Resources["ButtonBgBrush"] = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
                this.Resources["ButtonFgBrush"] = new SolidColorBrush(Colors.Black);
            }
            else
            {
                this.Resources["WindowBgBrush"] = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255));
                this.Resources["PanelBgBrush"] = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                this.Resources["TextMainBrush"] = new SolidColorBrush(Colors.Black);
                this.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
                this.Resources["SearchBoxBgBrush"] = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
                this.Resources["SearchBoxFgBrush"] = new SolidColorBrush(Colors.Black);
                this.Resources["ButtonBgBrush"] = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0));
                this.Resources["ButtonFgBrush"] = new SolidColorBrush(Colors.White);
            }

            SyncWin11Grid();
            CheckAndApplyCurrentStyle();
            if (save) SaveConfig();
        }

        private void SetDarkTheme_Click(object sender, RoutedEventArgs e) => SetThemeColors("Dark");
        private void SetLightTheme_Click(object sender, RoutedEventArgs e) => SetThemeColors("Light");

        // ==================== 模式隔离切换 ====================
        private void SwitchToWin7Or11(string mode)
        {
            _currentLayoutMode = mode;
            pnlLeft.Visibility = Visibility.Visible;
            colLeftPanel.Width = new GridLength(320);
            colSplitter.Width = new GridLength(6);
            gridSplitter.Visibility = Visibility.Visible;
            pnlRight10.Visibility = Visibility.Collapsed;
            pnlRight8.Visibility = Visibility.Collapsed;
            pnlWin7.Visibility = mode == "Win7" ? Visibility.Visible : Visibility.Collapsed;
            pnlWin11.Visibility = mode == "Win11" ? Visibility.Visible : Visibility.Collapsed;

            if (mode == "Win7") SetThemeColors("Light", false);
            else SetThemeColors(_currentThemeMode, false);
        }

        private void SwitchToWin10(object sender, RoutedEventArgs e)
        {
            _currentLayoutMode = "Win10";
            pnlLeft.Visibility = Visibility.Visible;
            colLeftPanel.Width = new GridLength(320);
            colSplitter.Width = new GridLength(6);
            gridSplitter.Visibility = Visibility.Visible;
            pnlRight10.Visibility = Visibility.Visible;
            pnlWin7.Visibility = Visibility.Collapsed;
            pnlWin11.Visibility = Visibility.Collapsed;
            pnlRight8.Visibility = Visibility.Collapsed;
            if (SettingsPopup != null) SettingsPopup.IsOpen = false;

            SetThemeColors(_currentThemeMode, false);
            RecalculateLayout();
        }

        private void SwitchToWin8(object sender, RoutedEventArgs e)
        {
            _currentLayoutMode = "Win8";
            SyncAllTiles();
            pnlLeft.Visibility = Visibility.Collapsed;
            colLeftPanel.Width = new GridLength(0);
            colSplitter.Width = new GridLength(0);
            gridSplitter.Visibility = Visibility.Collapsed;
            pnlRight10.Visibility = Visibility.Collapsed;
            pnlWin7.Visibility = Visibility.Collapsed;
            pnlWin11.Visibility = Visibility.Collapsed;
            pnlRight8.Visibility = Visibility.Visible;
            pnlAllAppsView.Visibility = Visibility.Collapsed;
            if (SettingsPopup != null) SettingsPopup.IsOpen = false;

            // Win8 依然保持较深的背景，因为它是全屏纯色模式
            this.Resources["WindowBgBrush"] = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0));
            CheckAndApplyCurrentStyle();
        }

        private void SwitchToWin7(object sender, RoutedEventArgs e)
        {
            SwitchToWin7Or11("Win7");
            if (SettingsPopup != null) SettingsPopup.IsOpen = false;
        }

        private void SwitchToWin11(object sender, RoutedEventArgs e)
        {
            SwitchToWin7Or11("Win11");
            if (SettingsPopup != null) SettingsPopup.IsOpen = false;
        }

        // ==================== 配置管理 ====================
        private void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "配置文件 (*.json)|*.json", FileName = "WinLauncher_Config_Backup.json", Title = "导出当前配置" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    SaveConfig();
                    string sourcePath = GetConfigPath();
                    if (!File.Exists(sourcePath)) SaveConfig();
                    File.Copy(sourcePath, dialog.FileName, true);
                    SettingsPopup.IsOpen = false;
                    MessageBox.Show("配置导出成功！", "成功");
                }
                catch (Exception ex) { MessageBox.Show($"导出失败：{ex.Message}", "错误"); }
            }
        }

        private void ResetConfig_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要还原所有设置吗？\n\n此操作将清空你所有的磁贴布局、自定义分组、颜色和主题设置，并恢复到初始状态，且无法撤销！",
                                         "⚠️ 警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string path = GetConfigPath();
                    if (File.Exists(path)) File.Delete(path);

                    TileGroups.Clear();
                    ImportedSkins.Clear();
                    AllTiles.Clear();

                    TileGroups.Add(new TileGroup { GroupTitle = "实用工具", Tiles = new ObservableCollection<AppItem>() });
                    this.Resources["MetroBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A0A0A"));
                    this.Resources["TileCornerRadius"] = new CornerRadius(0);

                    SyncAllTiles();
                    RecalculateLayout(false);
                    SetThemeColors("Dark", false);
                    SwitchToWin10(null, null);

                    SettingsPopup.IsOpen = false;
                    MessageBox.Show("已成功恢复至初始状态！", "还原完成");
                }
                catch (Exception ex) { MessageBox.Show($"还原失败：{ex.Message}", "错误"); }
            }
        }

        private string GetConfigPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        private void SaveConfig()
        {
            try
            {
                var config = new AppConfigDTO
                {
                    Groups = new List<TileGroupDTO>(),
                    ThemeMode = _currentThemeMode,
                    MetroBgColor = (this.Resources["MetroBgBrush"] as SolidColorBrush)?.Color.ToString(),
                    Skins = ImportedSkins.ToList()
                };

                foreach (var group in TileGroups)
                {
                    var groupDto = new TileGroupDTO { GroupTitle = group.GroupTitle, Tiles = new List<AppTileDTO>() };
                    foreach (var tile in group.Tiles)
                        groupDto.Tiles.Add(new AppTileDTO { Name = tile.Name, Path = tile.Path, TileSize = tile.TileSize, TileColor = tile.TileColor });
                    config.Groups.Add(groupDto);
                }
                File.WriteAllText(GetConfigPath(), JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            catch { }
        }

        private void LoadConfig()
        {
            string path = GetConfigPath();
            if (File.Exists(path))
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<AppConfigDTO>(File.ReadAllText(path));
                    if (config != null) ApplyConfig(config);
                }
                catch { }
            }

            if (TileGroups.Count == 0) TileGroups.Add(new TileGroup { GroupTitle = "实用工具", Tiles = new ObservableCollection<AppItem>() });
            SyncAllTiles();
            RecalculateLayout(false);
        }

        private void ApplyConfig(AppConfigDTO config)
        {
            if (!string.IsNullOrEmpty(config.ThemeMode)) SetThemeColors(config.ThemeMode, false);
            if (!string.IsNullOrEmpty(config.MetroBgColor)) this.Resources["MetroBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(config.MetroBgColor));

            if (config.Skins != null)
            {
                ImportedSkins.Clear();
                foreach (var skin in config.Skins) ImportedSkins.Add(skin);
            }

            TileGroups.Clear();
            foreach (var groupDto in config.Groups)
            {
                var group = new TileGroup { GroupTitle = groupDto.GroupTitle, Tiles = new ObservableCollection<AppItem>() };
                foreach (var tileDto in groupDto.Tiles)
                    group.Tiles.Add(new AppItem { Name = tileDto.Name, Path = tileDto.Path, TileSize = tileDto.TileSize, TileColor = tileDto.TileColor, Icon = IconHelper.GetIconWithoutArrow(tileDto.Path) });
                TileGroups.Add(group);
            }
        }

        private void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "配置文件 (*.json)|*.json", Title = "选择旧版 config.json 文件" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<AppConfigDTO>(File.ReadAllText(dialog.FileName));
                    if (config != null)
                    {
                        ApplyConfig(config);
                        SyncAllTiles();
                        RecalculateLayout(true);
                        SettingsPopup.IsOpen = false;
                        MessageBox.Show("配置导入成功！已自动保存到当前目录。", "成功");
                    }
                }
                catch { MessageBox.Show("导入失败，文件格式错误或已损坏！", "错误"); }
            }
        }

        // ==================== 数据同步与网格 ====================
        private void SyncAllTiles()
        {
            AllTiles.Clear();
            foreach (var group in TileGroups)
                foreach (var tile in group.Tiles)
                    AllTiles.Add(tile);
            metroTileControl.ItemsSource = AllTiles;
        }

        private void SyncWin11Grid()
        {
            if (win11AppsGrid == null) return;

            win11AppsGrid.Children.Clear();
            Brush txtBrush = (Brush)this.Resources["TextMainBrush"];

            if (SystemApps.Count > 0)
            {
                foreach (var app in SystemApps.Take(30))
                {
                    var grid = new Grid { Width = 80, Height = 80, Margin = new Thickness(10) };
                    grid.Children.Add(new Image { Source = app.Icon, Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Top });
                    grid.Children.Add(new TextBlock { Text = app.Name, Foreground = txtBrush, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 11, MaxWidth = 75 });
                    win11AppsGrid.Children.Add(grid);
                }
            }
        }

        // ==================== 皮肤系统 ====================
        private void ImportSkin_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "皮肤文件 (*.json)|*.json", Title = "选择皮肤 JSON 文件" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var skin = JsonConvert.DeserializeObject<SkinModel>(File.ReadAllText(dialog.FileName));
                    if (skin != null && !string.IsNullOrEmpty(skin.SkinName))
                    {
                        ImportedSkins.Add(skin);
                        SaveConfig();
                        MessageBox.Show($"皮肤 '{skin.SkinName}' 导入成功！", "提示");
                    }
                }
                catch { MessageBox.Show("皮肤文件格式错误！", "错误"); }
            }
        }

        private void DeleteSkin_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.Tag is SkinModel skin)
            {
                ImportedSkins.Remove(skin);
                SaveConfig();
            }
        }

        private void ApplySkin_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is SkinModel skin)
            {
                this.Resources["WindowBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.WindowBgColor));
                this.Resources["PanelBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.PanelBgColor));
                this.Resources["TextMainBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.TextMainColor));
                this.Resources["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.TextMutedColor));
                this.Resources["ButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.ButtonBgColor));
                this.Resources["ButtonFgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.ButtonFgColor));
                this.Resources["TileCornerRadius"] = new CornerRadius(skin.TileCornerRadius);
                _tileColors[0] = skin.TileDefaultColor;

                if (!skin.HasTiles) SwitchToWin7Or11(skin.LayoutMode);
                else
                {
                    if (skin.LayoutMode == "Win8") SwitchToWin8(null, null);
                    else if (skin.LayoutMode == "Win10") SwitchToWin10(null, null);
                }

                SyncWin11Grid();
                SettingsPopup.IsOpen = false;
                MessageBox.Show($"已应用皮肤：{skin.SkinName}", "成功");
            }
        }

        private void ToggleSettingsMenu(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = true;

        private void MetroBg_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                this.Resources["MetroBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                SaveConfig();
            }
        }

        // ==================== 触屏长按拖拽 ====================
        private void TouchHoldTimer_Tick(object sender, EventArgs e)
        {
            _touchHoldTimer.Stop();
            _isTouchHoldTriggered = true;
        }

        private void LstApps_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            _isTouchHoldTriggered = false;
            _touchStartPoint = e.GetTouchPoint(null).Position;
            _currentTouchDragItem = (sender as ListBox)?.SelectedItem as AppItem;
            _touchHoldTimer.Start();
        }

        private void LstApps_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            _touchHoldTimer.Stop();
            _isTouchHoldTriggered = false;
        }

        private void LstApps_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (_isTouchHoldTriggered && _currentTouchDragItem != null)
            {
                DragDrop.DoDragDrop(lstApps, new DataObject("AppFormat", _currentTouchDragItem), DragDropEffects.Copy);
                _touchHoldTimer.Stop();
                _isTouchHoldTriggered = false;
                e.Handled = true;
            }
            else if (!_isTouchHoldTriggered)
            {
                Vector diff = _touchStartPoint - e.GetTouchPoint(null).Position;
                if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10) _touchHoldTimer.Stop();
            }
        }

        private void Tile_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            _isTouchHoldTriggered = false;
            _touchStartPoint = e.GetTouchPoint(null).Position;
            _currentTouchDragItem = (sender as FrameworkElement)?.DataContext as AppItem;
            _touchHoldTimer.Start();
        }

        private void Tile_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            _touchHoldTimer.Stop();
            _isTouchHoldTriggered = false;
        }

        private void Tile_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (_isTouchHoldTriggered && _currentTouchDragItem != null)
            {
                DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("TileFormat", _currentTouchDragItem), DragDropEffects.Move);
                _touchHoldTimer.Stop();
                _isTouchHoldTriggered = false;
                e.Handled = true;
            }
            else if (!_isTouchHoldTriggered)
            {
                Vector diff = _touchStartPoint - e.GetTouchPoint(null).Position;
                if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10) _touchHoldTimer.Stop();
            }
        }

        // ==================== 鼠标拖拽 ====================
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
                    DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("TileFormat", _draggedTile), DragDropEffects.Move);
            }
        }

        // ==================== 拖放与网格算法 ====================
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = (e.Data.GetDataPresent("AppFormat") || e.Data.GetDataPresent("TileFormat")) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
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
                    SyncAllTiles();
                    RecalculateLayout();
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
                    SyncAllTiles();
                }
            }
            else if (e.Data.GetDataPresent("TileFormat"))
            {
                var sourceTile = e.Data.GetData("TileFormat") as AppItem;
                if (sourceTile != null && sourceTile.ParentGroup != null)
                {
                    int index = GetInsertIndex(targetGroup, mousePos);
                    if (sourceTile.ParentGroup == targetGroup)
                    {
                        int oldIndex = targetGroup.Tiles.IndexOf(sourceTile);
                        if (index > oldIndex) index--;
                        if (oldIndex != index) targetGroup.Tiles.Move(oldIndex, index);
                    }
                    else
                    {
                        sourceTile.ParentGroup.Tiles.Remove(sourceTile);
                        targetGroup.Tiles.Insert(index, sourceTile);
                    }
                    RecalculateLayout();
                    SyncAllTiles();
                }
            }
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
            if (newGroup.Tiles.Count > 0) { TileGroups.Add(newGroup); RecalculateLayout(); SyncAllTiles(); }
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

        // ==================== 系统与加载 ====================
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

        private void GenerateLetters()
        {
            var letters = new List<string>();
            for (char c = 'A'; c <= 'Z'; c++) letters.Add(c.ToString());
            letters.Add("#");
            letterPanel.ItemsSource = letters;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string keyword = txtSearch.Text.Trim().ToLower();
            var view = CollectionViewSource.GetDefaultView(SystemApps);
            if (string.IsNullOrEmpty(keyword)) view.Filter = null;
            else view.Filter = obj => { if (obj is AppItem app) return app.Name.ToLower().Contains(keyword); return false; };
        }

        private void Letter_Click(object sender, RoutedEventArgs e)
        {
            string letter = (sender as Button).Content.ToString();
            var item = SystemApps.FirstOrDefault(x => x.FirstLetter == letter);
            if (item != null) lstApps.ScrollIntoView(item);
        }

        private void TileClick(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AppItem app)
            {
                try { Process.Start(new ProcessStartInfo { FileName = app.Path, UseShellExecute = true }); }
                catch { MessageBox.Show("找不到目标，可能已被删除。", "错误"); }
            }
        }

        private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstApps.SelectedItem is AppItem app) try { Process.Start(new ProcessStartInfo { FileName = app.Path, UseShellExecute = true }); } catch { }
        }

        private AppItem GetMenuAppItem(object sender) => (sender as MenuItem)?.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement fe ? fe.DataContext as AppItem : null;
        private void SetTileSizeSmall(object sender, RoutedEventArgs e) { var app = GetMenuAppItem(sender); if (app != null) { app.TileSize = "Small"; RecalculateLayout(); SyncAllTiles(); } }
        private void SetTileSizeMedium(object sender, RoutedEventArgs e) { var app = GetMenuAppItem(sender); if (app != null) { app.TileSize = "Medium"; RecalculateLayout(); SyncAllTiles(); } }
        private void SetTileSizeWide(object sender, RoutedEventArgs e) { var app = GetMenuAppItem(sender); if (app != null) { app.TileSize = "Wide"; RecalculateLayout(); SyncAllTiles(); } }
        private void SetTileSizeLarge(object sender, RoutedEventArgs e) { var app = GetMenuAppItem(sender); if (app != null) { app.TileSize = "Large"; RecalculateLayout(); SyncAllTiles(); } }

        private void UnpinTile(object sender, RoutedEventArgs e)
        {
            var app = GetMenuAppItem(sender);
            if (app != null) { var group = app.ParentGroup; group?.Tiles.Remove(app); if (group != null && group.Tiles.Count == 0 && TileGroups.Count > 1) TileGroups.Remove(group); RecalculateLayout(); SyncAllTiles(); }
        }

        private void ToggleAppsView(object sender, RoutedEventArgs e)
        {
            if (pnlAllAppsView.Visibility == Visibility.Collapsed) { pnlAllAppsView.Visibility = Visibility.Visible; btnDownArrow.Visibility = Visibility.Collapsed; }
            else { pnlAllAppsView.Visibility = Visibility.Collapsed; btnDownArrow.Visibility = Visibility.Visible; }
        }

        private void MetroUserMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var lockItem = new MenuItem { Header = "锁定" }; lockItem.Click += (s, args) => SystemHelper.Lock();
            var logoffItem = new MenuItem { Header = "注销" }; logoffItem.Click += (s, args) => SystemHelper.Logoff();
            var sleepItem = new MenuItem { Header = "睡眠" }; sleepItem.Click += (s, args) => SystemHelper.Sleep();
            var shutdownItem = new MenuItem { Header = "关机" }; shutdownItem.Click += (s, args) => SystemHelper.Shutdown();
            var restartItem = new MenuItem { Header = "重启" }; restartItem.Click += (s, args) => SystemHelper.Restart();
            menu.Items.Add(lockItem); menu.Items.Add(logoffItem); menu.Items.Add(new Separator()); menu.Items.Add(sleepItem); menu.Items.Add(shutdownItem); menu.Items.Add(restartItem);
            menu.PlacementTarget = (Button)sender; menu.IsOpen = true;
        }

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
        private void OpenMusic_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenFolder(Environment.SpecialFolder.MyMusic);
        private void OpenComputer_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenFolder(Environment.SpecialFolder.MyComputer);
        private void OpenSettings_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenSettings();
        private void OpenDevices_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenSettings();
        private void OpenDefaultPrograms_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenSettings();
        private void OpenHelp_Click(object sender, RoutedEventArgs e) => MessageBox.Show("帮助和支持", "WinLauncher");

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
                    if (folder != null) { dynamic item = folder.ParseName(app.Name); if (item != null) item.InvokeVerb("taskbarpin"); }
                }
                catch { }
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.DataContext is AppItem app)
            {
                try
                {
                    string realPath = IconHelper.GetRealPath(app.Path);
                    if (File.Exists(realPath)) Process.Start("explorer.exe", $"/select,\"{realPath}\"");
                    else Process.Start("explorer.exe", $"/select,\"{app.Path}\"");
                }
                catch (Exception ex) { MessageBox.Show($"无法打开文件位置: {ex.Message}", "错误"); }
            }
        }

        private void ChangeTileColor_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement fe && fe.DataContext is AppItem app)
            {
                var colorWin = new Window
                {
                    Width = 320,
                    Height = 280,
                    Title = "选择磁贴颜色",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                    WindowStyle = WindowStyle.ToolWindow
                };
                var stack = new StackPanel { Margin = new Thickness(15) };
                stack.Children.Add(new TextBlock { Text = "Win10 经典预设色", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
                var wrap = new WrapPanel();
                string[] presets = { "#0078D7", "#E81123", "#005A9E", "#10893E", "#FFB900", "#5C2D91", "#FF8C00", "#00B294", "#2D2D30", "#767676" };
                foreach (var hex in presets)
                {
                    var btn = new Button
                    {
                        Width = 40,
                        Height = 40,
                        Margin = new Thickness(5),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand
                    };
                    btn.Click += (s, args) => { app.TileColor = hex; colorWin.Close(); SaveConfig(); };
                    wrap.Children.Add(btn);
                }
                stack.Children.Add(wrap);
                stack.Children.Add(new TextBlock { Text = "或输入 Hex 颜色代码 (如 #FF5722)", Foreground = Brushes.White, Margin = new Thickness(0, 15, 0, 5) });
                var tb = new TextBox
                {
                    Text = app.TileColor,
                    Height = 30,
                    Margin = new Thickness(0, 0, 0, 15),
                    Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(5, 0, 0, 0),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(tb);
                var applyBtn = new Button
                {
                    Content = "应用自定义颜色",
                    Height = 35,
                    Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                applyBtn.Click += (s, args) =>
                {
                    try { var color = (Color)ColorConverter.ConvertFromString(tb.Text); app.TileColor = tb.Text; SaveConfig(); colorWin.Close(); }
                    catch { MessageBox.Show("颜色代码无效，格式应为 #RRGGBB，例如 #FF0000。", "错误"); }
                };
                stack.Children.Add(applyBtn);
                colorWin.Content = stack;
                colorWin.ShowDialog();
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized) { WindowState = WindowState.Normal; btnMaximize.Content = "□"; }
            else { WindowState = WindowState.Maximized; btnMaximize.Content = "❐"; }
        }
    }

    // ==================== 配置数据模型 DTO ====================
    public class AppConfigDTO
    {
        public List<TileGroupDTO> Groups { get; set; }
        public string ThemeMode { get; set; }
        public string MetroBgColor { get; set; }
        public List<SkinModel> Skins { get; set; }
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