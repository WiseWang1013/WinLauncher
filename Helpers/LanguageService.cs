using System;
using System.Collections.Generic;
using System.Windows;
using WinLauncher.Languages;

namespace WinLauncher.Helpers
{
    /// <summary>
    /// 语言服务：把当前语言的字符串写进 Window.Resources（key 前缀 "L."），
    /// XAML 里用 {DynamicResource L.xxx} 就能随切换实时刷新。
    /// </summary>
    public static class LanguageService
    {
        public static readonly List<ILanguage> All = new List<ILanguage>
        {
            new ZhCN(),
            new EnUS(),
            new FrFR(),
            new ArSA(),
            new RuRU(),
            new EsES()
        };

        public static string CurrentCode { get; private set; }

        static LanguageService()
        {
            CurrentCode = All[0].Code;
        }

        public static ILanguage Current
        {
            get
            {
                foreach (ILanguage lang in All)
                    if (lang.Code == CurrentCode) return lang;
                return All[0];
            }
        }

        /// <summary>把语言写入窗口资源，并按需要翻转布局方向（阿拉伯语 RTL）</summary>
        public static void Apply(Window window, string code)
        {
            ILanguage target = All[0];
            if (!string.IsNullOrEmpty(code))
            {
                foreach (ILanguage lang in All)
                {
                    if (string.Equals(lang.Code, code, StringComparison.OrdinalIgnoreCase)) { target = lang; break; }
                }
            }

            CurrentCode = target.Code;

            if (window == null) return;

            window.FlowDirection = target.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            foreach (KeyValuePair<string, string> pair in target.Strings)
                window.Resources["L." + pair.Key] = pair.Value;
        }

        public static string Get(string key)
        {
            string value;
            return Current.Strings.TryGetValue(key, out value) ? value : key;
        }
    }
}
