using System;
using System.Windows;
using RemoteSystemWpf.Helpers;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

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
                Title = "Инициализация системы",
                Width = 450,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                Topmost = true,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Content = new System.Windows.Controls.Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                    CornerRadius = new CornerRadius(10),
                    Child = new System.Windows.Controls.TextBlock
                    {
                        Name = "StatusText",
                        Text = "Проверка компонентов...",
                        Foreground = System.Windows.Media.Brushes.White,
                        Padding = new Thickness(20),
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            var txt = (System.Windows.Controls.TextBlock)((System.Windows.Controls.Border)splash.Content).Child;
            splash.Show();

            IProgress<string> progress = new Progress<string>(msg => txt.Text = msg);

            try
            {
                await DownloadHelper.DownloadFFmpeg(progress);
                await DownloadHelper.DownloadMediaMTX(progress);
                await DownloadHelper.DownloadVLC(progress);

                progress.Report("🌐 Настройка защищенной сети...");
                bool tsInstalled = await DownloadHelper.InstallTailscale(progress);

                if (tsInstalled)
                {
                    await SetupTailscaleNetworking(progress);
                }

                progress.Report("🚀 Все готово! Запуск...");
                await Task.Delay(1000);

                var main = new MainWindow();
                this.MainWindow = main;
                splash.Close();
                main.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Shutdown();
            }
        }

        private async Task SetupTailscaleNetworking(IProgress<string> progress)
        {
            try
            {
                string tsExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe");
                if (!File.Exists(tsExe)) return;

                progress.Report("🔑 Авторизация в сети Tailscale...");
                await Task.Run(() =>
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = tsExe,
                        Arguments = "up",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    var p = Process.Start(startInfo);
                    p?.WaitForExit(5000);
                });
            }
            catch {}
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