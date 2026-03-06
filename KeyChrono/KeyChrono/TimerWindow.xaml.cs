using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace KeyChrono
{
    public partial class TimerWindow : Window
    {
        private string timerName;
        private int initialDuration;
        private int timeLeft;
        private bool autoRestart;
        private DispatcherTimer countdownTimer;
        private DispatcherTimer blinkTimer;
        private bool isRunning;

        public Action<string, int, int> OnLocationChanged;

        // 新增接收 fontSize 參數
        public TimerWindow(string timerName, int duration, string imagePath, int x, int y, int imgWidth, int imgHeight, int fontSize, bool autoRestart) {
            InitializeComponent();
            this.timerName = timerName;
            this.initialDuration = duration;
            this.timeLeft = duration;
            this.autoRestart = autoRestart;

            NameText.Text = timerName;

            // 套用主畫面設定的字體大小
            TimeText.FontSize = fontSize;

            this.Topmost = true;
            this.Left = x;
            this.Top = y;

            BgImage.Width = imgWidth;
            BgImage.Height = imgHeight;

            try
            {
                BitmapImage bitmap = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                BgImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show("圖片載入失敗: " + ex.Message);
            }

            TimeText.Text = timeLeft.ToString();

            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;

            blinkTimer = new DispatcherTimer();
            blinkTimer.Interval = TimeSpan.FromMilliseconds(300);
            blinkTimer.Tick += BlinkTimer_Tick;

            StartTimer();
        }

        public void StartTimer() {
            timeLeft = initialDuration;
            TimeText.Text = timeLeft.ToString();
            BgImage.Visibility = Visibility.Visible;
            countdownTimer.Start();
            isRunning = true;
        }

        public void StopAndResetTimer() {
            countdownTimer.Stop();
            blinkTimer.Stop();
            BgImage.Visibility = Visibility.Visible;
            timeLeft = initialDuration;
            TimeText.Text = "STOP";
            isRunning = false;
        }

        public void ToggleTimer() {
            if (isRunning) StopAndResetTimer();
            else StartTimer();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e) {
            timeLeft--;
            if(countdownTimer.IsEnabled == false)
            {
                TimeText.Text = "STOP";
                blinkTimer.Stop();
                isRunning = false;
                return;
            }

            if (timeLeft > 0)
            {
                TimeText.Text = timeLeft.ToString();
                if (timeLeft <= 5 && !blinkTimer.IsEnabled) blinkTimer.Start();
            } else
            {
                if (this.autoRestart)
                {
                    timeLeft = initialDuration;
                    TimeText.Text = timeLeft.ToString();
                    blinkTimer.Stop();
                    BgImage.Visibility = Visibility.Visible;
                } else
                {
                    countdownTimer.Stop();
                    blinkTimer.Stop();
                    BgImage.Visibility = Visibility.Visible;
                    TimeText.Text = "0";
                    isRunning = false;
                }
            }
        }

        private void BlinkTimer_Tick(object sender, EventArgs e) {
            BgImage.Visibility = BgImage.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
                OnLocationChanged?.Invoke(this.timerName, (int)this.Left, (int)this.Top);
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            this.Close();
        }

        protected override void OnClosed(EventArgs e) {
            if (countdownTimer != null) countdownTimer.Stop();
            if (blinkTimer != null) blinkTimer.Stop();
            base.OnClosed(e);
        }
    }
}