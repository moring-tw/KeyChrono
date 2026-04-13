using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KeyChrono
{
    public partial class StatusWindow : Window
    {
        public Action<bool>? OnStatusChanged;

        public StatusWindow() {
            InitializeComponent();

            try
            {
                // 載入背景圖
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "GlassBack.png");
                if (File.Exists(path))
                {
                    BgImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
                    BackgroundContainer.Background = Brushes.Transparent; // 成功載入圖片，將底色變透明
                }
            }
            catch { }

            StatusToggle.IsChecked = true; // 預設開啟
        }

        public void SetToggleState(bool isEnabled) {
            StatusToggle.IsChecked = isEnabled;
        }

        private void StatusToggle_Changed(object sender, RoutedEventArgs e) {
            bool isEnabled = StatusToggle.IsChecked == true;
            OnStatusChanged?.Invoke(isEnabled);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove(); // 允許拖曳
            }
        }
    }
}