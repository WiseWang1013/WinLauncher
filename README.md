# WinLauncher

仿 Windows XP / 7 / 8 / 10 / 11 开始菜单的独立启动器，自带动态磁贴。

## ✨ 3.0 新特性

- **五种开始菜单**：新增 Windows XP 经典双栏（蓝色渐变标题条、左白右浅蓝、底部橙色分隔线 + 注销 / 关闭计算机），原有 Win7 / 8 / 10 / 11 全部重做
- **六种界面语言**：简体中文 / English / Français / العربية / Русский / Español，`≡` 菜单里实时切换，不用重启；阿拉伯语自动切 RTL
- **完整的磁贴动画**：拿起时投影浮起、拖动中其他磁贴实时让位、松手缓动落位、切换模式淡入回弹
- **触摸拖拽重写**：长按 500ms 后改走手动拖拽，不再依赖 OLE 拖放，手指按住即可拖动磁贴
- **启动自动同步**：每次启动重扫开始菜单，已卸载程序的磁贴自动清理，新安装的应用自动进列表
- **分组管理**：右键分组标题可重命名 / 删除（连同磁贴）

## 🎯 核心功能

### 五种模式

| 模式 | 特征 |
|---|---|
| Windows XP | 双栏、蓝色渐变标题条、浅蓝链接区、橙色分隔线 |
| Windows 7 | 深色玻璃外框、左侧白色列表、右侧深色链接区 + 底部搜索 |
| Windows 8 | 全屏磁贴、桌面壁纸做背景、向下键滑出「所有应用」 |
| Windows 10 | 左侧图标条 + 字母索引列表 + 右侧磁贴组 |
| Windows 11 | 搜索框 + 已固定网格（默认两行）+ 全部（字母分组）+ 底部工具栏 |

### 磁贴系统

- Canvas 绝对定位 + 二维装箱算法，自动填补缝隙、自动换行
- 小 / 中 / 宽 / 大 四种尺寸，改尺寸后其他磁贴自动让位
- 拖拽换位：鼠标走 OLE 拖放，触摸走手动拖拽，两条路径共用同一套让位逻辑
- 拖到磁贴区底部固定的投放条即可新建分组
- 右键菜单：自定义颜色 / 调整大小 / 打开文件所在位置 / 以管理员身份运行 / 取消固定

### 应用列表

- 扫描系统开始菜单（所有用户 + 当前用户），提取无箭头图标
- 按首字母分组：英文取大写、中文按 GB2312 拼音区间映射、二级汉字（哔 / 囧 / 喵 等）查补充表
- 右侧字母索引条，点击跳转到对应分组
- 右键菜单：固定到任务栏 / 打开文件所在位置 / 以管理员身份运行

### 多语言

`Languages/` 下每种语言一个类，实现 `ILanguage` 接口（`Code` / `DisplayName` / `RightToLeft` / `Strings`）。
新增语言只需：

1. 在 `Languages/` 加一个实现 `ILanguage` 的类
2. 在 `LanguageService.All` 里注册

界面文本通过 `{DynamicResource L.xxx}` 绑定，切换语言时更新 `Window.Resources`，实时刷新，无需重启。

### 其他

- **皮肤系统**：导入 JSON 皮肤，可定制各模式的颜色、圆角、按钮样式，支持删除
- **配置管理**：自动保存 / 导出备份 / 导入旧配置 / 一键还原
- **窗口行为**：占满工作区（不覆盖任务栏）、Esc 退出、双击标题栏最大化

## 🛠️ 技术栈与环境

- **框架**：WPF (.NET Framework 4.8)
- **工具**：Visual Studio 2022
- **第三方依赖**：仅 Newtonsoft.Json
- **运行环境**：Windows 7 SP1 / 8 / 10 / 11（Win7 需自行安装 .NET Framework 4.8）

## 🚀 编译运行

1. 克隆仓库
2. 用 Visual Studio 打开 `WinLauncher.slnx`
3. 还原 NuGet 包，重新生成解决方案
4. `F5` 运行

> 命令行编译：`msbuild WinLauncher.slnx /t:Rebuild /p:Configuration=Debug`
> 不要用 `dotnet build`——老式 csproj 带 COM 引用，会报 MSB4803。

## 📦 打包发布

项目附带 `installer.iss`（Inno Setup 6 脚本）：

1. VS 切到 `Release` 编译
2. 把 `bin\Release` 下的 `WinLauncher.exe`、`WinLauncher.exe.config`、`Newtonsoft.Json.dll` 三个文件放进 `D:\WinLauncher`
3. 用 Inno Setup 打开 `installer.iss` 编译

脚本只打包这三个文件，`.pdb` / `.xml` / 未使用的 dll 一律不打包；已配置六种语言的安装界面，并会保留原有的 `AppId` 以便覆盖升级。

## 📁 目录结构

```
WinLauncher\
├─ App.config / App.xaml / App.xaml.cs
├─ MainWindow.xaml / MainWindow.xaml.cs
├─ Helpers\          Acrylic / Icon / Pinyin / System / Hotkey / Json / Lnk / LanguageService
├─ Models\           AppItem / TileGroup / SkinModel / SettingsModel
├─ Languages\        ILanguage + 六种语言实现
├─ Properties\       AssemblyInfo 等
├─ packages.config
└─ WinLauncher.csproj
```

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源协议。
