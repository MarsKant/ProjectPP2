using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibVLCSharp.Shared;

namespace RemoteSystemWpf.Pages
{
    public partial class ClientPage : Page
    {
        private LibVLC _libVLC;
        private LibVLCSharp.Shared.MediaPlayer _displayPlayer;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _isFullscreen = false;

        public ClientPage()
        {
            InitializeComponent();
            _libVLC = new LibVLC();
            _displayPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

            // Привязываем плеер только к нижнему слою отображения
            VideoView_Display.MediaPlayer = _displayPlayer;

            // Подписываемся на события клавиатуры всей страницы
            this.KeyDown += InputOverlay_KeyDown;
            this.KeyUp += InputOverlay_KeyUp;
        }

        private async void Connect(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = IpBox.Text;
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ip, 8890); // Порт команд
                _stream = _tcpClient.GetStream();

                string rtspUrl = $"rtsp://{ip}:{PortBox.Text}/stream";
                var media = new Media(_libVLC, new Uri(rtspUrl));

                // Опции для минимизации задержки и отключения захвата мыши самим VLC
                media.AddOption(":no-mouse-events");
                media.AddOption(":no-keyboard-events");
                media.AddOption(":rtsp-transport=tcp");
                media.AddOption(":network-caching=100");

                _displayPlayer.Play(media);

                ConnectBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = true;

                // Переводим фокус на слой команд, чтобы сразу ловить мышь и кнопки
                VideoView_Commands.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
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

        private void Disconnect(object sender, RoutedEventArgs e)
        {
            _displayPlayer?.Stop();
            _tcpClient?.Close();
            _stream = null;
            ConnectBtn.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;

            if (_isFullscreen) ToggleFullscreen();
        }

        private void InputOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // КРИТИЧЕСКИ ВАЖНО: Захватываем мышь на наш Border
            // Это заставляет WPF игнорировать нативное окно VLC под ним
            VideoView_Commands.CaptureMouse();
            VideoView_Commands.Focus();

            string btn = e.ChangedButton == MouseButton.Left ? "LEFT" : "RIGHT";
            SendCommand($"MOUSE_DOWN|{btn}");
        }

        private void InputOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            // Если мышь захвачена или просто двигается над нами
            Point p = e.GetPosition(VideoView_Commands);

            double serverWidth = 1920;
            double serverHeight = 1080;

            if (VideoView_Commands.ActualWidth > 0 && VideoView_Commands.ActualHeight > 0)
            {
                int x = (int)(p.X * serverWidth / VideoView_Commands.ActualWidth);
                int y = (int)(p.Y * serverHeight / VideoView_Commands.ActualHeight);

                x = Math.Max(0, Math.Min((int)serverWidth, x));
                y = Math.Max(0, Math.Min((int)serverHeight, y));

                SendCommand($"MOUSE_MOVE|{x}|{y}");
            }
        }

        private void InputOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // ОТПУСКАЕМ захват мыши, когда кнопка отжата
            if (VideoView_Commands.IsMouseCaptured)
            {
                VideoView_Commands.ReleaseMouseCapture();
            }

            string btn = e.ChangedButton == MouseButton.Left ? "LEFT" : "RIGHT";
            SendCommand($"MOUSE_UP|{btn}");
        }

        private void InputOverlay_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            SendCommand($"MOUSE_WHEEL|{e.Delta}");
        }

        private void InputOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }
            SendCommand($"KEY_DOWN|{(int)e.Key}");
            e.Handled = true;
        }

        private void InputOverlay_KeyUp(object sender, KeyEventArgs e)
        {
            SendCommand($"KEY_UP|{(int)e.Key}");
            e.Handled = true;
        }

        // --- ЛОГИКА ПОЛНОЭКРАННОГО РЕЖИМА ---
        private void ToggleFullscreen()
        {
            Window win = Window.GetWindow(this);
            if (win == null) return;

            if (!_isFullscreen)
            {
                ConnectionPanel.Visibility = Visibility.Collapsed;
                BackgroundLayer.Visibility = Visibility.Collapsed;

                // Убираем отступы контейнера, чтобы видео было на весь экран
                VideoContainer.Margin = new Thickness(0);
                VideoContainer.CornerRadius = new CornerRadius(0);

                win.WindowStyle = WindowStyle.None;
                win.WindowState = WindowState.Maximized;
                _isFullscreen = true;
            }
            else
            {
                ConnectionPanel.Visibility = Visibility.Visible;
                BackgroundLayer.Visibility = Visibility.Visible;

                // Возвращаем отступы
                VideoContainer.Margin = new Thickness(15, 0, 15, 15);
                VideoContainer.CornerRadius = new CornerRadius(8);

                win.WindowStyle = WindowStyle.SingleBorderWindow;
                win.WindowState = WindowState.Normal;
                _isFullscreen = false;
            }
            VideoView_Commands.Focus();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Disconnect(null, null);
            _displayPlayer?.Dispose();
            _libVLC?.Dispose();
        }
    }
}