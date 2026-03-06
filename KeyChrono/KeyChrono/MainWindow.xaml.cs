using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace KeyChrono
{
    public partial class MainWindow : Window
    {
        // ==========================================
        // 低階鍵盤鉤子 (Low Level Keyboard Hook)
        // ==========================================
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100; 
        private const int WM_KEYUP = 0x0101;

        private static IntPtr hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc? hookProc;
        // ==========================================
        // 目前修飾鍵狀態
        // ==========================================
        private bool isCtrlDown = false;
        private bool isAltDown = false;
        private bool isShiftDown = false;
        private bool isWinDown = false;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // ==========================================
        // 資料模型
        // ==========================================
        public class TimerConfig
        {
            public string Name { get; set; } = string.Empty;
            public string HotkeyStr { get; set; } = string.Empty;
            public int Duration { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int FontSize { get; set; } = 72;
            public bool AutoRestart { get; set; }
            public string ImagePath { get; set; } = string.Empty;

            [JsonIgnore] public string Coordinates => $"{X}, {Y}";
            [JsonIgnore] public string Dimensions => $"{Width}x{Height}";
        }

        private readonly ObservableCollection<TimerConfig> configs = new();
        private readonly Dictionary<string, TimerWindow> activeTimers = new();
        private readonly string configFilePath;

        public MainWindow() {
            InitializeComponent();

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "KeyChrono");
            Directory.CreateDirectory(appFolder);
            configFilePath = Path.Combine(appFolder, "timers.json");

            TimerList.ItemsSource = configs;
            LoadConfigs();
        }

        // ==========================================
        // 鉤子設置與移除
        // ==========================================
        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);

            hookProc = LowLevelKeyboardHookCallback;
            hookId = SetHook(hookProc);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc) {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            using var module = process.MainModule!;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
        }

        private IntPtr LowLevelKeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode < 0)
                return CallNextHookEx(hookId, nCode, wParam, lParam);

            int vkCode = Marshal.ReadInt32(lParam);
            Key key = KeyInterop.KeyFromVirtualKey(vkCode);

            bool keyDown = wParam == (IntPtr)WM_KEYDOWN;
            bool keyUp = wParam == (IntPtr)WM_KEYUP;

            // 更新修飾鍵狀態
            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    isCtrlDown = keyDown;
                    break;
                case Key.LeftAlt:
                case Key.RightAlt:
                    isAltDown = keyDown;
                    break;
                case Key.LeftShift:
                case Key.RightShift:
                    isShiftDown = keyDown;
                    break;
                case Key.LWin:
                case Key.RWin:
                    isWinDown = keyDown;
                    break;
            }

            // 只在按下主鍵（KeyDown）時才判斷是否觸發計時器
            if (keyDown && !IsModifierKey(key))
            {
                Application.Current?.Dispatcher.Invoke(() => HandleKeyPress(key));
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }
        private static bool IsModifierKey(Key key) {
            return key is Key.LeftCtrl or Key.RightCtrl or
                          Key.LeftAlt or Key.RightAlt or
                          Key.LeftShift or Key.RightShift or
                          Key.LWin or Key.RWin;
        }


        // ==========================================
        // 按鍵處理核心邏輯
        // ==========================================
        private void HandleKeyPress(Key key) {
            string hotkeyString = GenerateHotkeyString(key);
            // 特殊處理：按 ESC 鍵（不論是否有其他修飾鍵） → 關閉全部
            if (hotkeyString == "shift+escape")
            {
                CloseAllTimers();
                return;
            }

            // 根據目前按下的修飾鍵 + 主鍵，產生快捷鍵字串

            // 找出所有 HotkeyStr 符合的計時器設定（不分大小寫）
            var matchedConfigs = configs
                .Where(c => string.Equals(c.HotkeyStr, hotkeyString, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var config in matchedConfigs)
            {
                TriggerTimer(config);
            }
        }
        /// <summary>
        /// 根據目前修飾鍵狀態與主鍵，產生標準格式的快捷鍵字串
        /// 格式範例： "ctrl+alt+k"、"shift+f5"、"win+space"、"f12"
        /// 順序固定：ctrl → alt → shift → win → 主鍵（全小寫）
        /// </summary>
        private string GenerateHotkeyString(Key mainKey) {
            var segments = new List<string>();

            if (isCtrlDown) segments.Add("ctrl");
            if (isAltDown) segments.Add("alt");
            if (isShiftDown) segments.Add("shift");
            if (isWinDown) segments.Add("win");

            string keyName = mainKey.ToString().ToLowerInvariant();

            // 常見按鍵名稱優化（可依需求增加）
            keyName = keyName switch
            {
                "return" => "enter",
                "capital" => "capslock",
                "oemcomma" => ",",
                "oemperiod" => ".",
                "oemminus" => "-",
                "oemplus" => "+",
                "oem6" => "]",
                "oem4" => "[",
                "oem5" => "\\",
                "oem1" => ";",
                "oem7" => "'",
                "oem102" => "<",
                _ => keyName
            };

            segments.Add(keyName);

            return string.Join("+", segments);
        }
        protected override void OnClosed(EventArgs e) {
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }

            // 關閉所有計時器視窗
            foreach (var timer in activeTimers.Values.ToList())
            {
                timer.Close();
            }
            activeTimers.Clear();

            base.OnClosed(e);
        }

        private void TriggerTimer(TimerConfig config) {
            string name = config.Name;

            if (activeTimers.TryGetValue(name, out var existingTimer))
            {
                existingTimer.ToggleTimer();
            } else
            {
                var timerWindow = new TimerWindow(
                    name,
                    config.Duration,
                    config.ImagePath,
                    config.X, config.Y,
                    config.Width, config.Height,
                    config.FontSize,
                    config.AutoRestart);

                timerWindow.OnLocationChanged = (timerName, newX, newY) =>
                {
                    var cfg = configs.FirstOrDefault(c => c.Name.Equals(timerName, StringComparison.OrdinalIgnoreCase));
                    if (cfg != null)
                    {
                        cfg.X = newX;
                        cfg.Y = newY;
                        SaveConfigs();
                        RefreshUIAfterConfigChange();
                    }
                };

                timerWindow.Closed += (_, _) => activeTimers.Remove(name);

                timerWindow.Show();
                activeTimers[name] = timerWindow;
            }
        }

        private void CloseAllTimers() {
            foreach (var timer in activeTimers.Values.ToList())
            {
                timer.Close();
            }
            activeTimers.Clear();
        }

        private void RefreshUIAfterConfigChange() {
            var selected = TimerList.SelectedItem;
            TimerList.Items.Refresh();
            TimerList.SelectedItem = selected;

            if (TimerList.SelectedItem is TimerConfig cfg)
            {
                XInput.Text = cfg.X.ToString();
                YInput.Text = cfg.Y.ToString();
            }
        }

        // ==========================================
        // 設定檔存取
        // ==========================================
        private void SaveConfigs() {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(configs, options);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadConfigs() {
            if (!File.Exists(configFilePath)) return;

            try
            {
                string json = File.ReadAllText(configFilePath);
                var loaded = JsonSerializer.Deserialize<List<TimerConfig>>(json);

                if (loaded != null)
                {
                    foreach (var c in loaded)
                    {
                        if (c.FontSize <= 0) c.FontSize = 72;
                        configs.Add(c);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入設定檔失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // UI 事件處理
        // ==========================================
        private void BrowseImage_Click(object sender, RoutedEventArgs e) {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dlg.ShowDialog() == true)
            {
                ImageInput.Text = dlg.FileName;
            }
        }

        private void AddTimer_Click(object sender, RoutedEventArgs e) {
            if (!int.TryParse(TimeInput.Text, out int duration) ||
                !int.TryParse(XInput.Text, out int x) ||
                !int.TryParse(YInput.Text, out int y) ||
                !int.TryParse(WidthInput.Text, out int w) ||
                !int.TryParse(HeightInput.Text, out int h) ||
                !int.TryParse(FontSizeInput.Text, out int fontSize))
            {
                MessageBox.Show("請檢查數字欄位格式", "輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string name = NameInput.Text.Trim();
            string hotkey = HotkeyInput.Text.Trim();
            string imgPath = ImageInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(hotkey) || string.IsNullOrWhiteSpace(imgPath))
            {
                MessageBox.Show("名稱、快捷鍵、圖片路徑 請務必填寫", "欄位不完整", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 移除舊的同名項目（如果存在）
            var existing = configs.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                configs.Remove(existing);
                activeTimers.Remove(name); // 也關閉正在執行的視窗（可選）
            }

            var newConfig = new TimerConfig
            {
                Name = name,
                HotkeyStr = hotkey,
                Duration = duration,
                X = x,
                Y = y,
                Width = w,
                Height = h,
                FontSize = fontSize,
                AutoRestart = AutoRestartCheck.IsChecked == true,
                ImagePath = imgPath
            };

            configs.Add(newConfig);
            SaveConfigs();

            MessageBox.Show($"已儲存計時器：{name}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteTimer_Click(object sender, RoutedEventArgs e) {
            if (TimerList.SelectedItem is not TimerConfig selected) return;

            if (activeTimers.TryGetValue(selected.Name, out var tw))
            {
                tw.Close();
                activeTimers.Remove(selected.Name);
            }

            configs.Remove(selected);
            SaveConfigs();
        }

        private void TimerList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (TimerList.SelectedItem is TimerConfig cfg)
            {
                NameInput.Text = cfg.Name;
                TimeInput.Text = cfg.Duration.ToString();
                HotkeyInput.Text = cfg.HotkeyStr;
                ImageInput.Text = cfg.ImagePath;
                XInput.Text = cfg.X.ToString();
                YInput.Text = cfg.Y.ToString();
                WidthInput.Text = cfg.Width.ToString();
                HeightInput.Text = cfg.Height.ToString();
                FontSizeInput.Text = cfg.FontSize.ToString();
                AutoRestartCheck.IsChecked = cfg.AutoRestart;
            }
        }
    }
}