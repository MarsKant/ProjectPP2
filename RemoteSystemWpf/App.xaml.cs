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
                Height = 200,
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

            var progress = new Progress<string>(msg =>
            {
                progressText.Text = msg;
            });

            // Скачиваем всё необходимое
            await DownloadHelper.DownloadFFmpeg(progress);
            await DownloadHelper.DownloadMediaMTX(progress);
            await DownloadHelper.DownloadVLC(progress);

            splashWindow.Close();

            base.OnStartup(e);
        }
    }
}