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
using System.Windows.Media;

namespace KeyChrono
{
    public partial class MainWindow : Window
    {
        // ==========================================
        // 低階鍵盤鉤子
        // ==========================================
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private static IntPtr hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc? hookProc;

        private bool isCtrlDown = false;
        private bool isAltDown = false;
        private bool isShiftDown = false;
        private bool isWinDown = false;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam); [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId); [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk); [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam); [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // ==========================================
        // 資料模型與全域狀態
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
            public int RetriggerAction { get; set; } = 0;
            public string ImagePath { get; set; } = string.Empty;
            public int BlinkTime { get; set; } = 10;
            public string AudioPath { get; set; } = string.Empty;
            public int AudioTime { get; set; } = 10;

            [JsonIgnore] public string Coordinates => $"{X}, {Y}";
            [JsonIgnore] public string Dimensions => $"{Width}x{Height}";
            [JsonIgnore] public string RetriggerActionText => RetriggerAction switch { 1 => "重置", 2 => "暫停", _ => "停止" };
        }

        public class GlobalSettings
        {
            public bool IsStatusWindowTopMost { get; set; } = true;
        }

        private readonly ObservableCollection<TimerConfig> configs = new();
        private readonly Dictionary<string, TimerWindow> activeTimers = new();
        private readonly string configFilePath;
        private readonly string globalSettingsPath;

        private StatusWindow statusWindow;
        private GlobalSettings appSettings = new();
        private bool isHotkeyEnabled = true;

        public MainWindow() {
            InitializeComponent();
            SetDefaultCenterCoordinates();

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "KeyChrono");
            Directory.CreateDirectory(appFolder);
            configFilePath = Path.Combine(appFolder, "timers.json");
            globalSettingsPath = Path.Combine(appFolder, "settings.json");

            TimerList.ItemsSource = configs;
            LoadConfigs();

            // 初始化狀態懸浮窗
            statusWindow = new StatusWindow();
            statusWindow.Topmost = appSettings.IsStatusWindowTopMost;
            StatusWindowTopMostCheck.IsChecked = appSettings.IsStatusWindowTopMost;

