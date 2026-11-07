using System;
using System.Collections.Generic;
using System.Text;

namespace WinLauncher.Helpers
{
    /// <summary>
    /// 取名字的首字母：英文取大写，中文按 GB2312 一级汉字区间映射拼音首字母，
    /// 二级汉字（如"哔"）落在编码范围之外，用一张高频字表兜底。
    /// </summary>
    public static class PinyinHelper
    {
        // GB2312 一级汉字（0xB0A1-0xD7F9）按拼音排序，23 段起始码
        private static readonly int[] Bounds =
        {
            0xB0A1, 0xB0C5, 0xB2C1, 0xB4EE, 0xB6EA, 0xB7A2, 0xB8C1,
            0xB9FE, 0xBBF7, 0xBFA6, 0xC0AC, 0xC2E8, 0xC4C3, 0xC5B6,
            0xC5BE, 0xC6DA, 0xC8BB, 0xC8F6, 0xCBFA, 0xCDDA, 0xCEF4,
            0xD1B9, 0xD4D1
        };

        private const string Letters = "ABCDEFGHJKLMNOPQRSTWXYZ";

        // 二级汉字不按拼音排序，只能查表；这里放应用名里常见的
        private static readonly Dictionary<char, string> Extra = new Dictionary<char, string>
        {
            { '哔', "B" }, { '啵', "B" }, { '囧', "J" }, { '嗯', "E" }, { '喵', "M" },
            { '嗨', "H" }, { '嘿', "H" }, { '噢', "O" }, { '咦', "Y" }, { '嘻', "X" },
            { '呦', "Y" }, { '嘞', "L" }, { '喏', "N" }, { '哞', "M" }, { '咩', "M" },
            { '咯', "G" }, { '嘀', "D" }, { '嘟', "D" }, { '嘭', "P" }, { '唰', "S" },
            { '啾', "J" }, { '嗒', "D" }, { '嗑', "K" }, { '嗦', "S" }, { '嘣', "B" }
        };

        public static string GetFirstLetter(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "#";

            char first = text[0];

            if (first >= 'a' && first <= 'z') return char.ToUpperInvariant(first).ToString();
            if (first >= 'A' && first <= 'Z') return first.ToString();
            if (first >= '0' && first <= '9') return "#";

            string mapped;
            if (Extra.TryGetValue(first, out mapped)) return mapped;

            try
            {
                byte[] bytes = Encoding.GetEncoding(936).GetBytes(first.ToString());
                if (bytes.Length == 2)
                {
                    int code = bytes[0] * 256 + bytes[1];
                    if (code >= Bounds[0] && code <= 0xD7F9)
                    {
                        for (int i = Bounds.Length - 1; i >= 0; i--)
                        {
                            if (code >= Bounds[i]) return Letters[i].ToString();
                        }
                    }
                }
            }
            catch { }

            return "#";
        }
    }
}
