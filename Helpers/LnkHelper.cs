using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinLauncher.Models;

namespace WinLauncher.Helpers
{
    public static class LnkHelper
    {
        // 使用 Win32 API 提取图标，不需要 System.Drawing，绝对不报错
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, StringBuilder lpIconPath, out ushort lpiIcon);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static List<AppItem> GetSystemApps()
        {
            var apps = new List<AppItem>();
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
                        apps.Add(new AppItem
                        {
                            Name = name,
                            Path = file,
                            Icon = GetIcon(file)
                        });
                    }
                }
                catch { /* 忽略无权限访问的目录 */ }
            }
            return apps;
        }

        private static ImageSource GetIcon(string filePath)
        {
            try
            {
                var sb = new StringBuilder(filePath, 260);
                IntPtr hIcon = ExtractAssociatedIcon(IntPtr.Zero, sb, out _);
                if (hIcon != IntPtr.Zero)
                {
                    ImageSource iconSource = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    DestroyIcon(hIcon);
                    return iconSource;
                }
            }
            catch { }
            return null;
        }
    }
}