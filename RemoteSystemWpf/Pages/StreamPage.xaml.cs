using RemoteSystemWpf.Classes;
using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vlc.DotNet.Wpf;

namespace RemoteSystemWpf.Pages
{
    public partial class StreamPage : Page
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;

        private string _serverIp;
        private string _serverPort;

        private double _serverWidth = 1920;
        private double _serverHeight = 1080;

        public StreamPage(string input)
        {
            InitializeComponent();
            _serverIp = DecodeID(input);
            _serverPort = "8890";

            this.PreviewKeyDown += InputOverlay_KeyDown;
            this.PreviewKeyUp += InputOverlay_KeyUp;

            InitializeVlc();

            this.Loaded += async (s, e) =>
            {
                await ConnectAsync();
                VideoView_Commands.Focus();
            };
        }

        private void InitializeVlc()
        {
            try
            {
                var vlcLibDirectory = new DirectoryInfo(PathsConfig.VlcLibDir);

                if (!vlcLibDirectory.Exists)
                {
                    throw new DirectoryNotFoundException($"VLC библиотеки не найдены в системе: {vlcLibDirectory.FullName}");
                }

                VlcPlayer.SourceProvider.CreatePlayer(vlcLibDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации VLC: {ex.Message}");
            }
        }

        private async Task ConnectAsync()
        {
            int attempts = 3;
            while (attempts > 0)
            {
                try
                {
                    _tcpClient = new TcpClient();
                    var connectTask = _tcpClient.ConnectAsync(_serverIp, int.Parse(_serverPort));
                    if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                    {
                        throw new Exception("Превышено время ожидания подключения.");
                    }

                    _stream = _tcpClient.GetStream();
                    _stream.ReadTimeout = 5000;

                    byte[] buffer = new byte[1024];
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    if (response.StartsWith("SIZE"))
                    {
                        string[] parts = response.Split('|');
                        _serverWidth = double.Parse(parts[1]);
                        _serverHeight = double.Parse(parts[2]);
                    }

                    string rtspUrl = $"rtsp://{_serverIp}:8554/stream";
                    string[] options = new string[]
                    {
                        ":network-caching=300",
                        ":rtsp-transport=tcp",
                        ":no-audio",
                        ":clock-jitter=0",
                        ":rtsp-frame-buffer-size=500000"
                    };

                    VlcPlayer.SourceProvider.MediaPlayer.Play(new Uri(rtspUrl), options);
                    return;
                }
                catch (Exception ex)
                {
                    attempts--;
                    if (attempts == 0)
                    {
                        MessageBox.Show($"Не удалось подключиться к {_serverIp}. Проверьте, запущен ли Tailscale на обоих ПК.\nОшибка: {ex.Message}");
                        ReturnToClientPage();
                    }
                    await Task.Delay(2000);
                }
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


        private void InputOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (VideoView_Commands.ActualWidth <= 0 || VideoView_Commands.ActualHeight <= 0) return;

            Point p = e.GetPosition(VideoView_Commands);

            double serverRatio = _serverWidth / _serverHeight;
            double clientRatio = VideoView_Commands.ActualWidth / VideoView_Commands.ActualHeight;

            double actualVideoWidth = VideoView_Commands.ActualWidth;
            double actualVideoHeight = VideoView_Commands.ActualHeight;
            double offsetX = 0;
            double offsetY = 0;

            if (clientRatio > serverRatio)
            {
                actualVideoWidth = VideoView_Commands.ActualHeight * serverRatio;
                offsetX = (VideoView_Commands.ActualWidth - actualVideoWidth) / 2;
            }
            else
            {
                actualVideoHeight = VideoView_Commands.ActualWidth / serverRatio;
                offsetY = (VideoView_Commands.ActualHeight - actualVideoHeight) / 2;
            }

            double relativeX = (p.X - offsetX) / actualVideoWidth;
            double relativeY = (p.Y - offsetY) / actualVideoHeight;

            int finalX = (int)(relativeX * _serverWidth);
            int finalY = (int)(relativeY * _serverHeight);

            finalX = Math.Max(0, Math.Min((int)_serverWidth, finalX));
            finalY = Math.Max(0, Math.Min((int)_serverHeight, finalY));

            SendCommand($"MOUSE_MOVE|{finalX}|{finalY}");
        }

        private void InputOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VideoView_Commands.CaptureMouse();
            VideoView_Commands.Focus();
            string btn = e.ChangedButton == MouseButton.Left ? "LEFT" : "RIGHT";
            SendCommand($"MOUSE_DOWN|{btn}");
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
            e.Handled = true;
        }


        private void InputOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                Disconnect();
                e.Handled = true;
                return;
            }

            int virtualKey = KeyInterop.VirtualKeyFromKey(e.Key);
            SendCommand($"KEY_DOWN|{virtualKey}");
            e.Handled = true;
        }

        private void InputOverlay_KeyUp(object sender, KeyEventArgs e)
        {
            int virtualKey = KeyInterop.VirtualKeyFromKey(e.Key);
            SendCommand($"KEY_UP|{virtualKey}");
            e.Handled = true;
        }

        private void Disconnect()
        {
            this.PreviewKeyDown -= InputOverlay_KeyDown;
            this.PreviewKeyUp -= InputOverlay_KeyUp;

            ReturnToClientPage();
        }

        private async void ReturnToClientPage()
        {
            await Task.Run(() => Cleanup());

            Dispatcher.Invoke(() =>
            {
                if (MainWindow.main != null)
                    MainWindow.main.SwapFrame(new ClientPage());
            });
        }

        private void Cleanup()
        {
            try
            {
                if (VlcPlayer?.SourceProvider?.MediaPlayer != null)
                {
                    if (VlcPlayer.SourceProvider.MediaPlayer.IsPlaying())
                    {
                        VlcPlayer.SourceProvider.MediaPlayer.Stop();
                    }
                }

                _stream?.Dispose();
                _tcpClient?.Close();
                _stream = null;
                _tcpClient = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка при очистке: " + ex.Message);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
            VlcPlayer?.Dispose();
        }

        private string DecodeID(string id)
        {
            if (id.Contains(".")) return id;

            if (long.TryParse(id, out long numericId))
            {
                string ip = $"{(numericId >> 24) & 0xFF}.{(numericId >> 16) & 0xFF}.{(numericId >> 8) & 0xFF}.{numericId & 0xFF}";
                return ip;
            }
            return id;
        }
    }
}