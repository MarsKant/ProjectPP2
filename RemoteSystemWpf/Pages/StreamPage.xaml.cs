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
    public partial class StreamPage : Page
    {
        private LibVLC _libVLC;
        private LibVLCSharp.Shared.MediaPlayer _displayPlayer;
        private TcpClient _tcpClient;
        private NetworkStream _stream;

        private string _serverIp;
        private string _serverPort;

        private double _serverWidth = 1920;
        private double _serverHeight = 1080;

        public StreamPage(string ip, string port)
        {
            InitializeComponent();
            _serverIp = ip;
            _serverPort = port;

            _libVLC = new LibVLC();
            _displayPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            VideoView_Display.MediaPlayer = _displayPlayer;

            this.KeyDown += InputOverlay_KeyDown;
            this.KeyUp += InputOverlay_KeyUp;

            this.SizeChanged += (s, e) =>
            {
                ApplyAspectRatio();
                UpdatePopupPosition();
            };

            // Автоматически запускаем подключение после прогрузки страницы
            this.Loaded += async (s, e) => await ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_serverIp, 8890);
                _stream = _tcpClient.GetStream();

                byte[] buffer = new byte[1024];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                if (response.StartsWith("SIZE"))
                {
                    string[] parts = response.Split('|');
                    _serverWidth = double.Parse(parts[1]);
                    _serverHeight = double.Parse(parts[2]);
                }

                string rtspUrl = $"rtsp://{_serverIp}:{_serverPort}/stream";
                var media = new Media(_libVLC, new Uri(rtspUrl));
                media.AddOption(":no-mouse-events");
                media.AddOption(":no-keyboard-events");
                media.AddOption(":rtsp-transport=tcp");
                media.AddOption(":network-caching=100");

                _displayPlayer.Play(media);
                ApplyAspectRatio();

                InputPopup.IsOpen = true;

                await Task.Delay(500);
                UpdatePopupPosition();
                VideoView_Commands.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
                ReturnToClientPage();
            }
        }

        private void ApplyAspectRatio()
        {
            if (_displayPlayer != null && VideoView_Display.ActualWidth > 0)
            {
                _displayPlayer.AspectRatio = $"{(int)VideoView_Display.ActualWidth}:{(int)VideoView_Display.ActualHeight}";
            }
        }

        private void UpdatePopupPosition()
        {
            if (!InputPopup.IsOpen) return;

            InputPopup.Width = VideoView_Display.ActualWidth;
            InputPopup.Height = VideoView_Display.ActualHeight;

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

            int finalX = (int)(p.X * _serverWidth / VideoView_Commands.ActualWidth);
            int finalY = (int)(p.Y * _serverHeight / VideoView_Commands.ActualHeight);

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

        private void InputOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            // Вместо проблемного FullScreen отключаемся по Escape
            if (e.Key == Key.Escape)
            {
                Disconnect_Click(null, null);
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

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            ReturnToClientPage();
        }

        private void ReturnToClientPage()
        {
            Cleanup();
            MainWindow.main.SwapFrame(new ClientPage());
        }

        private void Cleanup()
        {
            _displayPlayer?.Stop();
            _tcpClient?.Close();
            _stream = null;
            InputPopup.IsOpen = false;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
            _displayPlayer?.Dispose();
            _libVLC?.Dispose();
        }
    }
}