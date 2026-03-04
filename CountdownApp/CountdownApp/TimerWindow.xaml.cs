using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CountdownApp
{
    public partial class TimerWindow : Window
    {
        private string hotkeyId;
        private int initialDuration; // 記住初始秒數供重置使用
        private int timeLeft;
        private bool autoRestart;    // 記住是否自動重啟
        private DispatcherTimer countdownTimer;
        private DispatcherTimer blinkTimer;

        // 參數新增了 imgWidth, imgHeight, autoRestart
        public TimerWindow(string hotkeyId, int duration, string imagePath, int x, int y, int imgWidth, int imgHeight, bool autoRestart)
        {
            InitializeComponent();
            this.hotkeyId = hotkeyId;
            this.initialDuration = duration;
            this.timeLeft = duration;
            this.autoRestart = autoRestart;

            // 1. 強制視窗置頂 (TopMost) 與設定座標
            this.Topmost = true;
            this.Left = x;
            this.Top = y;

            // 2. 套用自訂的圖片顯示大小
            BgImage.Width = imgWidth;
            BgImage.Height = imgHeight;

            // 3. 載入無邊框圖片
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

            // 4. 設定倒數計時器
            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownTimer.Start();

            // 5. 設定閃爍計時器
            blinkTimer = new DispatcherTimer();
            blinkTimer.Interval = TimeSpan.FromMilliseconds(300);
            blinkTimer.Tick += BlinkTimer_Tick;
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            timeLeft--;

            if (timeLeft > 0)
            {
                TimeText.Text = timeLeft.ToString();

                // 剩餘 5 秒 (含) 內啟動閃爍機制
                if (timeLeft <= 5 && !blinkTimer.IsEnabled)
                {
                    blinkTimer.Start();
                }
            }
            else
            {
                // 時間歸零的判斷邏輯
                if (this.autoRestart)
                {
                    // 【啟用自動重計】重置時間、停止閃爍、確保圖片顯示，然後繼續倒數
                    this.timeLeft = this.initialDuration;
                    this.TimeText.Text = this.timeLeft.ToString();

                    this.blinkTimer.Stop();
                    this.BgImage.Visibility = Visibility.Visible;
                }
                else
                {
                    // 【未啟用自動重計】直接關閉視窗
                    countdownTimer.Stop();
                    blinkTimer.Stop();
                    this.Close();
                }
            }
        }

        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            // 閃爍效果
            BgImage.Visibility = BgImage.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (countdownTimer != null) countdownTimer.Stop();
            if (blinkTimer != null) blinkTimer.Stop();
            base.OnClosed(e);
        }
    }
}