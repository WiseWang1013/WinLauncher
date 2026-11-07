using System.Collections.Generic;

namespace WinLauncher.Languages
{
    /// <summary>中文（简体，中国）—— 术语对照 Windows 11 简体中文版</summary>
    public class ZhCN : ILanguage
    {
        public string Code { get { return "zh-CN"; } }
        public string DisplayName { get { return "中文（简体）"; } }
        public bool RightToLeft { get { return false; } }

        public Dictionary<string, string> Strings
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "Menu", "菜单与设置" },
                    { "LayoutStyle", "开始菜单样式" },
                    { "Theme", "深浅主题" },
                    { "Dark", "深色" },
                    { "Light", "浅色" },
                    { "MetroBg", "Win8 背景色" },
                    { "Skins", "已导入皮肤（右键删除）" },
                    { "ImportSkin", "+ 导入皮肤 JSON" },
                    { "ConfigSection", "配置管理" },
                    { "ImportConfig", "导入旧配置" },
                    { "ExportConfig", "导出当前配置" },
                    { "ResetConfig", "一键还原（清空配置）" },
                    { "Language", "语言" },

                    { "Search", "搜索" },
                    { "SearchTip", "搜索应用、设置和文档" },
                    { "SearchPrograms", "搜索程序和文件" },

                    { "Pinned", "已固定" },
                    { "PinnedToStart", "固定到「开始」屏幕" },
                    { "All", "全部" },
                    { "AllApps", "所有应用" },
                    { "ShowAll", "全部显示" },
                    { "Collapse", "收起" },
                    { "PinnedHeader", "固定项目" },

                    { "Documents", "文档" },
                    { "Pictures", "图片" },
                    { "Music", "音乐" },
                    { "Computer", "计算机" },
                    { "Downloads", "下载" },
                    { "ControlPanel", "控制面板" },
                    { "Devices", "设备和打印机" },
                    { "DefaultPrograms", "默认程序" },
                    { "Help", "帮助和支持" },
                    { "Run", "运行..." },

                    { "MyDocuments", "我的文档" },
                    { "MyPictures", "我的图片" },
                    { "MyMusic", "我的音乐" },
                    { "MyComputer", "我的电脑" },
                    { "Printers", "打印机和传真" },
                    { "SearchText", "搜索" },

                    { "SignOut", "注销" },
                    { "ShutDown", "关闭计算机" },
                    { "PowerOff", "关机" },
                    { "Restart", "重启" },
                    { "Sleep", "睡眠" },
                    { "Lock", "锁定" },
                    { "Power", "电源" },
                    { "Settings", "设置" },
                    { "UserAccount", "当前用户" },

                    { "PinTaskbar", "固定到任务栏" },
                    { "OpenLocation", "打开文件所在位置" },
                    { "RunAsAdmin", "以管理员身份运行" },
                    { "Unpin", "取消固定" },
                    { "CustomColor", "自定义颜色" },
                    { "Small", "小图" },
                    { "Medium", "中图" },
                    { "Wide", "宽图" },
                    { "Large", "大图" },
                    { "RenameGroup", "重命名分组" },
                    { "DeleteGroup", "删除分组（连同磁贴）" },
                    { "NewGroupHint", "+ 拖到这里新建分组" },
                    { "AllPrograms", "所有程序" }
                };
            }
        }
    }
}
