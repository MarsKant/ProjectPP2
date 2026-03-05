using LibVLCSharp.Shared;
using System;
using System.Net.Sockets;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;

namespace RemoteSystemWpf.Pages
{
    public partial class ClientPage : Page
    {
        private LibVLC _libvlc;
        private MediaPlayer _mediaPlayer;
        private bool _isConnected = false;
        private TcpClient _inputClient;
        private NetworkStream _inputStream;
        private DispatcherTimer _moveTimer;
        private int _lastX = -1;
        private int _lastY = -1;

        public ClientPage()
        {
            InitializeComponent();
            InitializeVLC();

            _moveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _moveTimer.Tick += (s, e) => SendMousePosition();
        }

        private void InitializeVLC()
        {
            try
            {
                Core.Initialize();

                string[] vlcArgs = new[]
                {
                    "--verbose=2",
                    "--no-audio",
                    "--network-caching=500",
                    "--rtsp-tcp"
                };

                _libvlc = new LibVLC(vlcArgs);
                _mediaPlayer = new MediaPlayer(_libvlc);

                this.Loaded += (s, e) =>
                {
                    if (VideoView != null)
                    {
                        VideoView.MediaPlayer = _mediaPlayer;
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}");
            }
        }

        private void Connect(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = IpBox.Text.Trim();
                string port = PortBox.Text.Trim();

                if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
                {
                    MessageBox.Show("Введите IP и порт");
                    return;
                }

                if (_mediaPlayer == null)
                {
                    MessageBox.Show("Плеер не инициализирован");
                    return;
                }

                _mediaPlayer.Stop();

                string streamUrl = $"rtsp://{ip}:{port}/stream";

                var media = new Media(_libvlc, new Uri(streamUrl));
                media.AddOption(":network-caching=500");
                media.AddOption(":rtsp-tcp");
                media.AddOption(":live-caching=500");

                _mediaPlayer.Play(media);

                int inputPort = int.Parse(port) + 1;
                _inputClient = new TcpClient();
                _inputClient.Connect(ip, inputPort);
                _inputStream = _inputClient.GetStream();

                ConnectBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = true;
                IpBox.IsEnabled = false;
                PortBox.IsEnabled = false;
                _isConnected = true;

                InputOverlay.Focusable = true;
                InputOverlay.Focus();
                Keyboard.Focus(InputOverlay);

                _moveTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e)
        {
            try
            {
                _moveTimer.Stop();
                _mediaPlayer?.Stop();

                _inputStream?.Close();
                _inputClient?.Close();

                ConnectBtn.IsEnabled = true;
                DisconnectBtn.IsEnabled = false;
                IpBox.IsEnabled = true;
                PortBox.IsEnabled = true;
                _isConnected = false;
                _lastX = -1;
                _lastY = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _moveTimer.Stop();
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libvlc?.Dispose();

            _inputStream?.Close();
            _inputClient?.Close();

            if (VideoView != null)
                VideoView.MediaPlayer = null;
        }

        private void SendCommand(string command)
        {
            try
            {
                if (_inputStream != null && _inputStream.CanWrite)
                {
                    byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                    _inputStream.Write(data, 0, data.Length);
                }
            }
            catch {
                Dispatcher.Invoke(() => Disconnect(null, null));
            }
        }

        private void SendMousePosition()
        {
            if (!_isConnected || _lastX == -1 || _lastY == -1) return;
            SendCommand($"MOUSE_MOVE|{_lastX},{_lastY}");
        }

        private void InputOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isConnected) return;
            var pos = e.GetPosition(InputOverlay);
            _lastX = (int)(pos.X * 1920 / InputOverlay.ActualWidth);
            _lastY = (int)(pos.Y * 1080 / InputOverlay.ActualHeight);
        }

        private void InputOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isConnected) return;
            SendCommand($"MOUSE_DOWN|{(e.ChangedButton == MouseButton.Left ? 0 : 1)}");
            InputOverlay.Focus();
        }

        private void InputOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isConnected) return;
            SendCommand($"MOUSE_UP|{(e.ChangedButton == MouseButton.Left ? 0 : 1)}");
        }

        private void InputOverlay_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isConnected) return;
            SendCommand($"MOUSE_WHEEL|{e.Delta}");
        }

        private void InputOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isConnected) return;
            SendCommand($"KEY_DOWN|{KeyInterop.VirtualKeyFromKey(e.Key)}");
            e.Handled = true;
        }

        private void InputOverlay_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_isConnected) return;
            SendCommand($"KEY_UP|{KeyInterop.VirtualKeyFromKey(e.Key)}");
            e.Handled = true;
        }
    }
}