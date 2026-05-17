using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace XEmuera.Droid
{
    internal class AndroidGameItemModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly ObservableCollection<AndroidGameItemModel> AllModels = new ObservableCollection<AndroidGameItemModel>();

        public string Name { get; private set; }
        public string Path { get; private set; }
        public bool HasError { get; private set; }
        public string Error { get; private set; }

        public static void LoadAll()
        {
            AllModels.Clear();

            string externalPath;
            try
            {
                externalPath = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0";
            }
            catch
            {
                externalPath = "/storage/emulated/0";
            }

            var scanPaths = new[]
            {
                externalPath,
                System.IO.Path.Combine(externalPath, "emuera"),
                System.IO.Path.Combine(externalPath, "Download"),
                System.IO.Path.Combine(externalPath, "Documents"),
            };

            var seen = new HashSet<string>();

            foreach (var scanPath in scanPaths)
            {
                try
                {
                    if (!Directory.Exists(scanPath))
                        continue;

                    if (IsValidGameDirectory(scanPath) && !seen.Contains(scanPath))
                    {
                        seen.Add(scanPath);
                        AllModels.Add(new AndroidGameItemModel
                        {
                            Name = System.IO.Path.GetFileName(scanPath) ?? scanPath,
                            Path = scanPath + System.IO.Path.DirectorySeparatorChar,
                        });
                    }

                    foreach (var dir in Directory.GetDirectories(scanPath))
                    {
                        try
                        {
                            if (IsValidGameDirectory(dir) && !seen.Contains(dir))
                            {
                                seen.Add(dir);
                                var model = new AndroidGameItemModel
                                {
                                    Name = System.IO.Path.GetFileName(dir),
                                    Path = dir + System.IO.Path.DirectorySeparatorChar,
                                };

                                if (!Directory.Exists(System.IO.Path.Combine(dir, "CSV"))
                                    && !Directory.Exists(System.IO.Path.Combine(dir, "csv")))
                                {
                                    model.HasError = true;
                                    model.Error = "(No CSV)";
                                }

                                AllModels.Add(model);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        public static void Load(string basePath)
        {
            AllModels.Clear();

            if (!Directory.Exists(basePath))
                return;

            var gameItemPaths = Directory.GetDirectories(basePath);
            if (gameItemPaths.Length == 0)
                return;

            char sep = System.IO.Path.DirectorySeparatorChar;

            foreach (var itemPath in gameItemPaths)
            {
                if (!Directory.Exists(itemPath + sep + "ERB")
                    && !Directory.Exists(itemPath + sep + "erb"))
                    continue;

                var model = new AndroidGameItemModel
                {
                    Name = System.IO.Path.GetFileName(itemPath),
                    Path = itemPath + sep,
                };

                if (!Directory.Exists(itemPath + sep + "CSV")
                    && !Directory.Exists(itemPath + sep + "csv"))
                {
                    model.HasError = true;
                    model.Error = "(No CSV)";
                }

                AllModels.Add(model);
            }
        }

        private static bool IsValidGameDirectory(string path)
        {
            try
            {
                var hasErb = Directory.Exists(System.IO.Path.Combine(path, "erb"))
                             || Directory.Exists(System.IO.Path.Combine(path, "ERB"));
                return hasErb;
            }
            catch { return false; }
        }
    }
}
