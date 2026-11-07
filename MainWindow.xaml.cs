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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
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
        private ICollectionView _viewWin10;
        private ICollectionView _viewWin7;
        private ICollectionView _viewWin11;

        private Point _listDragStartPoint, _tileDragStartPoint;
        private AppItem _draggedTile;
        private const double CellSize = 50;
        private const int Columns = 6;
        private string[] _tileColors = { "#0078D7", "#005A9E", "#10893E", "#5C2D91", "#CA5010", "#00B294", "#2D7D9A", "#7A7574" };
        private Random _rand = new Random();
        private int _tileColorIndex = 0;

        private string _currentThemeMode = "Dark";
        private string _currentLayoutMode = "Win10";
        private bool _win11ShowAll = false;
        private bool _isMaximized = false;
        private Rect _restoreBounds = Rect.Empty;

        // ===== 触屏长按 =====
        private DispatcherTimer _touchHoldTimer;
        private DispatcherTimer _tileFlipTimer;
        private bool _isTouchHoldTriggered = false;
        private AppItem _touchDragItem;
        private Point _touchStartPoint;
        private FrameworkElement _touchDragSource;
        private DateTime _suppressClickUntil = DateTime.MinValue;
        private bool _manualDrag;
        private AppItem _manualDragItem;
        private Border _manualDragBorder;
        private Border _dragGhost;
        private DispatcherTimer _tileAnimTimer;
        private AppItem _dragGhostItem;

        /// <summary>按顺序取磁贴颜色，避免一片花花绿绿；同时保证都是中深色，白字始终可读</summary>
        private string NextTileColor()
        {
            string color = _tileColors[_tileColorIndex % _tileColors.Length];
            _tileColorIndex++;
            return color;
        }

        private bool ClickSuppressed
        {
            get { return DateTime.Now < _suppressClickUntil; }
        }

        public MainWindow()
        {
            InitializeComponent();

            SystemApps = new ObservableCollection<AppItem>();
            TileGroups = new ObservableCollection<TileGroup>();

            // 三个列表各用独立的视图，互不干扰；分组只给 Win7 / Win10
            CollectionViewSource vs10 = new CollectionViewSource { Source = SystemApps };
            vs10.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppItem.FirstLetter)));
            _viewWin10 = vs10.View;
            lstApps.ItemsSource = _viewWin10;

            CollectionViewSource vs7 = new CollectionViewSource { Source = SystemApps };
            vs7.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppItem.FirstLetter)));
            _viewWin7 = vs7.View;
            lstAppsWin7.ItemsSource = _viewWin7;

            CollectionViewSource vs11 = new CollectionViewSource { Source = SystemApps };
            vs11.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppItem.FirstLetter)));
            vs11.SortDescriptions.Add(new SortDescription(nameof(AppItem.FirstLetter), ListSortDirection.Ascending));
            vs11.SortDescriptions.Add(new SortDescription(nameof(AppItem.Name), ListSortDirection.Ascending));
            _viewWin11 = vs11.View;
            lstAppsWin11.ItemsSource = _viewWin11;

            // Win7 / Win10 也按首字母 + 名称排序
            vs10.SortDescriptions.Add(new SortDescription(nameof(AppItem.FirstLetter), ListSortDirection.Ascending));
            vs10.SortDescriptions.Add(new SortDescription(nameof(AppItem.Name), ListSortDirection.Ascending));
            vs7.SortDescriptions.Add(new SortDescription(nameof(AppItem.FirstLetter), ListSortDirection.Ascending));
            vs7.SortDescriptions.Add(new SortDescription(nameof(AppItem.Name), ListSortDirection.Ascending));

            lstAppsXP.ItemsSource = SystemApps;

            tileGroupsControl.ItemsSource = TileGroups;
            allAppsMetroControl.ItemsSource = _metroAllAppsSource;
            skinListControl.ItemsSource = ImportedSkins;
            if (languageListControl != null) languageListControl.ItemsSource = LanguageService.All;

            _touchHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _touchHoldTimer.Tick += TouchHoldTimer_Tick;

            // 动态磁贴：Win8 / Win10 面板里每隔几秒翻两下
            _tileFlipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _tileFlipTimer.Tick += TileFlip_Tick;
            _tileFlipTimer.Start();

            LanguageService.Apply(this, "zh-CN");

            LoadApps();
            LoadUserAvatar();
            GenerateLetters();
            UpdateSearchPlaceholders();

            LoadConfig();
            PruneMissingTiles();
            StartTileAnimator();
            SyncWin11Grid();
            ApplyLayoutMode("Win10");

            // 调试用：WinLauncher.exe --mode=Win8 直接进某个模式
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                    ApplyLayoutMode(arg.Substring(7));
            }

            // 全方位监听窗口状态，防止毛玻璃被系统"吃掉"
            this.SourceInitialized += (s, e) => CheckAndApplyCurrentStyle();
            this.Loaded += (s, e) =>
            {
                CheckAndApplyCurrentStyle();
                ApplyWin8Backdrop();
                Dispatcher.BeginInvoke(new Action(PlayEntranceAnimation), DispatcherPriority.Background);
            };
            this.Activated += (s, e) => CheckAndApplyCurrentStyle();
            this.StateChanged += (s, e) => CheckAndApplyCurrentStyle();

            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) => txtTime.Text = DateTime.Now.ToString("HH:mm");
            timer.Start();
            txtTime.Text = DateTime.Now.ToString("HH:mm");
        }

        // ==================== 核心检测与特效应用 ====================
        private void CheckAndApplyCurrentStyle()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 全部用不透明纯色底，内容直接浮在上面，不再依赖 DWM 亚克力，
                // 避免 tint 把文字和图标冲白
                AcrylicHelper.DisableAcrylic(this);
            }), DispatcherPriority.Render);
        }

        // ==================== 全局色彩 ====================
        private void SetThemeColors(string mode, bool save = true)
        {
            _currentThemeMode = mode;

            if (mode == "Dark")
            {
                this.Resources["WindowBgBrush"] = new SolidColorBrush(Color.FromArgb(255, 32, 32, 32));
                this.Resources["PanelBgBrush"] = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
                this.Resources["TextMainBrush"] = new SolidColorBrush(Colors.White);
                this.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
                this.Resources["SearchBoxBgBrush"] = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                this.Resources["SearchBoxFgBrush"] = new SolidColorBrush(Colors.White);
                this.Resources["ButtonBgBrush"] = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
                this.Resources["ButtonFgBrush"] = new SolidColorBrush(Colors.Black);
            }
            else
            {
                this.Resources["WindowBgBrush"] = new SolidColorBrush(Color.FromArgb(255, 243, 243, 243));
                this.Resources["PanelBgBrush"] = new SolidColorBrush(Color.FromArgb(18, 0, 0, 0));
                this.Resources["TextMainBrush"] = new SolidColorBrush(Colors.Black);
                this.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
                this.Resources["SearchBoxBgBrush"] = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
                this.Resources["SearchBoxFgBrush"] = new SolidColorBrush(Colors.Black);
                this.Resources["ButtonBgBrush"] = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0));
                this.Resources["ButtonFgBrush"] = new SolidColorBrush(Colors.White);
            }

            UpdateSearchPlaceholders();
            SyncWin11Grid();
            CheckAndApplyCurrentStyle();
            if (save) SaveConfig();
        }

        private void SetDarkTheme_Click(object sender, RoutedEventArgs e) => SetThemeColors("Dark");
        private void SetLightTheme_Click(object sender, RoutedEventArgs e) => SetThemeColors("Light");

        // ==================== 五模式切换 ====================
        private static string GetLayoutDisplayName(string mode)
        {
            switch (mode)
            {
                case "XP": return "Windows XP";
                case "Win7": return "Windows 7";
                case "Win8": return "Windows 8";
                case "Win11": return "Windows 11";
                default: return "Windows 10";
            }
        }

        private void ApplyLayoutMode(string mode)
        {
            _currentLayoutMode = mode;

            pnlXP.Visibility = mode == "XP" ? Visibility.Visible : Visibility.Collapsed;
            pnlWin7.Visibility = mode == "Win7" ? Visibility.Visible : Visibility.Collapsed;
            pnlRight8.Visibility = mode == "Win8" ? Visibility.Visible : Visibility.Collapsed;
            pnlWin10Root.Visibility = mode == "Win10" ? Visibility.Visible : Visibility.Collapsed;
            pnlWin11.Visibility = mode == "Win11" ? Visibility.Visible : Visibility.Collapsed;

            if (txtShellTitle != null) txtShellTitle.Text = GetLayoutDisplayName(mode);

            // 每种开始菜单用各自的经典尺寸
            if (mode == "Win8")
            {
                EnterPseudoMaximize();
            }
            else
            {
                ExitPseudoMaximize();

                double w, h;
                switch (mode)
                {
                    case "XP": w = 416; h = 556; break;
                    case "Win7": w = 580; h = 676; break;
                    case "Win11": w = 640; h = 760; break;
                    default: w = 660; h = 676; break;
                }
                if (Math.Abs(Width - w) > 0.5) Width = w;
                if (Math.Abs(Height - h) > 0.5) Height = h;

                double left = (SystemParameters.PrimaryScreenWidth - Width) / 2.0;
                double top = (SystemParameters.PrimaryScreenHeight - Height) / 2.0;
                if (!double.IsNaN(left) && !double.IsInfinity(left)) Left = left < 0 ? 0 : left;
                if (!double.IsNaN(top) && !double.IsInfinity(top)) Top = top < 0 ? 0 : top;
            }

            if (MainBorder != null)
                MainBorder.CornerRadius = mode == "Win8" ? new CornerRadius(0) : new CornerRadius(8);

            // Win8 用平铺磁贴：等布局完成后再同步一次，避免切换瞬间容器还没生成
            if (mode == "Win8")
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SyncAllTiles();
                    RecalculateLayout(false, 62);
                }), DispatcherPriority.Background);
            }
            else if (mode == "Win10")
            {
                Dispatcher.BeginInvoke(new Action(() => RecalculateLayout(false, 50)), DispatcherPriority.Background);
            }

            UpdateChromeForState();

            // XP / Win7 时代没有深色主题：临时换成浅色配色，但不能吃掉用户选的深浅
            if (mode == "XP" || mode == "Win7")
            {
                string keepTheme = _currentThemeMode;
                SetThemeColors("Light", false);
                _currentThemeMode = keepTheme;
            }
            else
            {
                SetThemeColors(_currentThemeMode, false);
            }

            CloseSettings();
            CheckAndApplyCurrentStyle();
            AnimateCurrentPanel();
        }

        private void SwitchToXP(object sender, RoutedEventArgs e) { ApplyLayoutMode("XP"); }
        private void SwitchToWin7(object sender, RoutedEventArgs e) { ApplyLayoutMode("Win7"); }
        private void SwitchToWin8(object sender, RoutedEventArgs e) { SyncAllTiles(); ApplyLayoutMode("Win8"); }
        private void SwitchToWin10(object sender, RoutedEventArgs e) { ApplyLayoutMode("Win10"); RecalculateLayout(false); }
        private void SwitchToWin11(object sender, RoutedEventArgs e) { SyncWin11Grid(); ApplyLayoutMode("Win11"); }

        /// <summary>皮肤系统会按 LayoutMode 直接切换，兼容旧的 "Win7"/"Win11"/"XP" 三种无磁贴布局</summary>
        private void SwitchToWin7Or11(string mode)
        {
            if (string.IsNullOrEmpty(mode)) mode = "Win10";
            if (mode != "XP" && mode != "Win7" && mode != "Win8" && mode != "Win10" && mode != "Win11") mode = "Win10";
            ApplyLayoutMode(mode);
        }

        /// <summary>磁贴位置缓动：X/Y 变了不瞬移，逐帧逼近，换位和落位都是滑过去的</summary>
        private void StartTileAnimator()
        {
            _tileAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _tileAnimTimer.Tick += TileAnimator_Tick;
            _tileAnimTimer.Start();
        }

        private void TileAnimator_Tick(object sender, EventArgs e)
        {
            foreach (AppItem tile in AllTiles)
            {
                if (tile == null) continue;

                double dx = tile.X - tile.DisplayX;
                double dy = tile.Y - tile.DisplayY;

                if (Math.Abs(dx) < 0.4 && Math.Abs(dy) < 0.4)
                {
                    if (tile.DisplayX != tile.X) tile.DisplayX = tile.X;
                    if (tile.DisplayY != tile.Y) tile.DisplayY = tile.Y;
                    continue;
                }
                tile.DisplayX += dx * 0.3;
                tile.DisplayY += dy * 0.3;
            }
        }

        /// <summary>启动时清掉指向已卸载程序的磁贴；SystemApps 每次启动重扫，新装的会自动出现在列表里</summary>
        private void PruneMissingTiles()
        {
            bool changed = false;

            for (int g = TileGroups.Count - 1; g >= 0; g--)
            {
                TileGroup group = TileGroups[g];
                if (group == null) { TileGroups.RemoveAt(g); changed = true; continue; }

                for (int i = group.Tiles.Count - 1; i >= 0; i--)
                {
                    AppItem tile = group.Tiles[i];
                    if (tile == null) { group.Tiles.RemoveAt(i); changed = true; continue; }
                    if (string.IsNullOrEmpty(tile.Path)) continue;
                    if (!File.Exists(tile.Path)) { group.Tiles.RemoveAt(i); changed = true; }
                }

                if (group.Tiles.Count == 0 && TileGroups.Count > 1) { TileGroups.RemoveAt(g); changed = true; }
            }

            if (changed)
            {
                SyncAllTiles();
                RecalculateLayout(false);
                SaveConfig();
            }
        }

        private FrameworkElement GetPanel(string mode)
        {
            switch (mode)
            {
                case "XP": return pnlXP;
                case "Win7": return pnlWin7;
                case "Win8": return pnlRight8;
                case "Win11": return pnlWin11;
                default: return pnlWin10Root;
            }
        }

        /// <summary>切换开始菜单风格时：淡入 + 轻微放大</summary>
        private void AnimateCurrentPanel()
        {
            FrameworkElement panel = GetPanel(_currentLayoutMode);
            if (panel == null) return;

            var scale = new ScaleTransform(0.94, 0.94);
            panel.RenderTransformOrigin = new Point(0.5, 0.5);
            panel.RenderTransform = scale;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170));
            fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 回弹：从 0.94 弹到 1.0，比线性放大更有"弹出来"的感觉
            var grow = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(260));
            grow.EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

            panel.BeginAnimation(UIElement.OpacityProperty, fade);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }

        private void CloseSettings()
        {
            if (SettingsPopup != null) SettingsPopup.IsOpen = false;
        }

        /// <summary>最大化时去掉四周的缩放热区，否则右上角三个按钮会被鼠标拖拽判定吃掉</summary>
        private void UpdateChromeForState()
        {
            try
            {
                WindowChrome chrome = WindowChrome.GetWindowChrome(this);
                if (chrome != null)
                {
                    chrome.ResizeBorderThickness = _isMaximized ? new Thickness(0) : new Thickness(6);
                }
            }
            catch { }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

            if (_currentLayoutMode == "Win8")
            {
                ApplyLayoutMode("Win10");

            // 调试用：WinLauncher.exe --mode=Win8 直接进某个模式
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                    ApplyLayoutMode(arg.Substring(7));
            }
                e.Handled = true;
            }
            else if (_isMaximized)
            {
                ExitPseudoMaximize();
                e.Handled = true;
            }
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
                    CloseSettings();
                    MessageBox.Show("配置导出成功！", "成功");
                }
                catch (Exception ex) { MessageBox.Show("导出失败：" + ex.Message, "错误"); }
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
                    ApplyLayoutMode("Win10");

            // 调试用：WinLauncher.exe --mode=Win8 直接进某个模式
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                    ApplyLayoutMode(arg.Substring(7));
            }

                    CloseSettings();
                    MessageBox.Show("已成功恢复至初始状态！", "还原完成");
                }
                catch (Exception ex) { MessageBox.Show("还原失败：" + ex.Message, "错误"); }
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
                    Skins = ImportedSkins.ToList(),
                    Language = LanguageService.CurrentCode
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
            if (config == null) return;

            if (!string.IsNullOrEmpty(config.Language)) LanguageService.Apply(this, config.Language);
            if (!string.IsNullOrEmpty(config.ThemeMode)) SetThemeColors(config.ThemeMode, false);
            if (!string.IsNullOrEmpty(config.MetroBgColor))
            {
                try { this.Resources["MetroBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(config.MetroBgColor)); }
                catch { }
            }

            if (config.Skins != null)
            {
                ImportedSkins.Clear();
                foreach (var skin in config.Skins) ImportedSkins.Add(skin);
            }

            TileGroups.Clear();
            if (config.Groups != null)
            {
                foreach (var groupDto in config.Groups)
                {
                    if (groupDto == null) continue;
                    var group = new TileGroup { GroupTitle = groupDto.GroupTitle, Tiles = new ObservableCollection<AppItem>() };
                    if (groupDto.Tiles != null)
                    {
                        foreach (var tileDto in groupDto.Tiles)
                        {
                            if (tileDto == null) continue;
                            group.Tiles.Add(new AppItem
                            {
                                Name = tileDto.Name,
                                Path = tileDto.Path,
                                TileSize = string.IsNullOrEmpty(tileDto.TileSize) ? "Medium" : tileDto.TileSize,
                                TileColor = string.IsNullOrEmpty(tileDto.TileColor) ? "#0078D7" : tileDto.TileColor,
                                Icon = string.IsNullOrEmpty(tileDto.Path) ? null : IconHelper.GetIconWithoutArrow(tileDto.Path)
                            });
                        }
                    }
                    TileGroups.Add(group);
                }
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
                    if (config == null) { MessageBox.Show("导入失败，文件内容为空！", "错误"); return; }

                    ApplyConfig(config);
                    SyncAllTiles();
                    RecalculateLayout(true);
                    CloseSettings();
                    MessageBox.Show("配置导入成功！已自动保存到当前目录。", "成功");
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

            string keyword = txtSearchWin11 != null ? (txtSearchWin11.Text ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
            IEnumerable<AppItem> source = AllTiles.Count > 0 ? (IEnumerable<AppItem>)AllTiles : SystemApps;
            if (!string.IsNullOrEmpty(keyword))
                source = source.Where(a => a.Name != null && a.Name.ToLowerInvariant().Contains(keyword));
            if (!_win11ShowAll) source = source.Take(18);

            foreach (AppItem app in source)
            {
                Button btn = new Button
                {
                    Style = (Style)this.Resources["Win11ItemStyle"],
                    Tag = app,
                    ToolTip = app.Name
                };
                btn.Click += Win11App_Click;

                var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                panel.Children.Add(new Image { Source = app.Icon, Width = 32, Height = 32, Margin = new Thickness(0, 0, 0, 6) });
                panel.Children.Add(new TextBlock
                {
                    Text = app.Name,
                    Foreground = txtBrush,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 84
                });
                btn.Content = panel;
                win11AppsGrid.Children.Add(btn);
            }
        }

        private void Win11App_Click(object sender, RoutedEventArgs e)
        {
            if (ClickSuppressed) return;
            AppItem app = (sender as Button)?.Tag as AppItem;
            Launch(app);
        }

        /// <summary>「已固定」默认两行，点「全部显示」展开成多行，下面的列表被推下去</summary>
        private void ToggleWin11AllApps(object sender, RoutedEventArgs e)
        {
            _win11ShowAll = !_win11ShowAll;

            if (scrollWin11Pinned != null)
            {
                double from = scrollWin11Pinned.MaxHeight;
                double to = _win11ShowAll ? 420 : 184;

                var grow = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(230));
                grow.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                scrollWin11Pinned.BeginAnimation(FrameworkElement.MaxHeightProperty, grow);
            }

            if (btnWin11Expand != null)
                btnWin11Expand.Content = LanguageService.Get(_win11ShowAll ? "Collapse" : "ShowAll");

            SyncWin11Grid();
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
                        MessageBox.Show("皮肤 '" + skin.SkinName + "' 导入成功！", "提示");
                    }
                }
                catch { MessageBox.Show("皮肤文件格式错误！", "错误"); }
            }
        }

        private void DeleteSkin_Click(object sender, RoutedEventArgs e)
        {
            SkinModel skin = (sender as MenuItem)?.Tag as SkinModel;
            if (skin != null)
            {
                ImportedSkins.Remove(skin);
                SaveConfig();
            }
        }

        private void ApplySkin_Click(object sender, RoutedEventArgs e)
        {
            SkinModel skin = (sender as Button)?.Tag as SkinModel;
            if (skin == null) return;

            try
            {
                this.Resources["WindowBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.WindowBgColor));
                this.Resources["PanelBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.PanelBgColor));
                this.Resources["TextMainBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.TextMainColor));
                this.Resources["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.TextMutedColor));
                this.Resources["ButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.ButtonBgColor));
                this.Resources["ButtonFgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skin.ButtonFgColor));
                this.Resources["TileCornerRadius"] = new CornerRadius(skin.TileCornerRadius);
                if (!string.IsNullOrEmpty(skin.TileDefaultColor)) _tileColors[0] = skin.TileDefaultColor;

                if (!skin.HasTiles)
                {
                    SwitchToWin7Or11(skin.LayoutMode);
                }
                else
                {
                    if (skin.LayoutMode == "Win8") SwitchToWin8(null, null);
                    else if (skin.LayoutMode == "XP") SwitchToXP(null, null);
                    else if (skin.LayoutMode == "Win11") SwitchToWin11(null, null);
                    else SwitchToWin10(null, null);
                }

                SyncWin11Grid();
                CloseSettings();
                MessageBox.Show("已应用皮肤：" + skin.SkinName, "成功");
            }
            catch (Exception ex) { MessageBox.Show("应用皮肤失败：" + ex.Message, "错误"); }
        }

        private void ToggleSettingsMenu(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = true;

        private void SwitchLanguage_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string code = btn != null ? btn.Tag as string : null;
            if (string.IsNullOrEmpty(code)) return;

            LanguageService.Apply(this, code);
            UpdateSearchPlaceholders();
            if (btnWin11Expand != null)
                btnWin11Expand.Content = LanguageService.Get(_win11ShowAll ? "Collapse" : "ShowAll");
            SyncWin11Grid();
            SaveConfig();
        }

        private void MetroBg_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string hex = btn?.Tag as string;
            if (!string.IsNullOrEmpty(hex))
            {
                this.Resources["MetroBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                SaveConfig();
            }
        }

        // ==================== 动态磁贴 ====================
        private void TileFlip_Tick(object sender, EventArgs e)
        {
            if (_currentLayoutMode != "Win8" && _currentLayoutMode != "Win10") return;
            if (_isTouchHoldTriggered) return;

            var tiles = new List<Border>();
            if (_currentLayoutMode == "Win8") CollectTileBorders(metroTileControl, tiles);
            else CollectTileBorders(tileGroupsControl, tiles);

            if (tiles.Count == 0) return;

            int flips = Math.Min(2, tiles.Count);
            for (int i = 0; i < flips; i++)
                AnimateTileFlip(tiles[_rand.Next(tiles.Count)]);
        }

        private void CollectTileBorders(DependencyObject root, List<Border> output)
        {
            if (root == null) return;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                Border border = child as Border;
                if (border != null && (border.Tag as string) == "tile" && border.RenderSize.Width > 20)
                {
                    output.Add(border);
                    continue;
                }
                CollectTileBorders(child, output);
            }
        }

        private void AnimateTileFlip(Border tile)
        {
            if (tile == null) return;
            try
            {

            ScaleTransform scale = GetFlipTransform(tile);
            if (scale == null) return;

            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.04, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
            catch { }
        }

        // ==================== 触屏长按拖拽（重写） ====================
        private void TouchHoldTimer_Tick(object sender, EventArgs e)
        {
            _touchHoldTimer.Stop();
            _isTouchHoldTriggered = true;
        }

        /// <summary>从手指按下的位置向上找到真正的 AppItem（原实现取 SelectedItem，会拖到上一次选中的项）</summary>
        private AppItem FindAppItemFromSource(DependencyObject source)
        {
            DependencyObject node = source;
            int guard = 0;
            while (node != null && guard++ < 64)
            {
                FrameworkElement fe = node as FrameworkElement;
                if (fe != null && fe.DataContext is AppItem) return (AppItem)fe.DataContext;
                node = GetParentSafe(node);
            }
            return null;
        }

        private static DependencyObject GetParentSafe(DependencyObject node)
        {
            try
            {
                FrameworkContentElement fce = node as FrameworkContentElement;
                if (fce != null) return fce.Parent;
                return VisualTreeHelper.GetParent(node);
            }
            catch { return null; }
        }

        private void LstApps_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            _touchHoldTimer.Stop();
            _isTouchHoldTriggered = false;
            _touchDragSource = sender as FrameworkElement;
            _touchDragItem = FindAppItemFromSource(e.OriginalSource as DependencyObject);
            if (_touchDragItem == null) return;
            _touchStartPoint = e.GetTouchPoint(null).Position;
            _touchHoldTimer.Start();
        }

        private void LstApps_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            _touchHoldTimer.Stop();
            if (_isTouchHoldTriggered) _suppressClickUntil = DateTime.Now.AddMilliseconds(350);
            _isTouchHoldTriggered = false;
            _touchDragItem = null;
            _touchDragSource = null;
        }

        private void LstApps_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            Point now = e.GetTouchPoint(null).Position;

            if (!_isTouchHoldTriggered)
            {
                Vector diff = now - _touchStartPoint;
                if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10)
                {
                    // 手指在滑动列表 → 放行滚动，取消长按
                    _touchHoldTimer.Stop();
                    _touchDragItem = null;
                    _touchDragSource = null;
                }
                return;
            }

            if (_touchDragItem == null || _touchDragSource == null) return;

            _touchHoldTimer.Stop();
            _isTouchHoldTriggered = false;
            e.Handled = true;

            DragDrop.DoDragDrop(_touchDragSource, new DataObject("AppFormat", _touchDragItem), DragDropEffects.Copy);
            _suppressClickUntil = DateTime.Now.AddMilliseconds(350);
            _touchDragItem = null;
            _touchDragSource = null;
        }

        // ==================== 触控手动拖拽（不走 DoDragDrop） ====================
        private void BeginManualDrag(AppItem app, Point ghostPos)
        {
            _manualDrag = true;
            _manualDragItem = app;
            _manualDragBorder = FindTileBorder(_touchDragSource);
            if (_manualDragBorder != null) { _manualDragBorder.Opacity = 0.3; _manualDragBorder.Effect = LiftShadow(); }

            BeginDragGhost(app, ghostPos);
            _isTouchHoldTriggered = false;
            _touchHoldTimer.Stop();
            _suppressClickUntil = DateTime.Now.AddMilliseconds(400);
        }

        private void EndManualDrag()
        {
            _manualDrag = false;
            HideDragGhost();

            if (_manualDragBorder != null) { _manualDragBorder.Opacity = 1.0; _manualDragBorder.Effect = null; _manualDragBorder = null; }
            _manualDragItem = null;
            _touchDragItem = null;
            _touchDragSource = null;

            _suppressClickUntil = DateTime.Now.AddMilliseconds(400);
            RecalculateLayout(true);
            SyncAllTiles();
            PlaySettleAnimation();
        }

        /// <summary>手指移到哪，就让磁贴在哪个分组的哪个位置让位</summary>
        private void UpdateManualDragTarget(Point posInMain)
        {
            AppItem dragged = _manualDragItem;
            if (dragged == null || dragged.ParentGroup == null) return;

            HitTestResult hit = VisualTreeHelper.HitTest(gridMainContent, posInMain);
            DependencyObject node = hit?.VisualHit;

            ItemsControl targetControl = null;
            TileGroup targetGroup = null;
            int guard = 0;
            while (node != null && guard++ < 32)
            {
                ItemsControl ic = node as ItemsControl;
                if (ic != null && ic.DataContext is TileGroup)
                {
                    targetControl = ic;
                    targetGroup = (TileGroup)ic.DataContext;
                    break;
                }
                node = GetParentSafe(node);
            }

            if (targetControl == null || targetGroup == null) return;

            Point local;
            try { local = gridMainContent.TranslatePoint(posInMain, targetControl); }
            catch { return; }

            int index = GetInsertIndex(targetGroup, local);

            try
            {
                if (dragged.ParentGroup == targetGroup)
                {
                    int old = targetGroup.Tiles.IndexOf(dragged);
                    if (old < 0) return;

                    int want = index > old ? index - 1 : index;
                    if (want < 0) want = 0;
                    if (want > targetGroup.Tiles.Count - 1) want = targetGroup.Tiles.Count - 1;

                    if (want != old)
                    {
                        targetGroup.Tiles.Move(old, want);
                        RecalculateLayout(false);
                    }
                }
                else
                {
                    dragged.ParentGroup.Tiles.Remove(dragged);
                    if (index < 0) index = 0;
                    if (index > targetGroup.Tiles.Count) index = targetGroup.Tiles.Count;
                    targetGroup.Tiles.Insert(index, dragged);
                    RecalculateLayout(false);
                }
            }
            catch { }
        }

        private void Tile_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            _touchHoldTimer.Stop();
            _isTouchHoldTriggered = false;
            _touchDragSource = sender as FrameworkElement;
            _touchDragItem = FindAppItemFromSource(e.OriginalSource as DependencyObject);
            if (_touchDragItem == null) return;
            _touchStartPoint = e.GetTouchPoint(null).Position;
            _touchHoldTimer.Start();
        }

        private void Tile_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            _touchHoldTimer.Stop();

            if (_manualDrag)
            {
                EndManualDrag();
                e.Handled = true;
                return;
            }

            if (_isTouchHoldTriggered) _suppressClickUntil = DateTime.Now.AddMilliseconds(350);
            _isTouchHoldTriggered = false;
            _touchDragItem = null;
            _touchDragSource = null;
        }

        private void Tile_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            // 已经在手动拖拽中：只更新幽灵和让位，不再走长按判定
            if (_manualDrag)
            {
                MoveDragGhost(e.GetTouchPoint(dragLayer).Position);
                UpdateManualDragTarget(e.GetTouchPoint(gridMainContent).Position);
                e.Handled = true;
                return;
            }

            Point now = e.GetTouchPoint(null).Position;

            if (!_isTouchHoldTriggered)
            {
                Vector diff = now - _touchStartPoint;
                if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10)
                {
                    _touchHoldTimer.Stop();
                    _touchDragItem = null;
                    _touchDragSource = null;
                }
                return;
            }

            if (_touchDragItem == null || _touchDragSource == null) return;

            Point ghostPos = e.GetTouchPoint(dragLayer).Position;

            if (!_manualDrag)
            {
                BeginManualDrag(_touchDragItem, ghostPos);
                e.Handled = true;
                return;
            }

            MoveDragGhost(ghostPos);
            UpdateManualDragTarget(e.GetTouchPoint(gridMainContent).Position);
            e.Handled = true;
        }

        // ==================== 鼠标拖拽 ====================
        private void LstApps_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _listDragStartPoint = e.GetPosition(null);
        }

        private void LstApps_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            Vector diff = _listDragStartPoint - e.GetPosition(null);
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            FrameworkElement fe = sender as FrameworkElement;
            AppItem app = FindAppItemFromSource(e.OriginalSource as DependencyObject);
            if (app == null) return;

            BeginDragGhost(app, e.GetPosition(dragLayer));
            DragDrop.DoDragDrop(fe, new DataObject("AppFormat", app), DragDropEffects.Copy);
            HideDragGhost();
        }

        private void Tile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _tileDragStartPoint = e.GetPosition(null);
            _draggedTile = FindAppItemFromSource(e.OriginalSource as DependencyObject);
            if (_draggedTile == null) _draggedTile = (sender as FrameworkElement)?.DataContext as AppItem;
        }

        private void Tile_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedTile == null) return;

            Vector diff = _tileDragStartPoint - e.GetPosition(null);
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            Border tileBorder = FindTileBorder((DependencyObject)sender);
            if (tileBorder != null) { tileBorder.Opacity = 0.35; tileBorder.Effect = LiftShadow(); }
            BeginDragGhost(_draggedTile, e.GetPosition(dragLayer));

            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("TileFormat", _draggedTile), DragDropEffects.Move);

            HideDragGhost();
            if (tileBorder != null) { tileBorder.Opacity = 1.0; tileBorder.Effect = null; }
            _draggedTile = null;
            Dispatcher.BeginInvoke(new Action(PlaySettleAnimation), DispatcherPriority.Loaded);
        }

        // ==================== 拖放与网格算法 ====================
        /// <summary>关掉 OLE 默认的拖动图像（那个虚线缩略框），只显示我们自己的幽灵磁贴</summary>
        private void Window_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = false;
            Mouse.SetCursor(Cursors.SizeAll);
            e.Handled = true;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            // 必须和拖动源声明的 effects 一致，否则 Windows 会显示"禁止"光标（红圈红杠）
            if (e.Data.GetDataPresent("TileFormat")) e.Effects = DragDropEffects.Move;
            else if (e.Data.GetDataPresent("AppFormat")) e.Effects = DragDropEffects.Copy;
            else if (e.Data.GetDataPresent("GroupFormat")) e.Effects = DragDropEffects.Move;
            else e.Effects = DragDropEffects.None;

            if (_dragGhostItem != null) MoveDragGhost(e.GetPosition(dragLayer));
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            HideDragGhost();
        }

        private void BeginDragGhost(AppItem app, Point startPos)
        {
            HideDragGhost();
            if (app == null || dragLayer == null) return;

            _dragGhostItem = app;
            Color color;
            try { color = (Color)ColorConverter.ConvertFromString(string.IsNullOrEmpty(app.TileColor) ? "#0078D7" : app.TileColor); }
            catch { color = Color.FromRgb(0, 120, 215); }

            _dragGhost = new Border
            {
                Width = 92,
                Height = 92,
                Background = new SolidColorBrush(color),
                Opacity = 0.72,
                CornerRadius = new CornerRadius(0),
                IsHitTestVisible = false
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new Image { Source = app.Icon, Width = 30, Height = 30, Margin = new Thickness(0, 0, 0, 5) });
            stack.Children.Add(new TextBlock
            {
                Text = app.Name,
                Foreground = Brushes.White,
                FontSize = 11.5,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 86
            });
            _dragGhost.Child = stack;
            dragLayer.Children.Add(_dragGhost);
            MoveDragGhost(startPos);
        }

        private void MoveDragGhost(Point pos)
        {
            if (_dragGhost == null) return;
            Canvas.SetLeft(_dragGhost, pos.X - 46);
            Canvas.SetTop(_dragGhost, pos.Y - 46);
        }

        private void HideDragGhost()
        {
            if (_dragGhost != null && dragLayer != null) dragLayer.Children.Remove(_dragGhost);
            _dragGhost = null;
            _dragGhostItem = null;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            HideDragGhost();
            if (!e.Data.GetDataPresent("AppFormat")) return;

            AppItem app = e.Data.GetData("AppFormat") as AppItem;
            if (app == null) return;

            if (TileGroups.Count == 0)
                TileGroups.Add(new TileGroup { GroupTitle = "实用工具", Tiles = new ObservableCollection<AppItem>() });

            InsertAppIntoGroup(TileGroups[0], app, new Point(0, 0));
            e.Handled = true;
        }

        /// <summary>拖动经过分组时实时让位：集合顺序当场调整，磁贴靠 DisplayX/Y 缓动滑到新位置</summary>
        private void Group_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TileFormat")) return;

            AppItem dragged = e.Data.GetData("TileFormat") as AppItem;
            ItemsControl ic = sender as ItemsControl;
            TileGroup target = ic?.DataContext as TileGroup;
            if (dragged == null || target == null || dragged.ParentGroup == null) return;

            Point pos = e.GetPosition(ic);
            int index = GetInsertIndex(target, pos);

            try
            {
                if (dragged.ParentGroup == target)
                {
                    int old = target.Tiles.IndexOf(dragged);
                    if (old < 0) return;

                    int want = index > old ? index - 1 : index;
                    if (want < 0) want = 0;
                    if (want > target.Tiles.Count - 1) want = target.Tiles.Count - 1;

                    if (want != old) { target.Tiles.Move(old, want); RecalculateLayout(false); }
                }
                else
                {
                    dragged.ParentGroup.Tiles.Remove(dragged);
                    if (index < 0) index = 0;
                    if (index > target.Tiles.Count) index = target.Tiles.Count;
                    target.Tiles.Insert(index, dragged);
                    RecalculateLayout(false);
                }
            }
            catch { }
        }

        private void Group_Drop(object sender, DragEventArgs e)
        {
            ItemsControl targetItemsControl = sender as ItemsControl;
            TileGroup targetGroup = targetItemsControl?.DataContext as TileGroup;
            if (targetGroup == null) return;

            Point mousePos = e.GetPosition(targetItemsControl);

            if (e.Data.GetDataPresent("AppFormat"))
            {
                AppItem app = e.Data.GetData("AppFormat") as AppItem;
                InsertAppIntoGroup(targetGroup, app, mousePos);
            }
            else if (e.Data.GetDataPresent("TileFormat"))
            {
                // 位置在 DragOver 里已经实时调整过，这里只负责落地保存
                RecalculateLayout();
                SyncAllTiles();
                PlaySettleAnimation();
            }
            e.Handled = true;
        }

        private void InsertAppIntoGroup(TileGroup group, AppItem app, Point pos)
        {
            if (group == null || app == null) return;
            if (TileGroups.Any(g => g.Tiles.Any(x => x != null && x.Name == app.Name))) return;

            var newTile = new AppItem
            {
                Name = app.Name,
                Path = app.Path,
                Icon = app.Icon,
                TileSize = "Medium",
                TileColor = NextTileColor()
            };
            int index = GetInsertIndex(group, pos);
            if (index < 0) index = 0;
            if (index > group.Tiles.Count) index = group.Tiles.Count;
            group.Tiles.Insert(index, newTile);
            RecalculateLayout();
            SyncAllTiles();
        }

        private void NewGroup_Drop(object sender, DragEventArgs e)
        {
            var newGroup = new TileGroup { GroupTitle = "新建分组", Tiles = new ObservableCollection<AppItem>() };

            if (e.Data.GetDataPresent("TileFormat"))
            {
                AppItem sourceTile = e.Data.GetData("TileFormat") as AppItem;
                if (sourceTile != null)
                {
                    if (sourceTile.ParentGroup != null) sourceTile.ParentGroup.Tiles.Remove(sourceTile);
                    newGroup.Tiles.Add(sourceTile);
                }
            }
            else if (e.Data.GetDataPresent("AppFormat"))
            {
                AppItem app = e.Data.GetData("AppFormat") as AppItem;
                if (app != null)
                {
                    newGroup.Tiles.Add(new AppItem
                    {
                        Name = app.Name,
                        Path = app.Path,
                        Icon = app.Icon,
                        TileSize = "Medium",
                        TileColor = NextTileColor()
                    });
                }
            }

            if (newGroup.Tiles.Count > 0)
            {
                TileGroups.Add(newGroup);
                RecalculateLayout();
                SyncAllTiles();
            }
            e.Handled = true;
        }

        private int GetInsertIndex(TileGroup group, Point mousePos)
        {
            if (group == null || group.Tiles.Count == 0) return 0;

            int index = group.Tiles.Count;
            double minDist = double.MaxValue;
            for (int i = 0; i < group.Tiles.Count; i++)
            {
                var t = group.Tiles[i];
                if (t == null) continue;
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

        private void RecalculateLayout(bool save = true, double cellSize = CellSize)
        {
            foreach (var group in TileGroups)
            {
                if (group == null) continue;
                var grid = new bool[200, Columns];
                double maxY = 0;

                foreach (var item in group.Tiles)
                {
                    if (item == null) continue;
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
                    if (r >= 200) { r = 0; c = 0; }

                    for (int i = 0; i < rowSpan; i++)
                        for (int j = 0; j < colSpan; j++)
                            grid[r + i, c + j] = true;

                    item.X = c * cellSize + 4; item.Y = r * cellSize + 4;
                    if (!item.HasPosition) { item.DisplayX = item.X; item.DisplayY = item.Y; item.HasPosition = true; }
                    item.Width = colSpan * cellSize - 8; item.Height = rowSpan * cellSize - 8;
                    if (item.Y + item.Height > maxY) maxY = item.Y + item.Height;
                }
                group.GroupHeight = maxY + 10 + cellSize;   // 多留一行给拖拽接触区
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

            // 排序：先按首字母（英文 A-Z、中文拼音、其他按字符），再按名称
            var ordered = SystemApps
                .OrderBy(a => a.FirstLetter, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SystemApps.Clear();
            _metroAllAppsSource.Clear();
            foreach (var app in ordered)
            {
                SystemApps.Add(app);
                _metroAllAppsSource.Add(app);
            }
        }

        private void LoadUserAvatar()
        {
            try
            {
                string avatarPath = SystemHelper.GetCurrentUserPicturePath();
                ImageSource source = null;

                if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    source = bitmap;
                }

                if (source != null)
                {
                    if (imgUserAvatar != null) imgUserAvatar.Source = source;
                    if (imgMetroAvatar != null) imgMetroAvatar.Source = source;
                    if (imgXpAvatar != null) imgXpAvatar.Source = source;
                    if (imgWin7Avatar != null) imgWin7Avatar.Source = source;
                    if (imgWin11Avatar != null) imgWin11Avatar.Source = source;

                    if (ellipseAvatarBg != null) ellipseAvatarBg.Visibility = Visibility.Collapsed;
                    if (ellipseMetroAvatarBg != null) ellipseMetroAvatarBg.Visibility = Visibility.Collapsed;
                    if (ellipseXpAvatarBg != null) ellipseXpAvatarBg.Visibility = Visibility.Collapsed;
                    if (ellipseWin7AvatarBg != null) ellipseWin7AvatarBg.Visibility = Visibility.Collapsed;
                    if (ellipseWin11AvatarBg != null) ellipseWin11AvatarBg.Visibility = Visibility.Collapsed;
                }

                string userName = Environment.UserName;
                if (txtMetroUserName != null) txtMetroUserName.Text = userName;
                if (txtXpUserName != null) txtXpUserName.Text = userName;
                if (txtWin7UserName != null) txtWin7UserName.Text = userName;
                if (txtWin11UserName != null) txtWin11UserName.Text = userName;
            }
            catch { }
        }

        private void GenerateLetters()
        {
            var letters = new List<string>();
            for (char c = 'A'; c <= 'Z'; c++) letters.Add(c.ToString());
            letters.Add("#");
            letterPanel.ItemsSource = letters;
            if (letterPanelWin11 != null) letterPanelWin11.ItemsSource = letters;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox box = sender as TextBox;
            string keyword = box != null ? (box.Text ?? string.Empty).Trim() : string.Empty;

            ApplyAppFilter(keyword);
            UpdateSearchPlaceholders();

            if (box != null && box == txtSearchWin11) SyncWin11Grid();
        }

        private void ApplyAppFilter(string keyword)
        {
            Predicate<object> filter = null;
            if (!string.IsNullOrEmpty(keyword))
            {
                string kw = keyword.ToLowerInvariant();
                filter = o =>
                {
                    AppItem app = o as AppItem;
                    return app != null && app.Name != null && app.Name.ToLowerInvariant().Contains(kw);
                };
            }
            if (_viewWin10 != null) _viewWin10.Filter = filter;
            if (_viewWin7 != null) _viewWin7.Filter = filter;
            if (_viewWin11 != null) _viewWin11.Filter = filter;
        }

        private void UpdateSearchPlaceholders()
        {
            if (phSearch != null)
                phSearch.Visibility = (txtSearch != null && !string.IsNullOrEmpty(txtSearch.Text)) ? Visibility.Collapsed : Visibility.Visible;
            if (phSearchWin7 != null)
                phSearchWin7.Visibility = (txtSearchWin7 != null && !string.IsNullOrEmpty(txtSearchWin7.Text)) ? Visibility.Collapsed : Visibility.Visible;
            if (phSearchWin11 != null)
                phSearchWin11.Visibility = (txtSearchWin11 != null && !string.IsNullOrEmpty(txtSearchWin11.Text)) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void LetterWin11_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || btn.Content == null) return;

            string letter = btn.Content.ToString();
            AppItem item = SystemApps.FirstOrDefault(x => x.FirstLetter == letter);
            if (item != null) lstAppsWin11.ScrollIntoView(item);
        }

        private void Letter_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || btn.Content == null) return;
            string letter = btn.Content.ToString();
            var item = SystemApps.FirstOrDefault(x => x.FirstLetter == letter);
            if (item != null) lstApps.ScrollIntoView(item);
        }

        // ==================== 启动与右键 ====================
        private void Launch(AppItem app)
        {
            if (app == null || string.IsNullOrEmpty(app.Path)) return;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = app.Path, UseShellExecute = true });
            }
            catch { MessageBox.Show("找不到目标，可能已被删除。", "错误"); }
        }

        private void TileClick(object sender, RoutedEventArgs e)
        {
            if (ClickSuppressed) return;
            Launch((sender as Button)?.DataContext as AppItem);
        }

        private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ClickSuppressed) return;
            AppItem app = FindAppItemFromSource(e.OriginalSource as DependencyObject);
            if (app != null) Launch(app);
        }

        private AppItem GetMenuAppItem(object sender)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null) return null;
            ContextMenu cm = mi.Parent as ContextMenu;
            if (cm == null) return null;
            FrameworkElement fe = cm.PlacementTarget as FrameworkElement;
            return fe?.DataContext as AppItem;
        }

        private void SetTileSizeSmall(object sender, RoutedEventArgs e) { SetTileSize(sender, "Small"); }
        private void SetTileSizeMedium(object sender, RoutedEventArgs e) { SetTileSize(sender, "Medium"); }
        private void SetTileSizeWide(object sender, RoutedEventArgs e) { SetTileSize(sender, "Wide"); }
        private void SetTileSizeLarge(object sender, RoutedEventArgs e) { SetTileSize(sender, "Large"); }

        private void SetTileSize(object sender, string size)
        {
            AppItem app = GetMenuAppItem(sender);
            if (app == null) return;
            app.TileSize = size;
            RecalculateLayout();
            SyncAllTiles();
        }

        private void UnpinTile(object sender, RoutedEventArgs e)
        {
            AppItem app = GetMenuAppItem(sender);
            if (app == null) return;
            TileGroup group = app.ParentGroup;
            if (group != null)
            {
                group.Tiles.Remove(app);
                if (group.Tiles.Count == 0 && TileGroups.Count > 1) TileGroups.Remove(group);
            }
            RecalculateLayout();
            SyncAllTiles();
        }

        /// <summary>
        /// 拿到可动画的变换组。XAML 里的 Freezable 会被 WPF 自动冻结，
        /// 冻结对象不能 BeginAnimation，所以这里按需创建、发现冻结就重建。
        /// </summary>
        private static TransformGroup GetOrCreateTransform(Border tile)
        {
            TransformGroup group = tile.RenderTransform as TransformGroup;
            if (group != null && !group.IsFrozen && group.Children.Count >= 2) return group;

            group = new TransformGroup();
            group.Children.Add(new ScaleTransform(1, 1));
            group.Children.Add(new TranslateTransform(0, 0));

            tile.RenderTransformOrigin = new Point(0.5, 0.5);
            tile.RenderTransform = group;
            return group;
        }

        private static ScaleTransform GetFlipTransform(Border tile)
        {
            return GetOrCreateTransform(tile).Children[0] as ScaleTransform;
        }

        private static TranslateTransform GetSlideTransform(Border tile)
        {
            return GetOrCreateTransform(tile).Children[1] as TranslateTransform;
        }

        /// <summary>启动/切模式时磁贴逐个弹出：从小放大 + 上浮 + 淡入，错开 45ms</summary>
        private void PlayEntranceAnimation()
        {
            var tiles = new List<Border>();
            CollectTileBorders(tileGroupsControl, tiles);
            if (tiles.Count == 0) CollectTileBorders(metroTileControl, tiles);

            int index = 0;
            foreach (Border tile in tiles)
            {
                TimeSpan delay = TimeSpan.FromMilliseconds(index * 45);
                tile.Opacity = 0;

                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { BeginTime = delay };
                fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                tile.BeginAnimation(UIElement.OpacityProperty, fade);

                ScaleTransform scale = GetFlipTransform(tile);
                if (scale != null)
                {
                    var pop = new DoubleAnimation(0.7, 1.0, TimeSpan.FromMilliseconds(340)) { BeginTime = delay };
                    pop.EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
                }

                TranslateTransform slide = GetSlideTransform(tile);
                if (slide != null)
                {
                    var rise = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(340)) { BeginTime = delay };
                    rise.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                    slide.BeginAnimation(TranslateTransform.YProperty, rise);
                }
                index++;
            }
        }

        /// <summary>Win8 开始屏幕用桌面壁纸做背景（和真实一样不是纯色）</summary>
        private void ApplyWin8Backdrop()
        {
            try
            {
                if (imgWin8Backdrop == null) return;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    string path = key != null ? key.GetValue("WallPaper") as string : null;
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(path);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 1920;
                    image.EndInit();
                    image.Freeze();
                    imgWin8Backdrop.Source = image;
                }
            }
            catch { }
        }

        /// <summary>Win8 所有应用：从下滑入 / 向上收回</summary>
        private void SlideAppsView(bool show)
        {
            TranslateTransform slide = pnlAllAppsView.RenderTransform as TranslateTransform;
            if (slide == null) { slide = new TranslateTransform(0, 0); pnlAllAppsView.RenderTransform = slide; }

            if (show)
            {
                pnlAllAppsView.Visibility = Visibility.Visible;
                btnDownArrow.Visibility = Visibility.Collapsed;

                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190));
                fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                var move = new DoubleAnimation(70, 0, TimeSpan.FromMilliseconds(240));
                move.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

                pnlAllAppsView.BeginAnimation(UIElement.OpacityProperty, fade);
                slide.BeginAnimation(TranslateTransform.YProperty, move);
            }
            else
            {
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fade.Completed += (s, a) => pnlAllAppsView.Visibility = Visibility.Collapsed;
                var move = new DoubleAnimation(0, 70, TimeSpan.FromMilliseconds(150));
                move.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };

                pnlAllAppsView.BeginAnimation(UIElement.OpacityProperty, fade);
                slide.BeginAnimation(TranslateTransform.YProperty, move);
                btnDownArrow.Visibility = Visibility.Visible;
            }
        }

        private void ToggleAppsView(object sender, RoutedEventArgs e)
        {
            SlideAppsView(pnlAllAppsView.Visibility != Visibility.Visible);
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
            menu.PlacementTarget = sender as UIElement;
            menu.IsOpen = true;
        }

        private void UserAccount_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var lockItem = new MenuItem { Header = "锁定" }; lockItem.Click += (s, args) => SystemHelper.Lock();
            var logoffItem = new MenuItem { Header = "注销" }; logoffItem.Click += (s, args) => SystemHelper.Logoff();
            menu.Items.Add(lockItem); menu.Items.Add(logoffItem);
            menu.PlacementTarget = sender as UIElement;
            menu.IsOpen = true;
        }

        private void OpenDocuments_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenFolder(Environment.SpecialFolder.MyDocuments);
        private void OpenPictures_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenFolder(Environment.SpecialFolder.MyPictures);
        private void OpenMusic_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenFolder(Environment.SpecialFolder.MyMusic);
        private void OpenComputer_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenFolder(Environment.SpecialFolder.MyComputer);
        private void OpenSettings_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenSettings();
        private void OpenDevices_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenSettings();
        private void OpenDefaultPrograms_Click(object sender, RoutedEventArgs e) => SystemHelper.OpenSettings();
        private void OpenHelp_Click(object sender, RoutedEventArgs e) => MessageBox.Show("帮助和支持", "WinLauncher");

        private void OpenDownloads_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            catch { }
        }

        private void OpenAllProgramsFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
                if (!Directory.Exists(dir))
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            catch { }
        }

        private void OpenSearchBox(object sender, RoutedEventArgs e)
        {
            ApplyLayoutMode("Win10");

            // 调试用：WinLauncher.exe --mode=Win8 直接进某个模式
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                    ApplyLayoutMode(arg.Substring(7));
            }
            if (txtSearch != null) txtSearch.Focus();
        }

        private void OpenRunDialog(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { MessageBox.Show("无法打开运行对话框：" + ex.Message, "错误"); }
        }

        private void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var sleep = new MenuItem { Header = "睡眠" }; sleep.Click += (s, args) => SystemHelper.Sleep();
            var shutdown = new MenuItem { Header = "关机" }; shutdown.Click += (s, args) => SystemHelper.Shutdown();
            var restart = new MenuItem { Header = "重启" }; restart.Click += (s, args) => SystemHelper.Restart();
            menu.Items.Add(sleep); menu.Items.Add(shutdown); menu.Items.Add(restart);
            menu.PlacementTarget = sender as UIElement;
            menu.IsOpen = true;
        }

        private void XpLogOff_Click(object sender, RoutedEventArgs e)
        {
            SystemHelper.Logoff();
        }

        private void XpShutdown_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var shutdown = new MenuItem { Header = "关闭" }; shutdown.Click += (s, args) => SystemHelper.Shutdown();
            var restart = new MenuItem { Header = "重新启动" }; restart.Click += (s, args) => SystemHelper.Restart();
            var sleep = new MenuItem { Header = "待机" }; sleep.Click += (s, args) => SystemHelper.Sleep();
            menu.Items.Add(shutdown); menu.Items.Add(restart); menu.Items.Add(sleep);
            menu.PlacementTarget = sender as UIElement;
            menu.IsOpen = true;
        }

        private void PinToTaskbar_Click(object sender, RoutedEventArgs e)
        {
            AppItem app = (sender as MenuItem)?.DataContext as AppItem;
            if (app == null) return;

            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return;
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

        /// <summary>拖起来时给磁贴加一层投影，视觉上"浮"起来</summary>
        private static DropShadowEffect LiftShadow()
        {
            return new DropShadowEffect
            {
                BlurRadius = 26,
                ShadowDepth = 7,
                Direction = 270,
                Opacity = 0.55,
                Color = Colors.Black
            };
        }

        private Border FindTileBorder(DependencyObject node)
        {
            int guard = 0;
            while (node != null && guard++ < 48)
            {
                Border b = node as Border;
                if (b != null && (b.Tag as string) == "tile") return b;
                node = GetParentSafe(node);
            }
            return null;
        }

        private void PlaySettleAnimation()
        {
            var tiles = new List<Border>();
            CollectTileBorders(tileGroupsControl, tiles);
            foreach (Border tile in tiles)
            {
                var fade = new DoubleAnimation(0.5, 1.0, TimeSpan.FromMilliseconds(230));
                fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                tile.BeginAnimation(UIElement.OpacityProperty, fade);
            }
        }

        private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            AppItem app = (sender as MenuItem)?.DataContext as AppItem;
            if (app == null || string.IsNullOrEmpty(app.Path)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = app.Path,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Win32Exception) { }
            catch (Exception ex) { MessageBox.Show("无法以管理员身份运行：" + ex.Message, "错误"); }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            AppItem app = (sender as MenuItem)?.DataContext as AppItem;
            if (app == null) return;

            try
            {
                string realPath = IconHelper.GetRealPath(app.Path);
                if (!string.IsNullOrEmpty(realPath) && File.Exists(realPath))
                    Process.Start("explorer.exe", "/select,\"" + realPath + "\"");
                else
                    Process.Start("explorer.exe", "/select,\"" + app.Path + "\"");
            }
            catch (Exception ex) { MessageBox.Show("无法打开文件位置: " + ex.Message, "错误"); }
        }

        private void ChangeTileColor_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            ContextMenu cm = mi?.Parent as ContextMenu;
            FrameworkElement fe = cm?.PlacementTarget as FrameworkElement;
            AppItem app = fe?.DataContext as AppItem;
            if (app == null) return;

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
            string[] presets = { "#0078D7", "#005A9E", "#10893E", "#5C2D91", "#CA5010", "#00B294", "#2D7D9A", "#7A7574", "#2D2D30", "#4A4A4A" };
            foreach (var hex in presets)
            {
                string colorHex = hex;
                var btn = new Button
                {
                    Width = 40,
                    Height = 40,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                btn.Click += (s, args) => { app.TileColor = colorHex; colorWin.Close(); SaveConfig(); };
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
                try
                {
                    ColorConverter.ConvertFromString(tb.Text);
                    app.TileColor = tb.Text;
                    SaveConfig();
                    colorWin.Close();
                }
                catch { MessageBox.Show("颜色代码无效，格式应为 #RRGGBB，例如 #FF0000。", "错误"); }
            };
            stack.Children.Add(applyBtn);

            colorWin.Content = stack;
            colorWin.ShowDialog();
        }

        private Point _groupDragStart;
        private TileGroup _draggedGroup;

        private void GroupTitle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _groupDragStart = e.GetPosition(null);
            _draggedGroup = (sender as FrameworkElement)?.DataContext as TileGroup;
        }

        private void GroupTitle_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedGroup == null) return;

            Vector diff = _groupDragStart - e.GetPosition(null);
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("GroupFormat", _draggedGroup), DragDropEffects.Move);
            _draggedGroup = null;
        }

        /// <summary>把一个分组拖到另一个分组上：交换两组的先后顺序</summary>
        private void GroupReorder_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("GroupFormat")) return;

            TileGroup src = e.Data.GetData("GroupFormat") as TileGroup;
            TileGroup dst = (sender as FrameworkElement)?.DataContext as TileGroup;
            if (src == null || dst == null || src == dst) return;

            int from = TileGroups.IndexOf(src);
            int to = TileGroups.IndexOf(dst);
            if (from < 0 || to < 0) return;

            TileGroups.Move(from, to);
            RecalculateLayout();
            PlaySettleAnimation();
            e.Handled = true;
        }

        private void GroupTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            TileGroup group = (sender as TextBlock)?.DataContext as TileGroup;
            if (group == null) return;
            ShowRenameDialog(group);
        }

        private void RenameGroup_Click(object sender, RoutedEventArgs e)
        {
            TileGroup group = (sender as MenuItem)?.DataContext as TileGroup;
            if (group != null) ShowRenameDialog(group);
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            TileGroup group = (sender as MenuItem)?.DataContext as TileGroup;
            if (group == null) return;

            if (TileGroups.Count <= 1)
            {
                MessageBox.Show("至少要保留一个分组。", "无法删除");
                return;
            }

            string msg = "删除分组「" + group.GroupTitle + "」？\n\n里面的 " + group.Tiles.Count + " 个磁贴会一起删掉，不能撤销。";
            if (MessageBox.Show(msg, "删除分组", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            TileGroups.Remove(group);
            RecalculateLayout();
            SyncAllTiles();
            PlaySettleAnimation();
        }

        private void ShowRenameDialog(TileGroup group)
        {
            if (group == null) return;

            var inputDialog = new Window
            {
                Width = 300,
                Height = 150,
                Title = "重命名分组",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = Brushes.DarkGray,
                WindowStyle = WindowStyle.ToolWindow
            };
            var stack = new StackPanel { Margin = new Thickness(20) };
            var tb = new TextBox { Text = group.GroupTitle, Height = 30, Margin = new Thickness(0, 0, 0, 20) };
            var btn = new Button { Content = "确定", Height = 30, Width = 80 };
            btn.Click += (s, args) => { group.GroupTitle = tb.Text; inputDialog.Close(); SaveConfig(); };
            stack.Children.Add(tb);
            stack.Children.Add(btn);
            inputDialog.Content = stack;
            inputDialog.ShowDialog();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (e.ClickCount == 2)
            {
                Maximize_Click(sender, e);
                return;
            }
            try { DragMove(); } catch { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (_isMaximized) ExitPseudoMaximize();
            else EnterPseudoMaximize();
        }

        /// <summary>
        /// 占满工作区但不盖任务栏。WindowState.Maximized 对 WindowStyle=None 的窗口会连任务栏
        /// 和顶部工具条一起盖掉，按钮就"被吃掉"了，所以这里自己算尺寸。
        /// </summary>
        private void EnterPseudoMaximize()
        {
            if (_isMaximized) return;

            _restoreBounds = new Rect(Left, Top, Width, Height);

            Rect wa = SystemParameters.WorkArea;
            Left = wa.Left;
            Top = wa.Top;
            Width = wa.Width;
            Height = wa.Height;

            _isMaximized = true;
            if (btnMaximize != null) btnMaximize.Content = "\u2750";
            if (MainBorder != null) MainBorder.CornerRadius = new CornerRadius(0);
            UpdateChromeForState();
        }

        private void ExitPseudoMaximize()
        {
            if (!_isMaximized) return;

            _isMaximized = false;
            if (!_restoreBounds.IsEmpty && _restoreBounds.Width > 120 && _restoreBounds.Height > 120)
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
            }

            if (btnMaximize != null) btnMaximize.Content = "\u25A1";
            if (MainBorder != null) MainBorder.CornerRadius = new CornerRadius(8);
            UpdateChromeForState();
        }
    }

    // ==================== 配置数据模型 DTO（保持与旧 config.json 完全一致） ====================
    public class AppConfigDTO
    {
        public List<TileGroupDTO> Groups { get; set; }
        public string ThemeMode { get; set; }
        public string MetroBgColor { get; set; }
        public string Language { get; set; }
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