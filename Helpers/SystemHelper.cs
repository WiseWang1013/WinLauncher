using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using WinLauncher.Models;

namespace WinLauncher.Helpers
{
    public static class SystemHelper
    {
        public static void Shutdown() => Run("shutdown", "/s /t 0");
        public static void Restart() => Run("shutdown", "/r /t 0");
        public static void Sleep() => Run("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
        public static void Lock() => Run("rundll32.exe", "user32.dll,LockWorkStation");
        public static void Logoff() => Run("shutdown", "/l");

        public static void OpenFolder(Environment.SpecialFolder folder)
        {
            try { Process.Start("explorer.exe", Environment.GetFolderPath(folder)); } catch { }
        }

        public static void OpenSettings() => Run("ms-settings:", "");

        private static void Run(string file, string args)
        {
            try { Process.Start(new ProcessStartInfo { FileName = file, Arguments = args, UseShellExecute = true }); }
            catch { }
        }

        public static string GetCurrentUserPicturePath()
        {
            string publicPictures = @"C:\Users\Public\AccountPictures";
            if (Directory.Exists(publicPictures))
            {
                var files = Directory.GetFiles(publicPictures, "*.jpg", SearchOption.AllDirectories)
                                     .Concat(Directory.GetFiles(publicPictures, "*.png", SearchOption.AllDirectories))
                                     .ToList();
                if (files.Count > 0) return files[0];
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localPictures = Path.Combine(userProfile, @"AppData\Roaming\Microsoft\Windows\AccountPictures");
            if (Directory.Exists(localPictures))
            {
                var files = Directory.GetFiles(localPictures, "*.jpg", SearchOption.AllDirectories)
                                     .Concat(Directory.GetFiles(localPictures, "*.png", SearchOption.AllDirectories))
                                     .ToList();
                if (files.Count > 0) return files[0];
            }
            return string.Empty;
        }

        public static List<AppItem> GetRecentFiles()
        {
            var list = new List<AppItem>();
            string[] folders = {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                try
                {
                    var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                                         .Where(f => f.EndsWith(".lnk") || f.EndsWith(".docx") || f.EndsWith(".xlsx") || f.EndsWith(".pdf") || f.EndsWith(".txt"))
                                         .OrderByDescending(f => File.GetLastWriteTime(f))
                                         .Take(4);

                    foreach (var f in files)
                    {
                        list.Add(new AppItem
                        {
                            Name = Path.GetFileNameWithoutExtension(f),
                            Path = f,
                            Icon = IconHelper.GetIconWithoutArrow(f)
                        });
                    }
                }
                catch { }
            }
            return list;
        }
    }
}