using System;
using System.Windows;
using RemoteSystemWpf.Helpers;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace RemoteSystemWpf
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Console.WriteLine("[DEBUG] Приложение OnStartup запущено.");

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                MessageBox.Show($"Критическая ошибка: {ex.ExceptionObject}");

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Инициализация VLC с параметрами против ошибок COM и аудио
            try
            {
                Console.WriteLine("[DEBUG] Инициализация Core LibVLC...");
                // Передаем параметры сразу при инициализации ядра, если возможно, 
                // или гарантируем, что это произойдет один раз.
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
                // Эмуляция/проверка компонентов
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
    }
}