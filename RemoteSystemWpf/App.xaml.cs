using System;
using System.Windows;
using RemoteSystemWpf.Helpers;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using System.Diagnostics;

namespace RemoteSystemWpf
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Первым делом очищаем старые процессы, если они остались от прошлого запуска
            KillZombieProcesses();

            Console.WriteLine("[DEBUG] Приложение OnStartup запущено.");

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                MessageBox.Show($"Критическая ошибка: {ex.ExceptionObject}");

            // Принудительная очистка при выходе из процесса
            AppDomain.CurrentDomain.ProcessExit += (s, ev) => KillZombieProcesses();

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                Console.WriteLine("[DEBUG] Инициализация Core LibVLC...");
                Core.Initialize();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ошибка инициализации VLC: {ex.Message}");
            }

            var splashWindow = new Window
            {
                Title = "Загрузка системы",
                Width = 450,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                Topmost = true
            };

            var progressText = new System.Windows.Controls.TextBlock
            {
                Text = "Подготовка среды...",
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                TextAlignment = TextAlignment.Center
            };

            splashWindow.Content = progressText;
            splashWindow.Show();

            IProgress<string> progress = new Progress<string>(msg => {
                progressText.Text = msg;
                Console.WriteLine($"[LOG] {msg}");
            });

            try
            {
                await DownloadHelper.DownloadFFmpeg(progress);
                await DownloadHelper.DownloadMediaMTX(progress);
                await DownloadHelper.DownloadVLC(progress);

                Console.WriteLine("[DEBUG] Все компоненты готовы. Запуск MainWindow...");
                var mainWindow = new MainWindow();
                this.MainWindow = mainWindow;

                splashWindow.Close();
                mainWindow.Show();
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] Ошибка загрузки: {ex}");
                MessageBox.Show($"Ошибка при подготовке системы: {ex.Message}");
                this.Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            KillZombieProcesses();
            base.OnExit(e);
        }

        // Глобальный метод очистки
        public static void KillZombieProcesses()
        {
            string[] engines = { "ffmpeg", "mediamtx" };

            foreach (var name in engines)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(500);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
}