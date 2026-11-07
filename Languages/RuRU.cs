using System.Collections.Generic;

namespace WinLauncher.Languages
{
    /// <summary>Русский (Россия) — терминология как в Windows 11 ru-RU</summary>
    public class RuRU : ILanguage
    {
        public string Code { get { return "ru-RU"; } }
        public string DisplayName { get { return "Русский (Россия)"; } }
        public bool RightToLeft { get { return false; } }

        public Dictionary<string, string> Strings
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "Menu", "Меню и параметры" },
                    { "LayoutStyle", "Стиль меню «Пуск»" },
                    { "Theme", "Светлая или тёмная тема" },
                    { "Dark", "Тёмная" },
                    { "Light", "Светлая" },
                    { "MetroBg", "Фон Windows 8" },
                    { "Skins", "Импортированные темы (правый щелчок — удалить)" },
                    { "ImportSkin", "+ Импорт темы JSON" },
                    { "ConfigSection", "Конфигурация" },
                    { "ImportConfig", "Импорт старой конфигурации" },
                    { "ExportConfig", "Экспорт текущей конфигурации" },
                    { "ResetConfig", "Сбросить всё" },
                    { "Language", "Язык" },

                    { "Search", "Поиск" },
                    { "SearchTip", "Поиск приложений, параметров и документов" },
                    { "SearchPrograms", "Поиск программ и файлов" },

                    { "Pinned", "Закреплено" },
                    { "PinnedToStart", "Закрепить на начальном экране" },
                    { "All", "Все" },
                    { "AllApps", "Все приложения" },
                    { "ShowAll", "Показать все" },
                    { "Collapse", "Свернуть" },
                    { "PinnedHeader", "Закреплено" },

                    { "Documents", "Документы" },
                    { "Pictures", "Изображения" },
                    { "Music", "Музыка" },
                    { "Computer", "Компьютер" },
                    { "Downloads", "Загрузки" },
                    { "ControlPanel", "Панель управления" },
                    { "Devices", "Устройства и принтеры" },
                    { "DefaultPrograms", "Программы по умолчанию" },
                    { "Help", "Справка и поддержка" },
                    { "Run", "Выполнить..." },

                    { "MyDocuments", "Мои документы" },
                    { "MyPictures", "Мои рисунки" },
                    { "MyMusic", "Моя музыка" },
                    { "MyComputer", "Мой компьютер" },
                    { "Printers", "Принтеры и факсы" },
                    { "SearchText", "Поиск" },

                    { "SignOut", "Выйти" },
                    { "ShutDown", "Завершение работы" },
                    { "PowerOff", "Выключить" },
                    { "Restart", "Перезагрузка" },
                    { "Sleep", "Сон" },
                    { "Lock", "Блокировка" },
                    { "Power", "Питание" },
                    { "Settings", "Параметры" },
                    { "UserAccount", "Текущий пользователь" },

                    { "PinTaskbar", "Закрепить на панели задач" },
                    { "OpenLocation", "Расположение файла" },
                    { "RunAsAdmin", "Запуск от имени администратора" },
                    { "Unpin", "Открепить от начального экрана" },
                    { "CustomColor", "Настраиваемый цвет" },
                    { "Small", "Маленький" },
                    { "Medium", "Средний" },
                    { "Wide", "Широкий" },
                    { "Large", "Большой" },
                    { "RenameGroup", "Переименовать группу" },
                    { "DeleteGroup", "Удалить группу (с плитками)" },
                    { "NewGroupHint", "+ Перетащите сюда, чтобы создать группу" },
                    { "AllPrograms", "Все программы" }
                };
            }
        }
    }
}
