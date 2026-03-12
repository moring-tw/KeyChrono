using Microsoft.Win32;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace KeyChrono
{
    public partial class ElevenLabsWindow : Window
    {
        // 模型依舊寫死為 v2 以支援中文
        private const string ModelId = "eleven_multilingual_v2";

        public ElevenLabsWindow()
        {
            InitializeComponent();
        }

        // 開啟教學頁面
        private void OpenApiKeyTutorial_Click(object sender, RoutedEventArgs e)
        {
            var tutorialWindow = new HowToApplyElevenLabs_TTS_API_Key
            {
                Owner = this
            };
            tutorialWindow.ShowDialog();
        }

        private void BrowseSavePath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "MP3 Audio File|*.mp3",
                Title = "選擇儲存路徑",
                FileName = "output.mp3"
            };

            if (dlg.ShowDialog() == true)
            {
                SavePathInput.Text = dlg.FileName;
            }
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = ApiKeyInput.Text.Trim();
            string text = TextInput.Text.Trim();
            string savePath = SavePathInput.Text.Trim();

            // 判斷要使用的 Voice ID
            string voiceId = string.Empty;
            if (VoiceFemale.IsChecked == true)
            {
                voiceId = "x3ktUIG7gge4IhocTKbc";
            }
            else if (VoiceMale.IsChecked == true)
            {
                voiceId = "mwsfTAK3my23vP5QPPiC";
            }
            else if (VoiceCustom.IsChecked == true)
            {
                voiceId = CustomVoiceIdInput.Text.Trim();
            }

            // 防呆驗證
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("請輸入 API Key！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(voiceId))
            {
                MessageBox.Show("請選擇或輸入語音 ID！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("請輸入要生成的文字！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(savePath))
            {
                MessageBox.Show("請選擇儲存路徑！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GenerateButton.IsEnabled = false;
            StatusText.Text = "⏳ 正在呼叫 ElevenLabs API 生成語音中...";
            StatusText.Foreground = Brushes.DarkBlue;

            try
            {
                // 將 voiceId 傳入 API 請求
                await CallElevenLabsApiAsync(apiKey, voiceId, text, savePath);

                StatusText.Text = $"✅ 成功！音檔已儲存至:\n{savePath}";
                StatusText.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ 錯誤:\n{ex.Message}";
                StatusText.Foreground = Brushes.Red;
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }

        // 新增 voiceId 參數
        private async Task CallElevenLabsApiAsync(string apiKey, string voiceId, string text, string savePath)
        {
            string endpoint = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

            var payload = new
            {
                text = text,
                model_id = ModelId
            };
            string jsonPayload = JsonSerializer.Serialize(payload);

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("xi-api-key", apiKey);
                client.DefaultRequestHeaders.Add("Accept", "audio/mpeg");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await client.PostAsync(endpoint, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorDetail = await response.Content.ReadAsStringAsync();
                        throw new Exception($"API 請求失敗 ({response.StatusCode}): {errorDetail}");
                    }

                    var audioData = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(savePath, audioData);
                }
            }
        }
    }
}