using System;
using System.Collections.Generic;
using MinorShift.Emuera.Runtime.Config;
using XEmuera.Resources;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidConfigPage : ContentPage
    {
        private ListView listView;
        private List<ConfigItem> items;

        public AndroidConfigPage()
        {
            Title = StringsText.Settings;

            items = BuildConfigItems();

            listView = new ListView(ListViewCachingStrategy.RecycleElement)
            {
                HasUnevenRows = true,
                ItemsSource = items,
                ItemTemplate = new DataTemplate(() =>
                {
                    var nameLabel = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold };
                    nameLabel.SetBinding(Label.TextProperty, "Name");

                    var valueLabel = new Label { FontSize = 12, TextColor = Color.Gray };
                    valueLabel.SetBinding(Label.TextProperty, "DisplayValue");

                    return new ViewCell
                    {
                        View = new StackLayout
                        {
                            Padding = new Thickness(15, 8),
                            Children = { nameLabel, valueLabel }
                        }
                    };
                }),
            };

            listView.ItemSelected += OnItemSelected;

            var saveButton = new Button
            {
                Text = StringsText.SaveAndClose,
                BackgroundColor = Color.FromHex("#2196F3"),
                TextColor = Color.White,
                Margin = new Thickness(15, 10),
            };
            saveButton.Clicked += OnSaveClicked;

            Content = new StackLayout
            {
                Children = { listView, saveButton }
            };
        }

        private List<ConfigItem> BuildConfigItems()
        {
            var result = new List<ConfigItem>();

            try
            {
                var configData = ConfigData.Instance;
                if (configData == null) return result;

                foreach (ConfigCode code in Enum.GetValues(typeof(ConfigCode)))
                {
                    try
                    {
                        var aItem = configData.GetItem(code);
                        if (aItem == null) continue;

                        var item = new ConfigItem { Code = code, Name = code.ToString() };

                        if (aItem is ConfigItem<bool> boolItem)
                        {
                            item.Value = boolItem.Value;
                            item.DisplayValue = boolItem.Value ? "True (tap to toggle)" : "False (tap to toggle)";
                            item.IsBool = true;
                        }
                        else if (aItem is ConfigItem<int> intItem)
                        {
                            item.Value = intItem.Value;
                            item.DisplayValue = intItem.Value.ToString();
                        }
                        else if (aItem is ConfigItem<float> floatItem)
                        {
                            item.Value = floatItem.Value;
                            item.DisplayValue = floatItem.Value.ToString("F1");
                            item.IsFloat = true;
                        }
                        else if (aItem is ConfigItem<string> strItem)
                        {
                            item.Value = strItem.Value ?? "";
                            item.DisplayValue = strItem.Value ?? "";
                        }
                        else
                        {
                            item.Value = aItem.ValueToString();
                            item.DisplayValue = aItem.ValueToString();
                        }

                        result.Add(item);
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }

        private void OnItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (!(e.SelectedItem is ConfigItem item)) return;
            listView.SelectedItem = null;

            var configData = ConfigData.Instance;
            if (configData == null) return;

            if (item.IsBool)
            {
                var newVal = !(bool)item.Value;
                var aItem = configData.GetItem(item.Code);
                aItem?.SetValue(newVal);
                item.Value = newVal;
                item.DisplayValue = newVal ? "True (tap to toggle)" : "False (tap to toggle)";
                listView.ItemsSource = null;
                listView.ItemsSource = items;
            }
            else if (item.IsFloat)
            {
                var entry = new Entry
                {
                    Text = item.Value is float fv ? fv.ToString("F1") : item.Value?.ToString() ?? "",
                    Keyboard = Keyboard.Numeric,
                };
                var hintLabel = new Label
                {
                    Text = item.Code == ConfigCode.FontScale ? "Range: 0.5 ~ 5.0, step 0.1" : "",
                    FontSize = 12,
                    TextColor = Color.Gray,
                };
                var page = new ContentPage
                {
                    Title = item.Name,
                    Content = new StackLayout
                    {
                        Padding = 15,
                        Children =
                        {
                            new Label { Text = item.Name, FontSize = 18 },
                            hintLabel,
                            entry,
                        }
                    }
                };
                entry.Completed += (s, args) =>
                {
                    var aItem = configData.GetItem(item.Code);
                    if (aItem != null && float.TryParse(entry.Text, out float floatVal))
                    {
                        if (item.Code == ConfigCode.FontScale && (floatVal < 0.5f || floatVal > 5.0f))
                        {
                            hintLabel.TextColor = Color.Red;
                            hintLabel.Text = "Must be between 0.5 and 5.0!";
                            return;
                        }
                        aItem.SetValue(floatVal);
                        item.Value = floatVal;
                        item.DisplayValue = floatVal.ToString("F1");
                    }
                    Navigation.PopAsync();
                };
                Navigation.PushAsync(page);
            }
            else
            {
                var entry = new Entry { Text = item.Value?.ToString() ?? "" };
                var page = new ContentPage
                {
                    Title = item.Name,
                    Content = new StackLayout
                    {
                        Padding = 15,
                        Children =
                        {
                            new Label { Text = item.Name, FontSize = 18 },
                            entry,
                        }
                    }
                };
                entry.Completed += (s, args) =>
                {
                    var aItem = configData.GetItem(item.Code);
                    if (aItem != null)
                    {
                        if (item.Value is int)
                        {
                            if (int.TryParse(entry.Text, out int intVal))
                                aItem.SetValue(intVal);
                        }
                        else
                        {
                            aItem.SetValue(entry.Text);
                        }
                    }
                    item.Value = entry.Text;
                    item.DisplayValue = entry.Text;
                    Navigation.PopAsync();
                };
                Navigation.PushAsync(page);
            }
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                ConfigData.Instance.SaveConfig();
            }
            catch { }

            Navigation.PopAsync();
        }

        private class ConfigItem
        {
            public ConfigCode Code { get; set; }
            public string Name { get; set; }
            public object Value { get; set; }
            public string DisplayValue { get; set; }
            public bool IsBool { get; set; }
            public bool IsFloat { get; set; }
        }
    }
}
