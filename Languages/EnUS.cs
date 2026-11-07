using System.Collections.Generic;

namespace WinLauncher.Languages
{
    /// <summary>English (United States) — wording follows Windows 11 en-US</summary>
    public class EnUS : ILanguage
    {
        public string Code { get { return "en-US"; } }
        public string DisplayName { get { return "English (United States)"; } }
        public bool RightToLeft { get { return false; } }

        public Dictionary<string, string> Strings
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "Menu", "Menu and settings" },
                    { "LayoutStyle", "Start menu style" },
                    { "Theme", "Light or dark theme" },
                    { "Dark", "Dark" },
                    { "Light", "Light" },
                    { "MetroBg", "Windows 8 background" },
                    { "Skins", "Imported skins (right-click to delete)" },
                    { "ImportSkin", "+ Import skin JSON" },
                    { "ConfigSection", "Configuration" },
                    { "ImportConfig", "Import old config" },
                    { "ExportConfig", "Export current config" },
                    { "ResetConfig", "Reset everything" },
                    { "Language", "Language" },

                    { "Search", "Search" },
                    { "SearchTip", "Search apps, settings, and documents" },
                    { "SearchPrograms", "Search programs and files" },

                    { "Pinned", "Pinned" },
                    { "PinnedToStart", "Pin to Start" },
                    { "All", "All" },
                    { "AllApps", "All apps" },
                    { "ShowAll", "Show all" },
                    { "Collapse", "Collapse" },
                    { "PinnedHeader", "Pinned" },

                    { "Documents", "Documents" },
                    { "Pictures", "Pictures" },
                    { "Music", "Music" },
                    { "Computer", "Computer" },
                    { "Downloads", "Downloads" },
                    { "ControlPanel", "Control Panel" },
                    { "Devices", "Devices and Printers" },
                    { "DefaultPrograms", "Default Programs" },
                    { "Help", "Help and Support" },
                    { "Run", "Run..." },

                    { "MyDocuments", "My Documents" },
                    { "MyPictures", "My Pictures" },
                    { "MyMusic", "My Music" },
                    { "MyComputer", "My Computer" },
                    { "Printers", "Printers and Faxes" },
                    { "SearchText", "Search" },

                    { "SignOut", "Sign out" },
                    { "ShutDown", "Shut down" },
                    { "PowerOff", "Shut down" },
                    { "Restart", "Restart" },
                    { "Sleep", "Sleep" },
                    { "Lock", "Lock" },
                    { "Power", "Power" },
                    { "Settings", "Settings" },
                    { "UserAccount", "Current user" },

                    { "PinTaskbar", "Pin to taskbar" },
                    { "OpenLocation", "Open file location" },
                    { "RunAsAdmin", "Run as administrator" },
                    { "Unpin", "Unpin from Start" },
                    { "CustomColor", "Custom color" },
                    { "Small", "Small" },
                    { "Medium", "Medium" },
                    { "Wide", "Wide" },
                    { "Large", "Large" },
                    { "RenameGroup", "Rename group" },
                    { "DeleteGroup", "Delete group (with tiles)" },
                    { "NewGroupHint", "+ Drag here to create a group" },
                    { "AllPrograms", "All Programs" }
                };
            }
        }
    }
}
