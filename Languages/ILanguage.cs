using System.Collections.Generic;

namespace WinLauncher.Languages
{
    /// <summary>一种语言：代码（zh-CN）、显示名、字符串表</summary>
    public interface ILanguage
    {
        string Code { get; }
        string DisplayName { get; }
        bool RightToLeft { get; }
        Dictionary<string, string> Strings { get; }
    }
}
