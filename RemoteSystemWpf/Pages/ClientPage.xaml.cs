using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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

        // Разрешение сервера (обновится автоматически при подключении)
        private double _serverWidth = 1920;
        private double _serverHeight = 1080;

        public ClientPage()
        {
            InitializeComponent();
            _libVLC = new LibVLC();
            _displayPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            VideoView_Display.MediaPlayer = _displayPlayer;

            // Обработка клавиш на уровне страницы
            this.KeyDown += InputOverlay_KeyDown;
            this.KeyUp += InputOverlay_KeyUp;

            // Динамическое обновление позиции Popup при изменении размеров окна
            this.SizeChanged += (s, e) => UpdatePopupPosition();
        }

        private async void Connect(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = IpBox.Text;
                _tcpClient = new TcpClient();

                // Подключаемся к порту команд (8890)
                await _tcpClient.ConnectAsync(ip, 8890);
                _stream = _tcpClient.GetStream();

                // 1. РУКОПОЖАТИЕ: Ожидаем от сервера его разрешение (например, "SIZE|2560|1440")
                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                if (response.StartsWith("SIZE"))
                {
                    string[] parts = response.Split('|');
                    _serverWidth = double.Parse(parts[1]);
                    _serverHeight = double.Parse(parts[2]);
                }

                // 2. ЗАПУСК ВИДЕО
                string rtspUrl = $"rtsp://{ip}:{PortBox.Text}/stream";
                var media = new Media(_libVLC, new Uri(rtspUrl));
                media.AddOption(":no-mouse-events");
                media.AddOption(":no-keyboard-events");
                media.AddOption(":rtsp-transport=tcp");
                media.AddOption(":network-caching=100");

                _displayPlayer.Play(media);

                // Принудительно растягиваем видео под размер контейнера
                ApplyAspectRatio();

                ConnectBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = true;
                InputPopup.IsOpen = true;

                await Task.Delay(500);
                UpdatePopupPosition();
                VideoView_Commands.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        private void ApplyAspectRatio()
        {
            if (_displayPlayer != null && VideoView_Display.ActualWidth > 0)
            {
                // Убираем черные полосы, заставляя видео занять 100% площади
                _displayPlayer.AspectRatio = $"{(int)VideoView_Display.ActualWidth}:{(int)VideoView_Display.ActualHeight}";
            }
        }

        private void UpdatePopupPosition()
        {
            if (!InputPopup.IsOpen) return;

            // Popup должен быть строго по размеру видео-вью
            InputPopup.Width = VideoView_Display.ActualWidth;
            InputPopup.Height = VideoView_Display.ActualHeight;

            // Форсируем перерисовку позиции
            double h = InputPopup.HorizontalOffset;
            InputPopup.HorizontalOffset = h + 0.01;
            InputPopup.HorizontalOffset = h;
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

        // --- УПРАВЛЕНИЕ МЫШЬЮ ---
        private void InputOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VideoView_Commands.CaptureMouse();
            VideoView_Commands.Focus();
            string btn = e.ChangedButton == MouseButton.Left ? "LEFT" : "RIGHT";
            SendCommand($"MOUSE_DOWN|{btn}");
        }

        private void InputOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (VideoView_Commands.ActualWidth <= 0 || VideoView_Commands.ActualHeight <= 0) return;

            Point p = e.GetPosition(VideoView_Commands);

            // Теперь расчет идет исходя из РЕАЛЬНОГО разрешения сервера
            int finalX = (int)(p.X * _serverWidth / VideoView_Commands.ActualWidth);
            int finalY = (int)(p.Y * _serverHeight / VideoView_Commands.ActualHeight);

            // Ограничение координат
            finalX = Math.Max(0, Math.Min((int)_serverWidth, finalX));
            finalY = Math.Max(0, Math.Min((int)_serverHeight, finalY));

            SendCommand($"MOUSE_MOVE|{finalX}|{finalY}");
        }

        private void InputOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (VideoView_Commands.IsMouseCaptured) VideoView_Commands.ReleaseMouseCapture();
            string btn = e.ChangedButton == MouseButton.Left ? "LEFT" : "RIGHT";
            SendCommand($"MOUSE_UP|{btn}");
        }

        private void InputOverlay_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            SendCommand($"MOUSE_WHEEL|{e.Delta}");
        }

        // --- УПРАВЛЕНИЕ КЛАВИАТУРОЙ ---
        private void InputOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; return; }
            SendCommand($"KEY_DOWN|{(int)e.Key}");
            e.Handled = true;
        }

        private void InputOverlay_KeyUp(object sender, KeyEventArgs e)
        {
            SendCommand($"KEY_UP|{(int)e.Key}");
            e.Handled = true;
        }

        // --- ПОЛНОЭКРАННЫЙ РЕЖИМ ---
        private void ToggleFullscreen()
        {
            Window win = Window.GetWindow(this);
            if (win == null) return;

            if (!_isFullscreen)
            {
                ConnectionPanel.Visibility = Visibility.Collapsed;
                BackgroundLayer.Visibility = Visibility.Collapsed;
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
                VideoContainer.Margin = new Thickness(15, 0, 15, 15);
                VideoContainer.CornerRadius = new CornerRadius(8);
                win.WindowStyle = WindowStyle.SingleBorderWindow;
                win.WindowState = WindowState.Normal;
                _isFullscreen = false;
            }

            // После изменения размеров нужно обновить AspectRatio видео и положение Popup
            Dispatcher.BeginInvoke(new Action(async () => {
                await Task.Delay(250); // Ждем завершения анимации окна
                ApplyAspectRatio();
                UpdatePopupPosition();
                VideoView_Commands.Focus();
            }), DispatcherPriority.Render);
        }

        private void Disconnect(object sender, RoutedEventArgs e)
        {
            _displayPlayer?.Stop();
            _tcpClient?.Close();
            _stream = null;
            InputPopup.IsOpen = false;
            ConnectBtn.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;
            if (_isFullscreen) ToggleFullscreen();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Disconnect(null, null);
            _displayPlayer?.Dispose();
            _libVLC?.Dispose();
        }
    }
}