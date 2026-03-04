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

namespace CountdownApp
{
    public partial class MainWindow : Window
    {
        public class TimerConfig
        {
            public string HotkeyStr { get; set; }
            public int Duration { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool AutoRestart { get; set; }
            public string ImagePath { get; set; }

            // 加上 JsonIgnore，避免儲存時把這個只讀用的屬性也存入 JSON
            [JsonIgnore]
            public string Coordinates => $"{X}, {Y}";
            [JsonIgnore]
            public string Dimensions => $"{Width}x{Height}";
        }

        private ObservableCollection<TimerConfig> configs = new ObservableCollection<TimerConfig>();
        private Dictionary<string, TimerWindow> activeTimers = new Dictionary<string, TimerWindow>();
        private readonly string configFilePath;

        public MainWindow()
        {
            InitializeComponent();

            // 設定 AppData 儲存路徑 (C:\Users\使用者\AppData\Roaming\CountdownApp\timers.json)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "CountdownApp");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            configFilePath = Path.Combine(appFolder, "timers.json");

            TimerList.ItemsSource = configs;

            // 程式啟動時載入設定
            LoadConfigs();
        }

        // --- 功能 1: 存檔與讀取 ---
        private void SaveConfigs()
        {
            try
            {
                string json = JsonSerializer.Serialize(configs);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("儲存設定檔失敗: " + ex.Message);
            }
        }

        private void LoadConfigs()
        {
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
                            configs.Add(c);
                            // 重新註冊快捷鍵
                            KeyGesture gesture = (KeyGesture)converter.ConvertFromString(c.HotkeyStr);
                            HotkeyManager.Current.AddOrReplace(c.HotkeyStr, gesture.Key, gesture.Modifiers, OnHotkeyTriggered);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("載入設定檔失敗: " + ex.Message);
                }
            }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
            if (dlg.ShowDialog() == true)
            {
                ImageInput.Text = dlg.FileName;
            }
        }

        private void AddTimer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int time = int.Parse(TimeInput.Text);
                int x = int.Parse(XInput.Text);
                int y = int.Parse(YInput.Text);
                int width = int.Parse(WidthInput.Text);
                int height = int.Parse(HeightInput.Text);
                bool autoRestart = AutoRestartCheck.IsChecked ?? false;
                string imgPath = ImageInput.Text;
                string hkStr = HotkeyInput.Text.Trim();

                if (string.IsNullOrEmpty(imgPath) || string.IsNullOrEmpty(hkStr))
                {
                    MessageBox.Show("請完整輸入圖片路徑與快捷鍵！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                KeyGestureConverter converter = new KeyGestureConverter();
                KeyGesture gesture = (KeyGesture)converter.ConvertFromString(hkStr);

                // 如果設定中已經有同一個快捷鍵，先將舊的移除 (當作是更新設定)
                var existingConfig = configs.FirstOrDefault(c => c.HotkeyStr.Equals(hkStr, StringComparison.OrdinalIgnoreCase));
                if (existingConfig != null)
                {
                    configs.Remove(existingConfig);
                }

                // 註冊 / 覆蓋全域快捷鍵
                HotkeyManager.Current.AddOrReplace(hkStr, gesture.Key, gesture.Modifiers, OnHotkeyTriggered);

                configs.Add(new TimerConfig
                {
                    HotkeyStr = hkStr,
                    Duration = time,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    AutoRestart = autoRestart,
                    ImagePath = imgPath
                });

                // 觸發存檔
                SaveConfigs();
                MessageBox.Show($"成功儲存快捷鍵設定: {hkStr}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"新增/更新失敗，請檢查輸入格式。\n錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- 功能 2: 刪除計時器 ---
        private void DeleteTimer_Click(object sender, RoutedEventArgs e)
        {
            if (TimerList.SelectedItem is TimerConfig selectedConfig)
            {
                // 如果該計時器正在執行，先強制關閉它
                if (activeTimers.ContainsKey(selectedConfig.HotkeyStr))
                {
                    activeTimers[selectedConfig.HotkeyStr].Close();
                    activeTimers.Remove(selectedConfig.HotkeyStr);
                }

                // 解除註冊快捷鍵
                HotkeyManager.Current.Remove(selectedConfig.HotkeyStr);

                // 從清單中移除並存檔
                configs.Remove(selectedConfig);
                SaveConfigs();
            }
            else
            {
                MessageBox.Show("請先在下方清單選擇要刪除的計時器！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- 功能 3: 點擊清單項目自動帶入上方輸入框 ---
        private void TimerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimerList.SelectedItem is TimerConfig selectedConfig)
            {
                TimeInput.Text = selectedConfig.Duration.ToString();
                ImageInput.Text = selectedConfig.ImagePath;
                XInput.Text = selectedConfig.X.ToString();
                YInput.Text = selectedConfig.Y.ToString();
                WidthInput.Text = selectedConfig.Width.ToString();
                HeightInput.Text = selectedConfig.Height.ToString();
                AutoRestartCheck.IsChecked = selectedConfig.AutoRestart;
                HotkeyInput.Text = selectedConfig.HotkeyStr;
            }
        }

        private void OnHotkeyTriggered(object sender, HotkeyEventArgs e)
        {
            string hkStr = e.Name;

            if (activeTimers.ContainsKey(hkStr))
            {
                activeTimers[hkStr].Close();
                activeTimers.Remove(hkStr);
            }
            else
            {
                TimerConfig config = configs.FirstOrDefault(c => c.HotkeyStr.Equals(hkStr, StringComparison.OrdinalIgnoreCase));

                if (config != null)
                {
                    TimerWindow tw = new TimerWindow(hkStr, config.Duration, config.ImagePath, config.X, config.Y, config.Width, config.Height, config.AutoRestart);

                    tw.Closed += (s, args) =>
                    {
                        if (activeTimers.ContainsKey(hkStr)) activeTimers.Remove(hkStr);
                    };

                    tw.Show();
                    activeTimers[hkStr] = tw;
                }
            }
            e.Handled = true;
        }
    }
}