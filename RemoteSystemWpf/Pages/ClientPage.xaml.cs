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
            try
            {
                if (_libVLC == null)
                {
                    _libVLC = new LibVLC("--rtsp-tcp", "--network-caching=200", "--quiet");
                    _mediaPlayer = new MediaPlayer(_libVLC);
                    VideoView.MediaPlayer = _mediaPlayer;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("VLC Error: " + ex.Message);
            }
        }

        private async void Connect(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = IpBox.Text;
                if (!int.TryParse(PortBox.Text, out int cmdPort)) return;

                // 1. Подключение к командам
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ip, cmdPort);
                _stream = _tcpClient.GetStream();

                // 2. Запуск видео (8554 - стандартный порт MediaMTX)
                string rtspUrl = $"rtsp://{ip}:8554/stream";

                // Добавляем задержку, чтобы сервер успел пробросить поток от FFmpeg
                await Task.Delay(1000);

                using (var media = new Media(_libVLC, new Uri(rtspUrl)))
                {
                    media.AddOption(":rtsp-transport=tcp"); // Форсируем TCP
                    media.AddOption(":network-caching=200");
                    _mediaPlayer.Play(media);
                }

                ConnectBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = true;
                InputOverlay.Focus();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
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

        // --- Обработка ввода (Убедись, что InputOverlay - это прозрачный Rect/Grid поверх VideoView) ---
        private void InputOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(InputOverlay);
            // Простейшая передача координат (нужно масштабировать под разрешение сервера)
            SendCommand($"MOUSE_MOVE|{(int)p.X}|{(int)p.Y}");
        }

        private void InputOverlay_MouseDown(object sender, MouseButtonEventArgs e) =>
            SendCommand($"MOUSE_DOWN|{(e.LeftButton == MouseButtonState.Pressed ? 0 : 1)}");

        private void InputOverlay_MouseUp(object sender, MouseButtonEventArgs e) =>
            SendCommand($"MOUSE_UP|{(e.LeftButton == MouseButtonState.Released ? 0 : 1)}");

        private void Disconnect(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.Stop();
            _stream?.Close();
            _tcpClient?.Close();
            ConnectBtn.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) => Disconnect(null, null);

        private void InputOverlay_MouseWheel(object sender, MouseWheelEventArgs e) =>
            SendCommand($"MOUSE_WHEEL|{e.Delta}");

        private void InputOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            // Отправляем команду на сервер
            SendCommand($"KEY_DOWN|{(int)KeyInterop.VirtualKeyFromKey(e.Key)}");

            // Локальная обработка F11 для режима "Во весь экран"
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
            }
        }
        private void ToggleFullScreen()
        {
            var window = Window.GetWindow(this);
            if (window == null) return;

            if (!_isFullScreen)
            {
                // Переходим в Fullscreen
                window.WindowStyle = WindowStyle.None;
                window.WindowState = WindowState.Maximized;
                window.Topmost = true; // Поверх всех окон
                _isFullScreen = true;
            }
            else
            {
                // Возвращаемся в оконный режим
                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.WindowState = WindowState.Normal;
                window.Topmost = false;
                _isFullScreen = false;
            }
        }

        private void InputOverlay_KeyUp(object sender, KeyEventArgs e) =>
            SendCommand($"KEY_UP|{(int)KeyInterop.VirtualKeyFromKey(e.Key)}");

        private void InputOverlay_GotFocus(object sender, RoutedEventArgs e) { /* Можно добавить визуальную индикацию */ }
    }
}