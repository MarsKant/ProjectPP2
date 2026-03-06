using System;
using System.Windows;
using RemoteSystemWpf.Helpers;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Threading;

namespace RemoteSystemWpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Показываем окно загрузки
            var splash = new Window
            {
                Title = "Загрузка",
                Width = 400,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true
            };

            var text = new System.Windows.Controls.TextBlock
            {
                Text = "Проверка компонентов...",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                FontSize = 14
            };

            splash.Content = text;
            splash.Show();

            // Запускаем загрузку в фоне
            var dispatcher = Dispatcher.CurrentDispatcher;

            Task.Run(async () =>
            {
                try
                {
                    // Проверяем FFmpeg
                    string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");
                    if (!File.Exists(ffmpegPath))
                    {
                        dispatcher.Invoke(() => text.Text = "Скачивание FFmpeg...");
                        await DownloadHelper.DownloadFFmpeg();
                    }

                    // Проверяем MediaMTX
                    string mtxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MediaMTX", "mediamtx.exe");
                    if (!File.Exists(mtxPath))
                    {
                        dispatcher.Invoke(() => text.Text = "Скачивание MediaMTX...");
                        await DownloadHelper.DownloadMediaMTX();
                    }
                }
                catch (Exception ex)
                {
                    dispatcher.Invoke(() => MessageBox.Show($"Ошибка: {ex.Message}"));
                }
                finally
                {
                    dispatcher.Invoke(() => splash.Close());
                }
            });

            // Запускаем главное окно
            base.OnStartup(e);
        }
    }
}