            statusWindow.OnStatusChanged = (isEnabled) => {
                isHotkeyEnabled = isEnabled;
            };
            statusWindow.Show();
        }

        private void SetDefaultCenterCoordinates() {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            XInput.Text = ((int)((screenWidth - 200) / 2)).ToString();
            YInput.Text = ((int)((screenHeight - 200) / 2)).ToString();
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
            if (nCode < 0) return CallNextHookEx(hookId, nCode, wParam, lParam);

            int vkCode = Marshal.ReadInt32(lParam);
            Key key = KeyInterop.KeyFromVirtualKey(vkCode);
            bool keyDown = wParam == (IntPtr)WM_KEYDOWN;

            switch (key)
            {
                case Key.LeftCtrl: case Key.RightCtrl: isCtrlDown = keyDown; break;
                case Key.LeftAlt: case Key.RightAlt: isAltDown = keyDown; break;
                case Key.LeftShift: case Key.RightShift: isShiftDown = keyDown; break;
                case Key.LWin: case Key.RWin: isWinDown = keyDown; break;
            }

            if (keyDown && !IsModifierKey(key))
            {
                Application.Current?.Dispatcher.Invoke(() => HandleKeyPress(key));
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private static bool IsModifierKey(Key key) {
            return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
        }

        private void HandleKeyPress(Key key) {
            string hotkeyString = GenerateHotkeyString(key);

            // 系統全域熱鍵 (不受 Toggle 影響)
            if (hotkeyString == "shift+f1")
            {
                isHotkeyEnabled = !isHotkeyEnabled;
                statusWindow.SetToggleState(isHotkeyEnabled);
                return;
            }

            if (hotkeyString == "shift+escape")
            {
                CloseAllTimers();
                return;
            }

            // 【重點】：如果關閉監聽，則略過後續觸發
            if (!isHotkeyEnabled) return;

            var matchedConfigs = configs.Where(c => string.Equals(c.HotkeyStr, hotkeyString, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var config in matchedConfigs) TriggerTimer(config);
        }

        private string GenerateHotkeyString(Key mainKey) {
            var segments = new List<string>();
            if (isCtrlDown) segments.Add("ctrl");
            if (isAltDown) segments.Add("alt");
            if (isShiftDown) segments.Add("shift");
            if (isWinDown) segments.Add("win");

            string keyName = mainKey.ToString().ToLowerInvariant();

            // 修正數字鍵 0-9 顯示為 D0-D9 的問題
            if (mainKey >= Key.D0 && mainKey <= Key.D9)
            {
                keyName = keyName.Substring(1);
            }

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

        public static (Key Key, ModifierKeys Modifiers) ParseHotkey(string str) {
            var parts = str.Split('+');
            ModifierKeys modifiers = 0;
            Key key = Key.None;

            foreach (var p in parts)
            {
                string pLower = p.Trim().ToLower();
                switch (pLower)
                {
                    case "ctrl": case "control": modifiers |= ModifierKeys.Control; break;
                    case "alt": modifiers |= ModifierKeys.Alt; break;
                    case "shift": modifiers |= ModifierKeys.Shift; break;
                    case "win": case "windows": modifiers |= ModifierKeys.Windows; break;
                    default:
                        string keyStr = p.Trim();
                        // 修正反向解析數字鍵 0-9
                        if (keyStr.Length == 1 && char.IsDigit(keyStr[0])) keyStr = "D" + keyStr;

                        if (Enum.TryParse(typeof(Key), keyStr, true, out object parsedKey))
                            key = (Key)parsedKey;
                        break;
                }
            }
            return (key, modifiers);
        }

        protected override void OnClosed(EventArgs e) {
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
            statusWindow?.Close();
            CloseAllTimers();
            base.OnClosed(e);
        }

        private void TriggerTimer(TimerConfig config) {
            string name = config.Name;
            if (activeTimers.TryGetValue(name, out var existingTimer))
            {
                if (existingTimer.IsRunning)
                {
                    if (config.RetriggerAction == 1) existingTimer.ResetAndStart();
                    else if (config.RetriggerAction == 2) existingTimer.Pause();
                    else existingTimer.StopAndReset();
                } else
                {
                    if (existingTimer.TimeLeft > 0 && existingTimer.TimeLeft < existingTimer.InitialDuration)
                        existingTimer.Resume();
                    else existingTimer.ResetAndStart();
                }
            } else
            {
                var timerWindow = new TimerWindow(
                    name, config.Duration, config.ImagePath,
                    config.X, config.Y, config.Width, config.Height, config.FontSize,
                    config.AutoRestart, config.BlinkTime, config.AudioPath, config.AudioTime);

                timerWindow.OnLocationChanged = (timerName, newX, newY) => {
                    var cfg = configs.FirstOrDefault(c => c.Name.Equals(timerName, StringComparison.OrdinalIgnoreCase));
                    if (cfg != null) { cfg.X = newX; cfg.Y = newY; SaveConfigs(); RefreshUIAfterConfigChange(); }
                };
                timerWindow.Closed += (_, _) => activeTimers.Remove(name);
                timerWindow.Show();
                activeTimers[name] = timerWindow;
            }
        }

        private void CloseAllTimers() {
            foreach (var timer in activeTimers.Values.ToList()) timer.Close();
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
        // 存取設定檔
        // ==========================================
        private void SaveConfigs() {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(configFilePath, JsonSerializer.Serialize(configs, options));
                File.WriteAllText(globalSettingsPath, JsonSerializer.Serialize(appSettings, options));
            }
            catch { }
        }

        private void LoadConfigs() {
            try
            {
                if (File.Exists(globalSettingsPath))
                {
                    var loadedSettings = JsonSerializer.Deserialize<GlobalSettings>(File.ReadAllText(globalSettingsPath));
                    if (loadedSettings != null) appSettings = loadedSettings;
                }

                if (File.Exists(configFilePath))
                {
                    var loaded = JsonSerializer.Deserialize<List<TimerConfig>>(File.ReadAllText(configFilePath));
                    if (loaded != null)
                    {
                        foreach (var c in loaded)
                        {
                            if (c.FontSize <= 0) c.FontSize = 72;
                            if (c.BlinkTime <= 0) c.BlinkTime = 10;
                            if (c.AudioTime <= 0) c.AudioTime = 10;
                            configs.Add(c);
                        }
                    }
                }
            }
            catch { }
        }

        // ==========================================
        // UI 操作事件
        // ==========================================
        private void BrowseImage_Click(object sender, RoutedEventArgs e) {
            var dlg = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true) ImageInput.Text = dlg.FileName;
        }

        private void BrowseAudio_Click(object sender, RoutedEventArgs e) {
            var dlg = new OpenFileDialog { Filter = "Audio Files|*.mp3;*.wav;*.wma" };
            if (dlg.ShowDialog() == true) AudioPathInput.Text = dlg.FileName;
        }
        private void AddTimer_Click(object sender, RoutedEventArgs e) {
            if (!int.TryParse(TimeInput.Text, out int duration) ||
                !int.TryParse(XInput.Text, out int x) ||
                !int.TryParse(YInput.Text, out int y) ||
                !int.TryParse(WidthInput.Text, out int w) ||
                !int.TryParse(HeightInput.Text, out int h) ||
                !int.TryParse(FontSizeInput.Text, out int fontSize) ||
                !int.TryParse(BlinkTimeInput.Text, out int blinkTime) ||
                !int.TryParse(AudioTimeInput.Text, out int audioTime))
            {
                MessageBox.Show("請檢查數字欄位格式是否有誤", "輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string name = NameInput.Text.Trim();
            string hotkey = HotkeyInput.Text.Trim();
            string imgPath = ImageInput.Text.Trim();
            string audioPath = AudioPathInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(hotkey))
            {
                MessageBox.Show("名稱、快捷鍵請務必填寫", "欄位不完整", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int action = 0;
            if (ActionRestart.IsChecked == true) action = 1;
            if (ActionPause.IsChecked == true) action = 2;

            var existing = configs.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                configs.Remove(existing);
                if (activeTimers.TryGetValue(name, out var tw))
                {
                    tw.Close();
                    activeTimers.Remove(name);
                }
            }

            configs.Add(new TimerConfig
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
                RetriggerAction = action,
                ImagePath = imgPath,
                BlinkTime = blinkTime,
                AudioPath = audioPath,
                AudioTime = audioTime
            });

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

                BlinkTimeInput.Text = cfg.BlinkTime.ToString();
                AudioPathInput.Text = cfg.AudioPath;
                AudioTimeInput.Text = cfg.AudioTime.ToString();

                if (cfg.RetriggerAction == 1) ActionRestart.IsChecked = true;
                else if (cfg.RetriggerAction == 2) ActionPause.IsChecked = true;
                else ActionStop.IsChecked = true;
            }
        }

        private void OpenTTSWindow_Click(object sender, RoutedEventArgs e) {
            var ttsWindow = new ElevenLabsWindow { Owner = this };
            ttsWindow.Show();
        }

        // 全域設定變更
        private void StatusWindowTopMost_Changed(object sender, RoutedEventArgs e) {
            if (statusWindow != null && StatusWindowTopMostCheck.IsChecked.HasValue)
            {
                appSettings.IsStatusWindowTopMost = StatusWindowTopMostCheck.IsChecked.Value;
                statusWindow.Topmost = appSettings.IsStatusWindowTopMost;
                SaveConfigs();
            }
        }

        // ==========================================
        // 快捷鍵錄製邏輯
        // ==========================================
        private void HotkeyInput_GotFocus(object sender, RoutedEventArgs e) {
            HotkeyInput.BorderBrush = Brushes.Red;
            HotkeyInput.BorderThickness = new Thickness(2);
            RecordingBorder.Visibility = Visibility.Visible;
            RecordingHint.Visibility = Visibility.Collapsed;
            HotkeyInput.Text = "";
        }

        private void HotkeyInput_LostFocus(object sender, RoutedEventArgs e) {
            HotkeyInput.ClearValue(Border.BorderBrushProperty);
            HotkeyInput.ClearValue(Border.BorderThicknessProperty);
            RecordingBorder.Visibility = Visibility.Collapsed;
            RecordingHint.Visibility = Visibility.Visible;
        }

        private void HotkeyInput_RightDown(object sender, MouseButtonEventArgs e) {
            HotkeyInput.ClearValue(Border.BorderBrushProperty);
            HotkeyInput.ClearValue(Border.BorderThicknessProperty);
            RecordingBorder.Visibility = Visibility.Collapsed;
            RecordingHint.Visibility = Visibility.Visible;
        }

        private void HotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e) {
            e.Handled = true; // 阻止預設輸入
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // 如果只按下修飾鍵，繼續等待主鍵輸入
            if (IsModifierKey(key)) return;

            var modifiers = Keyboard.Modifiers;
            var segments = new List<string>();

            if (modifiers.HasFlag(ModifierKeys.Control)) segments.Add("ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) segments.Add("alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) segments.Add("shift");
            if (modifiers.HasFlag(ModifierKeys.Windows)) segments.Add("win");

            string keyName = key.ToString().ToLowerInvariant();
            if (key >= Key.D0 && key <= Key.D9) keyName = keyName.Substring(1);

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

            // 將錄製結果顯示在 TextBox 上
            HotkeyInput.Text = string.Join("+", segments);

            // 錄製完畢後自動移除焦點，結束錄製狀態
            Keyboard.ClearFocus();
        }
    }
}