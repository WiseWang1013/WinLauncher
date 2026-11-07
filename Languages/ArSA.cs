using System.Collections.Generic;

namespace WinLauncher.Languages
{
    /// <summary>العربية (السعودية) — RTL，术语对照 Windows 11 ar-SA</summary>
    public class ArSA : ILanguage
    {
        public string Code { get { return "ar-SA"; } }
        public string DisplayName { get { return "العربية (السعودية)"; } }
        public bool RightToLeft { get { return true; } }

        public Dictionary<string, string> Strings
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "Menu", "القائمة والإعدادات" },
                    { "LayoutStyle", "نمط قائمة البدء" },
                    { "Theme", "السمة الفاتحة أو الداكنة" },
                    { "Dark", "داكن" },
                    { "Light", "فاتح" },
                    { "MetroBg", "خلفية Windows 8" },
                    { "Skins", "السمات المستوردة (انقر بزر الماوس الأيمن للحذف)" },
                    { "ImportSkin", "+ استيراد سمة JSON" },
                    { "ConfigSection", "التكوين" },
                    { "ImportConfig", "استيراد تكوين قديم" },
                    { "ExportConfig", "تصدير التكوين الحالي" },
                    { "ResetConfig", "إعادة تعيين كل شيء" },
                    { "Language", "اللغة" },

                    { "Search", "بحث" },
                    { "SearchTip", "البحث عن التطبيقات والإعدادات والمستندات" },
                    { "SearchPrograms", "البحث في البرامج والملفات" },

                    { "Pinned", "مثبت" },
                    { "PinnedToStart", "التثبيت على شاشة البدء" },
                    { "All", "الكل" },
                    { "AllApps", "كل التطبيقات" },
                    { "ShowAll", "إظهار الكل" },
                    { "Collapse", "طي" },
                    { "PinnedHeader", "مثبت" },

                    { "Documents", "المستندات" },
                    { "Pictures", "الصور" },
                    { "Music", "الموسيقى" },
                    { "Computer", "الكمبيوتر" },
                    { "Downloads", "التنزيلات" },
                    { "ControlPanel", "لوحة التحكم" },
                    { "Devices", "الأجهزة والطابعات" },
                    { "DefaultPrograms", "البرامج الافتراضية" },
                    { "Help", "التعليمات والدعم" },
                    { "Run", "تشغيل..." },

                    { "MyDocuments", "مستنداتي" },
                    { "MyPictures", "صوري" },
                    { "MyMusic", "موسيقاي" },
                    { "MyComputer", " جهاز الكمبيوتر" },
                    { "Printers", "الطابعات والفاكسات" },
                    { "SearchText", "بحث" },

                    { "SignOut", "تسجيل الخروج" },
                    { "ShutDown", "إيقاف التشغيل" },
                    { "PowerOff", "إيقاف التشغيل" },
                    { "Restart", "إعادة التشغيل" },
                    { "Sleep", "السكون" },
                    { "Lock", "قفل" },
                    { "Power", "الطاقة" },
                    { "Settings", "الإعدادات" },
                    { "UserAccount", "المستخدم الحالي" },

                    { "PinTaskbar", "التثبيت على شريط المهام" },
                    { "OpenLocation", "فتح موقع الملف" },
                    { "RunAsAdmin", "التشغيل كمسؤول" },
                    { "Unpin", "إلغاء التثبيت من شاشة البدء" },
                    { "CustomColor", "لون مخصص" },
                    { "Small", "صغير" },
                    { "Medium", "متوسط" },
                    { "Wide", "عريض" },
                    { "Large", "كبير" },
                    { "RenameGroup", "إعادة تسمية المجموعة" },
                    { "DeleteGroup", "حذف المجموعة (مع المربعات)" },
                    { "NewGroupHint", "+ اسحب هنا لإنشاء مجموعة" },
                    { "AllPrograms", "كل البرامج" }
                };
            }
        }
    }
}
