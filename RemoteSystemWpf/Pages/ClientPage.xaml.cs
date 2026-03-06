using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace RemoteSystemWpf.Pages
{
    public partial class ClientPage : Page
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _isFullScreen = false;

        public ClientPage()
        {
            InitializeComponent();
            _libVLC = new LibVLC("--rtsp-tcp", "--network-caching=300");
            _mediaPlayer = new MediaPlayer(_libVLC);
            VideoView.MediaPlayer = _mediaPlayer;
        }

        private async void Connect(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = IpBox.Text;
                if (!int.TryParse(PortBox.Text, out int port)) port = 8890;

                _tcpClient = new TcpClient();

                // Попытка подключения к серверу команд
                var connectTask = _tcpClient.ConnectAsync(ip, port);

                // Ждем 3 секунды, если не вышло — отмена
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask)
                {
                    await connectTask;
                    _stream = _tcpClient.GetStream();

                    // Запуск видео потока (обычно на 8554)
                    string rtspUrl = $"rtsp://{ip}:8554/stream";
                    var media = new Media(_libVLC, new Uri(rtspUrl));
                    media.AddOption(":rtsp-transport=tcp");
                    _mediaPlayer.Play(media);

                    ConnectBtn.IsEnabled = false;
                    DisconnectBtn.IsEnabled = true;
                    InputOverlay.Focus(); // Даем фокус для управления
                }
                else
                {
                    MessageBox.Show($"Сервер на {ip}:{port} не отвечает.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сокета: " + ex.Message);
            }
        }

        private void SendCommand(string cmd)
        {
            if (_stream == null || !_tcpClient.Connected) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(cmd + "\n");
                _stream.Write(data, 0, data.Length);
            }
            catch { }
        }

        // --- УПРАВЛЕНИЕ ---

        private void InputOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(InputOverlay);
            // Масштабирование под стандарт 1920x1080 (замени на свое если нужно)
            int x = (int)(p.X * (1920.0 / InputOverlay.ActualWidth));
            int y = (int)(p.Y * (1080.0 / InputOverlay.ActualHeight));
            SendCommand($"MOUSE_MOVE|{x}|{y}");
        }

        private void InputOverlay_MouseDown(object sender, MouseButtonEventArgs e) =>
            SendCommand($"MOUSE_DOWN|{(e.LeftButton == MouseButtonState.Pressed ? 0 : 1)}");

        private void InputOverlay_MouseUp(object sender, MouseButtonEventArgs e) =>
            SendCommand($"MOUSE_UP|{(e.LeftButton == MouseButtonState.Released ? 0 : 1)}");

        private void InputOverlay_MouseWheel(object sender, MouseWheelEventArgs e) =>
            SendCommand($"MOUSE_WHEEL|{e.Delta}");

        private void InputOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            // Обработка F11 для Fullscreen
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
                return;
            }

            SendCommand($"KEY_DOWN|{(int)KeyInterop.VirtualKeyFromKey(e.Key)}");
        }

        private void InputOverlay_KeyUp(object sender, KeyEventArgs e) =>
            SendCommand($"KEY_UP|{(int)KeyInterop.VirtualKeyFromKey(e.Key)}");

        private void InputOverlay_GotFocus(object sender, RoutedEventArgs e)
        {
            // Визуально можно подсветить рамку, чтобы понять, что ввод активен
            VideoContainer.BorderBrush = System.Windows.Media.Brushes.Orange;
        }

        // --- РЕЖИМ ВО ВЕСЬ ЭКРАН ---
        private void ToggleFullScreen()
        {
            var window = Window.GetWindow(this);
            if (window == null) return;

            if (!_isFullScreen)
            {
                // 1. Прячем лишнее на странице
                ConnectionPanel.Visibility = Visibility.Collapsed;
                VideoContainer.Margin = new Thickness(0);

                // 2. Растягиваем окно на весь монитор
                window.WindowStyle = WindowStyle.None;
                window.Topmost = true;
                window.WindowState = WindowState.Maximized;

                _isFullScreen = true;
            }
            else
            {
                // Возвращаем интерфейс
                ConnectionPanel.Visibility = Visibility.Visible;
                VideoContainer.Margin = new Thickness(15);

                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.Topmost = false;
                window.WindowState = WindowState.Normal;

                _isFullScreen = false;
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.Stop();
            _stream?.Close();
            _tcpClient?.Close();
            ConnectBtn.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;
            VideoContainer.BorderBrush = System.Windows.Media.Brushes.Black;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) => Disconnect(null, null);
    }
}