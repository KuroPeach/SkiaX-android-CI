using System;
using System.Collections.Generic;
using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Utils;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidLanguagePage : ContentPage
    {
        private readonly List<LanguageItem> languages = new List<LanguageItem>
        {
            new LanguageItem { DisplayName = "日本語", LangName = "日本語" },
            new LanguageItem { DisplayName = "简体中文", LangName = "简体中文" },
            new LanguageItem { DisplayName = "English", LangName = "English" },
        };

        public event Action LanguageChanged;

        public AndroidLanguagePage()
        {
            Title = "Language";

            var currentLang = MinorShift.Emuera.Config.EmueraLang ?? "简体中文";

            var listView = new ListView
            {
                ItemsSource = languages,
                ItemTemplate = new DataTemplate(() =>
                {
                    var cell = new ViewCell();
                    var stack = new StackLayout
                    {
                        Orientation = StackOrientation.Horizontal,
                        Margin = new Thickness(20, 10),
                    };

                    var label = new Label
                    {
                        FontSize = 16,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.StartAndExpand,
                    };
                    label.SetBinding(Label.TextProperty, "DisplayName");

                    var check = new Label
                    {
                        FontSize = 18,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.End,
                    };
                    check.SetBinding(Label.TextProperty, "CheckMark");

                    stack.Children.Add(label);
                    stack.Children.Add(check);
                    cell.View = stack;
                    return cell;
                }),
            };

            // 标记当前语言
            foreach (var lang in languages)
                lang.CheckMark = lang.LangName == currentLang ? "✓" : "";

            listView.ItemSelected += OnItemSelected;

            Content = new StackLayout
            {
                Children =
                {
                    new Label
                    {
                        Text = "Changing language requires restart to take full effect.",
                        FontSize = 12,
                        TextColor = Color.Gray,
                        Margin = new Thickness(20, 10, 20, 0),
                    },
                    listView,
                }
            };
        }

        private void OnItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (!(e.SelectedItem is LanguageItem item)) return;
            ((ListView)sender).SelectedItem = null;

            var newLang = item.LangName;
            var currentLang = MinorShift.Emuera.Config.EmueraLang;

            if (newLang == currentLang) return;

            // 更新配置：通过 ConfigData 修改并保存
            var configData = MinorShift.Emuera.ConfigData.Instance;
            if (configData != null)
            {
                var configItem = configData.GetConfigItem(ConfigCode.EmueraLang);
                if (configItem != null)
                    configItem.SetValue(newLang);
                configData.SaveConfig();
            }

            // 更新显示标记
            foreach (var lang in languages)
                lang.CheckMark = lang.LangName == newLang ? "✓" : "";

            // 通知需要重启
            LanguageChanged?.Invoke();
        }

        private class LanguageItem
        {
            public string DisplayName { get; set; }
            public string LangName { get; set; }
            public string CheckMark { get; set; }
        }
    }
}
