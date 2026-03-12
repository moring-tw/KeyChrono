using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace KeyChrono
{
    public partial class HowToApplyElevenLabs_TTS_API_Key : Window
    {
        public HowToApplyElevenLabs_TTS_API_Key()
        {
            InitializeComponent();
        }

        // 點擊超連結時，呼叫預設瀏覽器開啟網頁
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        // 關閉視窗
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}