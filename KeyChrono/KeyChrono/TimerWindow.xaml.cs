using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

        // 警示設定相關變數
        private int blinkTime;
        private string audioPath;
        private int audioTime;
        private MediaPlayer? audioPlayer;
        private bool hasPlayedAudio = false;

        public Action<string, int, int>? OnLocationChanged;

        public int TimeLeft => timeLeft;
        public int InitialDuration => initialDuration;
        public bool IsRunning => isRunning;

        public TimerWindow(string timerName, int duration, string imagePath, int x, int y, int imgWidth, int imgHeight, int fontSize, bool autoRestart, int blinkTime, string audioPath, int audioTime)
        {
            InitializeComponent();
            this.timerName = timerName;
            this.initialDuration = duration;
            this.timeLeft = duration;
            this.autoRestart = autoRestart;

            // 賦值警示設定
            this.blinkTime = blinkTime;
            this.audioPath = audioPath;
            this.audioTime = audioTime;

            NameText.Text = timerName;
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

            // 若有設定音效路徑且檔案存在，初始化 MediaPlayer
            if (!string.IsNullOrWhiteSpace(this.audioPath) && File.Exists(this.audioPath))
            {
                audioPlayer = new MediaPlayer();
                audioPlayer.Open(new Uri(this.audioPath, UriKind.Absolute));
            }

            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;

            blinkTimer = new DispatcherTimer();
            blinkTimer.Interval = TimeSpan.FromMilliseconds(300);
            blinkTimer.Tick += BlinkTimer_Tick;

            ResetAndStart();
        }

        public void ResetAndStart()
        {
            timeLeft = initialDuration;
            TimeText.Text = timeLeft.ToString();

            blinkTimer.Stop();
            BgImage.Visibility = Visibility.Visible;

            // 重置音效播放狀態
            hasPlayedAudio = false;
            audioPlayer?.Stop();

            countdownTimer.Start();
            isRunning = true;
        }

        public void StopAndReset()
        {
            countdownTimer.Stop();
            blinkTimer.Stop();
            audioPlayer?.Stop();

            BgImage.Visibility = Visibility.Visible;
            timeLeft = initialDuration;
            TimeText.Text = "STOP";
            isRunning = false;
        }

        public void Pause()
        {
            countdownTimer.Stop();
            blinkTimer.Stop();
            audioPlayer?.Pause();

            BgImage.Visibility = Visibility.Visible;
            isRunning = false;
        }

        public void Resume()
        {
            TimeText.Text = timeLeft.ToString();
            BgImage.Visibility = Visibility.Visible;
            countdownTimer.Start();
            isRunning = true;

            if (hasPlayedAudio) audioPlayer?.Play();
            if (timeLeft <= blinkTime) blinkTimer.Start();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            timeLeft--;

            if (timeLeft > 0)
            {
                TimeText.Text = timeLeft.ToString();

                // 判斷是否啟動閃爍
                if (timeLeft <= blinkTime && !blinkTimer.IsEnabled) blinkTimer.Start();

                // 判斷是否播放音效 (只在抵達設定秒數時觸發一次)
                if (timeLeft <= audioTime && !hasPlayedAudio && audioPlayer != null)
                {
                    audioPlayer.Position = TimeSpan.Zero;
                    audioPlayer.Play();
                    hasPlayedAudio = true;
                }
            }
            else
            {
                if (this.autoRestart)
                {
                    timeLeft = initialDuration;
                    TimeText.Text = timeLeft.ToString();
                    blinkTimer.Stop();
                    BgImage.Visibility = Visibility.Visible;

                    hasPlayedAudio = false;
                    audioPlayer?.Stop();
                }
                else
                {
                    countdownTimer.Stop();
                    blinkTimer.Stop();
                    audioPlayer?.Stop();

                    BgImage.Visibility = Visibility.Visible;
                    TimeText.Text = "0";
                    isRunning = false;
                }
            }
        }

        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            BgImage.Visibility = BgImage.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
                OnLocationChanged?.Invoke(this.timerName, (int)this.Left, (int)this.Top);
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (countdownTimer != null) countdownTimer.Stop();
            if (blinkTimer != null) blinkTimer.Stop();
            if (audioPlayer != null) audioPlayer.Close();
            base.OnClosed(e);
        }
    }
}