using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinLauncher.Helpers
{
    public static class IconHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static string GetRealPath(string filePath)
        {
            try
            {
                if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(filePath);
                    if (!string.IsNullOrEmpty(shortcut.TargetPath) && (System.IO.File.Exists(shortcut.TargetPath) || System.IO.Directory.Exists(shortcut.TargetPath)))
                    {
                        return shortcut.TargetPath;
                    }
                }
            }
            catch { }
            return filePath;
        }

        public static ImageSource GetIconWithoutArrow(string filePath)
        {
            try
            {
                string realPath = GetRealPath(filePath);
                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hImg = SHGetFileInfo(realPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), 0x100 | 0x0);
                if (hImg != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    ImageSource iconSource = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    DestroyIcon(shinfo.hIcon);
                    return iconSource;
                }
            }
            catch { }
            return null;
        }
    }
}