using System;
using System.Runtime.InteropServices;

namespace WinLauncher.Helpers
{
    public static class PinyinHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int LCMapString(int Locale, int dwMapFlags, string lpSrcStr, int cchSrc, [Out] char[] lpDestStr, int cchDest);

        private const int LCMAP_SILENT = 0x00000001;
        private const int LCMAP_SIMPLIFIED_CHINESE = 0x02000000;
        private const int LOCALE_SYSTEM_DEFAULT = 0x0800;

        public static string GetFirstLetter(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "#";

            char firstChar = text[0];
            if ((firstChar >= 'a' && firstChar <= 'z') || (firstChar >= 'A' && firstChar <= 'Z'))
                return firstChar.ToString().ToUpper();
            if (char.IsDigit(firstChar)) return "#";

            try
            {
                char[] buffer = new char[256];
                int len = LCMapString(LOCALE_SYSTEM_DEFAULT, LCMAP_SIMPLIFIED_CHINESE | LCMAP_SILENT, text, text.Length, buffer, buffer.Length);
                if (len > 0)
                {
                    string pinyin = new string(buffer, 0, len);
                    if (!string.IsNullOrEmpty(pinyin))
                    {
                        char pyFirst = pinyin[0];
                        if ((pyFirst >= 'a' && pyFirst <= 'z') || (pyFirst >= 'A' && pyFirst <= 'Z'))
                            return pyFirst.ToString().ToUpper();
                    }
                }
            }
            catch { }
            return "#";
        }
    }
}