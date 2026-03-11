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
        // 預設寫死的參數
        private const string VoiceId = "x3ktUIG7gge4IhocTKbc"; // Rachel 聲音模型
        private const string ModelId = "eleven_multilingual_v2"; // v2 支援多國語言(含中文)

        public ElevenLabsWindow()
        {
            InitializeComponent();
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

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(savePath))
            {
                MessageBox.Show("API Key、文字內容與儲存路徑均不得為空！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GenerateButton.IsEnabled = false;
            StatusText.Text = "⏳ 正在呼叫 ElevenLabs API 生成語音中...";
            StatusText.Foreground = Brushes.DarkBlue;

            try
            {
                await CallElevenLabsApiAsync(apiKey, text, savePath);

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

        private async Task CallElevenLabsApiAsync(string apiKey, string text, string savePath)
        {
            string endpoint = $"https://api.elevenlabs.io/v1/text-to-speech/{VoiceId}";

            var payload = new
            {
                text = text,
                model_id = ModelId
            };
            string jsonPayload = JsonSerializer.Serialize(payload);

            using (HttpClient client = new HttpClient())
            {
                // 設定 API Headers
                client.DefaultRequestHeaders.Add("xi-api-key", apiKey);
                client.DefaultRequestHeaders.Add("Accept", "audio/mpeg");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // 發送 POST 請求
                using (HttpResponseMessage response = await client.PostAsync(endpoint, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorDetail = await response.Content.ReadAsStringAsync();
                        throw new Exception($"API 請求失敗 ({response.StatusCode}): {errorDetail}");
                    }

                    // 取得二進位音檔資料並直接寫入磁碟 (超簡化寫法)
                    var audioData = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(savePath, audioData);
                }
            }
        }
    }
}