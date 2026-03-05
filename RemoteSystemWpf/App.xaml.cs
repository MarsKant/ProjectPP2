using System;
using System.Windows;
using RemoteSystemWpf.Helpers;
using System.Threading.Tasks;
using System.IO;

namespace RemoteSystemWpf
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            var splashWindow = new Window
            {
                Title = "Загрузка",
                Width = 450,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true
            };

            var progressText = new System.Windows.Controls.TextBlock
            {
                Text = "Проверка компонентов...",
                Margin = new Thickness(20),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                FontSize = 14,
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            splashWindow.Content = progressText;
            splashWindow.Show();

            // Проверяем наличие FFmpeg
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                progressText.Text = "Скачивание FFmpeg...";
                await DownloadHelper.DownloadFFmpeg();
            }

            // Проверяем наличие MediaMTX
            string mtxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MediaMTX", "mediamtx.exe");
            if (!File.Exists(mtxPath))
            {
                progressText.Text = "Скачивание MediaMTX...";
                await DownloadHelper.DownloadMediaMTX();
            }

            splashWindow.Close();
            base.OnStartup(e);
        }
    }
}