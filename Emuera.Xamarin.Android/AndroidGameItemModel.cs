using System;
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
    }
}
