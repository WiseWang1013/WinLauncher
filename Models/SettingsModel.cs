using System.Collections.Generic;

namespace WinLauncher.Models
{
    /// <summary>
    /// 程序设置数据模型（用于 JSON 保存/读取配置）
    /// </summary>
    public class SettingsModel
    {
        // 主题颜色（默认 Win10 蓝）
        public string ThemeColor { get; set; } = "#0078D7";

        // 是否开机自动启动
        public bool IsAutoStart { get; set; } = false;

        // 全局呼出快捷键
        public string Hotkey { get; set; } = "Alt+Space";

        // 额外的自定义扫描路径（除了系统开始菜单外）
        public List<string> CustomScanPaths { get; set; } = new List<string>();

        // 磁贴透明度
        public double TileOpacity { get; set; } = 0.9;
    }
}