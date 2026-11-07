using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace WinLauncher.Helpers
{
    /// <summary>
    /// 窗口背景特效。按系统版本走不同实现：
    ///   Windows 7        -> DwmEnableBlurBehindWindow（Aero 毛玻璃）
    ///   Windows 8 / 8.1  -> SetWindowCompositionAttribute + BLURBEHIND
    ///   Windows 10 1803+ -> SetWindowCompositionAttribute + ACRYLICBLURBEHIND
    /// 任何一步失败都不会抛到调用方，最多是"没有特效"。
    /// </summary>
    public static class AcrylicHelper
    {
        // ---------- 通用 ----------
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll")]
        private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND blurBehind);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_ENABLE_HOSTBACKDROP = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_BLURBEHIND
        {
            public uint dwFlags;
            public int fEnable;
            public IntPtr hRgnBlur;
            public int fTransitionOnMaximized;
        }

        private const uint DWM_BB_ENABLE = 0x00000001;

        // ---------- 版本探测 ----------
        private static Version _cachedVersion;

        private static Version GetRealVersion()
        {
            if (_cachedVersion != null) return _cachedVersion;
            try
            {
                // 未声明 supportedOS 时 Environment.OSVersion 会谎报 6.2，优先读注册表
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        object major = key.GetValue("CurrentMajorVersionNumber");
                        object minor = key.GetValue("CurrentMinorVersionNumber");
                        object build = key.GetValue("CurrentBuildNumber");
                        if (major is int m)
                        {
                            int mi = minor is int mn ? mn : 0;
                            int b = 0;
                            if (build != null) int.TryParse(build.ToString(), out b);
                            _cachedVersion = new Version(m, mi, b);
                            return _cachedVersion;
                        }
                    }
                }
            }
            catch { }
            _cachedVersion = Environment.OSVersion.Version;
            return _cachedVersion;
        }

        /// <summary>Windows 7 / Server 2008 R2 = 6.1</summary>
        public static bool IsWin7
        {
            get { Version v = GetRealVersion(); return v.Major == 6 && v.Minor == 1; }
        }

        /// <summary>Win10 1803（17134）以上才真正支持亚克力</summary>
        private static bool SupportsAcrylic
        {
            get { Version v = GetRealVersion(); return v.Major >= 10 && v.Build >= 17134; }
        }

        // ---------- 对外 ----------
        public static void EnableAcrylic(Window window, byte opacity = 180, uint tintColor = 0)
        {
            if (window == null) return;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (IsWin7)
            {
                EnableWin7Blur(hwnd);
                return;
            }

            try
            {
                uint gradient = ((uint)opacity << 24) | (tintColor & 0xFFFFFF);
                var accent = new AccentPolicy
                {
                    AccentState = SupportsAcrylic ? AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND : AccentState.ACCENT_ENABLE_BLURBEHIND,
                    AccentFlags = 0,
                    GradientColor = unchecked((int)gradient),
                    AnimationId = 0
                };
                ApplyAccent(hwnd, accent);
            }
            catch
            {
                // Win7 / 精简系统：退回 DWM 毛玻璃
                EnableWin7Blur(hwnd);
            }
        }

        public static void DisableAcrylic(Window window)
        {
            if (window == null) return;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_DISABLED,
                    AccentFlags = 0,
                    GradientColor = 0,
                    AnimationId = 0
                };
                ApplyAccent(hwnd, accent);
            }
            catch { }

            try
            {
                var bb = new DWM_BLURBEHIND { dwFlags = DWM_BB_ENABLE, fEnable = 0, hRgnBlur = IntPtr.Zero, fTransitionOnMaximized = 0 };
                DwmEnableBlurBehindWindow(hwnd, ref bb);
            }
            catch { }
        }

        // ---------- 内部 ----------
        private static void ApplyAccent(IntPtr hwnd, AccentPolicy accent)
        {
            int size = Marshal.SizeOf(typeof(AccentPolicy));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = size,
                    Data = ptr
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static void EnableWin7Blur(IntPtr hwnd)
        {
            try
            {
                var bb = new DWM_BLURBEHIND { dwFlags = DWM_BB_ENABLE, fEnable = 1, hRgnBlur = IntPtr.Zero, fTransitionOnMaximized = 1 };
                DwmEnableBlurBehindWindow(hwnd, ref bb);
            }
            catch { }
        }
    }
}
