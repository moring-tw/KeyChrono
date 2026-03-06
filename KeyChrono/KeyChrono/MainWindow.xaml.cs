using Microsoft.Win32;
using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KeyChrono
{
    public partial class MainWindow : Window
    {
        public class TimerConfig
        {
            public string Name { get; set; }
            public string HotkeyStr { get; set; }
            public int Duration { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int FontSize { get; set; } // 新增: 字體大小
            public bool AutoRestart { get; set; }
            public string ImagePath { get; set; }

            [JsonIgnore]
            public string Coordinates => $"{X}, {Y}";
            [JsonIgnore]
            public string Dimensions => $"{Width}x{Height}";
        }

        private ObservableCollection<TimerConfig> configs = new ObservableCollection<TimerConfig>();
        private Dictionary<string, TimerWindow> activeTimers = new Dictionary<string, TimerWindow>();
        private readonly string configFilePath;

        public MainWindow() {
            InitializeComponent();

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "KeyChrono");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            configFilePath = Path.Combine(appFolder, "timers.json");

            TimerList.ItemsSource = configs;
            LoadConfigs();

            // 【新增】：註冊全域 ESC 快捷鍵，用來一鍵關閉所有計時器
            try
            {
                HotkeyManager.Current.AddOrReplace("CloseAllTimers", Key.Escape, ModifierKeys.Shift, OnCloseAllTimers);
            }
            catch (Exception ex)
            {
                MessageBox.Show("全域 ESC 快捷鍵註冊失敗，可能與系統衝突: " + ex.Message);
            }
        }

        // 【新增】：一鍵關閉所有計時器的邏輯
        private void OnCloseAllTimers(object sender, HotkeyEventArgs e) {
            // 將字典的 Key 複製成 List，避免在 foreach 迴圈中修改集合導致錯誤
            var keys = activeTimers.Keys.ToList();
            foreach (var key in keys)
            {
                activeTimers[key].Close();
            }
            activeTimers.Clear();
            e.Handled = true;
        }

        private void SaveConfigs() {
            try
            {
                string json = JsonSerializer.Serialize(configs);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex) { MessageBox.Show("儲存設定檔失敗: " + ex.Message); }
        }

        private void LoadConfigs() {
            if (File.Exists(configFilePath))
            {
                try
                {
                    string json = File.ReadAllText(configFilePath);
                    var loadedConfigs = JsonSerializer.Deserialize<List<TimerConfig>>(json);

                    if (loadedConfigs != null)
                    {
                        KeyGestureConverter converter = new KeyGestureConverter();
                        foreach (var c in loadedConfigs)
                        {
                            // 舊版本設定檔相容處理：如果讀取不到字體大小，預設給 72
                            if (c.FontSize == 0) c.FontSize = 72;
                            configs.Add(c);
                        }

                        var uniqueHotkeys = configs.Select(c => c.HotkeyStr).Distinct();
                        foreach (var hk in uniqueHotkeys)
                        {
                            KeyGesture gesture = (KeyGesture)converter.ConvertFromString(hk);
                            HotkeyManager.Current.AddOrReplace(hk, gesture.Key, gesture.Modifiers, OnHotkeyTriggered);
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("載入設定檔失敗: " + ex.Message); }
            }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
            if (dlg.ShowDialog() == true) ImageInput.Text = dlg.FileName;
        }

        private void AddTimer_Click(object sender, RoutedEventArgs e) {
            try
            {
                string name = NameInput.Text.Trim();
                int time = int.Parse(TimeInput.Text);
                int x = int.Parse(XInput.Text);
                int y = int.Parse(YInput.Text);
                int width = int.Parse(WidthInput.Text);
                int height = int.Parse(HeightInput.Text);
                int fontSize = int.Parse(FontSizeInput.Text); // 讀取字體大小
                bool autoRestart = AutoRestartCheck.IsChecked ?? false;
                string imgPath = ImageInput.Text;
                string hkStr = HotkeyInput.Text.Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(imgPath) || string.IsNullOrEmpty(hkStr))
                {
                    MessageBox.Show("請完整輸入名稱、圖片路徑與快捷鍵！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                KeyGestureConverter converter = new KeyGestureConverter();
                KeyGesture gesture = (KeyGesture)converter.ConvertFromString(hkStr);

                var existingConfig = configs.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existingConfig != null)
                {
                    string oldHk = existingConfig.HotkeyStr;
                    configs.Remove(existingConfig);

                    if (!configs.Any(c => c.HotkeyStr.Equals(oldHk, StringComparison.OrdinalIgnoreCase)) && oldHk != hkStr)
                    {
                        HotkeyManager.Current.Remove(oldHk);
                    }
                }

                HotkeyManager.Current.AddOrReplace(hkStr, gesture.Key, gesture.Modifiers, OnHotkeyTriggered);

                configs.Add(new TimerConfig
                {
                    Name = name,
                    HotkeyStr = hkStr,
                    Duration = time,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    FontSize = fontSize, // 儲存字體大小
                    AutoRestart = autoRestart,
                    ImagePath = imgPath
                });

                SaveConfigs();
                MessageBox.Show($"成功儲存計時器: {name}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"新增/更新失敗，請檢查輸入格式。\n錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTimer_Click(object sender, RoutedEventArgs e) {
            if (TimerList.SelectedItem is TimerConfig selectedConfig)
            {
                if (activeTimers.ContainsKey(selectedConfig.Name))
                {
                    activeTimers[selectedConfig.Name].Close();
                    activeTimers.Remove(selectedConfig.Name);
                }

                string hkStr = selectedConfig.HotkeyStr;
                configs.Remove(selectedConfig);

                if (!configs.Any(c => c.HotkeyStr.Equals(hkStr, StringComparison.OrdinalIgnoreCase)))
                {
                    HotkeyManager.Current.Remove(hkStr);
                }

                SaveConfigs();
            } else
            {
                MessageBox.Show("請先在下方清單選擇要刪除的計時器！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TimerList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (TimerList.SelectedItem is TimerConfig selectedConfig)
            {
                NameInput.Text = selectedConfig.Name;
                TimeInput.Text = selectedConfig.Duration.ToString();
                ImageInput.Text = selectedConfig.ImagePath;
                XInput.Text = selectedConfig.X.ToString();
                YInput.Text = selectedConfig.Y.ToString();
                WidthInput.Text = selectedConfig.Width.ToString();
                HeightInput.Text = selectedConfig.Height.ToString();
                FontSizeInput.Text = selectedConfig.FontSize.ToString(); // 自動帶入字體大小
                AutoRestartCheck.IsChecked = selectedConfig.AutoRestart;
                HotkeyInput.Text = selectedConfig.HotkeyStr;
            }
        }

        private void OnHotkeyTriggered(object sender, HotkeyEventArgs e) {
            string hkStr = e.Name;

            var matchingConfigs = configs.Where(c => c.HotkeyStr.Equals(hkStr, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var config in matchingConfigs)
            {
                if (activeTimers.ContainsKey(config.Name))
                {
                    activeTimers[config.Name].ToggleTimer();
                } else
                {
                    // 傳遞 FontSize 參數
                    TimerWindow tw = new TimerWindow(config.Name, config.Duration, config.ImagePath, config.X, config.Y, config.Width, config.Height, config.FontSize, config.AutoRestart);

                    tw.OnLocationChanged = (nameId, newX, newY) =>
                    {
                        var c = configs.FirstOrDefault(x => x.Name.Equals(nameId, StringComparison.OrdinalIgnoreCase));
                        if (c != null)
                        {
                            c.X = newX;
                            c.Y = newY;
                            SaveConfigs();

                            var selected = TimerList.SelectedItem;
                            TimerList.Items.Refresh();
                            TimerList.SelectedItem = selected;

                            if (TimerList.SelectedItem == c)
                            {
                                XInput.Text = newX.ToString();
                                YInput.Text = newY.ToString();
                            }
                        }
                    };

                    tw.Closed += (s, args) =>
                    {
                        if (activeTimers.ContainsKey(config.Name)) activeTimers.Remove(config.Name);
                    };

                    tw.Show();
                    activeTimers[config.Name] = tw;
                }
            }
            e.Handled = true;
        }
    }
}