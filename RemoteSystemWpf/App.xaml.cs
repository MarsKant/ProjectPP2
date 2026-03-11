using System;
using System.Windows;
using RemoteSystemWpf.Helpers;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RemoteSystemWpf
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            KillZombieProcesses();

            var splash = new Window
            {
                Title = "Загрузка ресурсов",
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                Topmost = true
            };

            var txt = new System.Windows.Controls.TextBlock
            {
                Text = "Проверка компонентов...",
                Padding = new Thickness(20),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };
            splash.Content = txt;
            splash.Show();

            IProgress<string> progress = new Progress<string>(msg => txt.Text = msg);

            try
            {
                await DownloadHelper.DownloadFFmpeg(progress);
                await DownloadHelper.DownloadMediaMTX(progress);
                await DownloadHelper.DownloadVLC(progress);

                progress.Report("🚀 Все готово! Запуск...");
                await Task.Delay(500);

                var main = new MainWindow();
                this.MainWindow = main;
                splash.Close();
                main.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка загрузки: {ex.Message}\nПроверьте интернет-соединение.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Shutdown();
            }
        }

        public static void KillZombieProcesses()
        {
            string[] targets = { "ffmpeg", "mediamtx" };
            foreach (var name in targets)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            KillZombieProcesses();
            base.OnExit(e);
        }
    }
}