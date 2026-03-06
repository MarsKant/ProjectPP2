using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
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
        private DispatcherTimer _focusTimer;
        private int _lastX = -1;
        private int _lastY = -1;
        private int _lastSentX = -1;
        private int _lastSentY = -1;
        private double _serverWidth = 1920;
        private double _serverHeight = 1080;

        // Для полноэкранного режима
        private bool _isFullScreen = false;
        private Window _parentWindow;
        private WindowStyle _originalWindowStyle;
        private ResizeMode _originalResizeMode;
        private WindowState _originalWindowState;

        public ClientPage()
        {
            InitializeComponent();
            InitializeVLC();

            _moveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _moveTimer.Tick += (s, e) => SendMousePosition();

            _focusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // Чаще проверяем фокус
            };
            _focusTimer.Tick += (s, e) =>
            {
                if (_isConnected && !InputOverlay.IsFocused)
                {
                    InputOverlay.Focus();
                    Keyboard.Focus(InputOverlay);
                    System.Diagnostics.Debug.WriteLine("Фокус возвращен на оверлей");
                }
            };

            this.Loaded += (s, e) =>
            {
                _parentWindow = Window.GetWindow(this);
            };

            this.SizeChanged += (s, e) =>
            {
                if (_isConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"Размер окна изменен: {e.NewSize.Width}x{e.NewSize.Height}");
                }
            };

            // Глобальная обработка клавиш
            this.KeyDown += (s, e) =>
            {
                if (_isFullScreen && e.Key == Key.Escape)
                {
                    ExitFullScreen();
                    e.Handled = true;
                }
                else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    ToggleFullScreen();
                    e.Handled = true;
                }
            };
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
                    "--rtsp-tcp",
                    "--vout=wingdi",
                    "--no-video-title-show",
                    "--mouse-hide-timeout=0",
                    "--no-video-on-top",
                    "--no-embedded-video",
                    "--no-keyboard-events", // Отключаем обработку клавиатуры VLC
                    "--no-mouse-events"     // Отключаем обработку мыши VLC
                };

                _libvlc = new LibVLC(vlcArgs);
                _mediaPlayer = new MediaPlayer(_libvlc);

                if (VideoView != null)
                {
                    VideoView.MediaPlayer = _mediaPlayer;
                    System.Diagnostics.Debug.WriteLine("✅ MediaPlayer привязан к VideoView");
                }

                _mediaPlayer.Playing += (s, e) =>
                    Dispatcher.Invoke(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("▶️ Воспроизведение начато");
                        _serverWidth = 1920;
                        _serverHeight = 1080;

                        // Принудительно возвращаем фокус
                        InputOverlay.Focus();
                        Keyboard.Focus(InputOverlay);

                        // Автоматически переходим в полноэкранный режим
                        EnterFullScreen();
                    });

                _mediaPlayer.Stopped += (s, e) =>
                    Dispatcher.Invoke(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("⏹️ Воспроизведение остановлено");
                        ExitFullScreen();
                    });

                _mediaPlayer.EncounteredError += (s, e) =>
                    Dispatcher.Invoke(() => System.Diagnostics.Debug.WriteLine("❌ Ошибка воспроизведения"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации VLC: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Подключение к {streamUrl}");

                var media = new Media(_libvlc, new Uri(streamUrl));
                media.AddOption(":network-caching=500");
                media.AddOption(":rtsp-tcp");
                media.AddOption(":live-caching=500");

                _mediaPlayer.Play(media);

                // Подключение к серверу управления
                int inputPort = int.Parse(port) + 1;
                _inputClient = new TcpClient();
                _inputClient.Connect(ip, inputPort);
                _inputStream = _inputClient.GetStream();

                ConnectBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = true;
                IpBox.IsEnabled = false;
                PortBox.IsEnabled = false;
                _isConnected = true;

                // Настраиваем оверлей
                InputOverlay.Focusable = true;
                InputOverlay.Focus();
                Keyboard.Focus(InputOverlay);

                // Захватываем мышь
                InputOverlay.CaptureMouse();

                _moveTimer.Start();
                _focusTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        private void Disconnect(object sender, RoutedEventArgs e)
        {
            try
            {
                _moveTimer.Stop();
                _focusTimer.Stop();
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

                InputOverlay.ReleaseMouseCapture();
                ExitFullScreen();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отключения: {ex.Message}");
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _moveTimer.Stop();
            _focusTimer.Stop();
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libvlc?.Dispose();

            _inputStream?.Close();
            _inputClient?.Close();

            if (VideoView != null)
                VideoView.MediaPlayer = null;

            InputOverlay.ReleaseMouseCapture();
            ExitFullScreen();
        }

        private void ToggleFullScreen()
        {
            if (_isFullScreen)
                ExitFullScreen();
            else
                EnterFullScreen();
        }

        private void EnterFullScreen()
        {
            if (_parentWindow == null || _isFullScreen) return;

            _originalWindowStyle = _parentWindow.WindowStyle;
            _originalResizeMode = _parentWindow.ResizeMode;
            _originalWindowState = _parentWindow.WindowState;

            _parentWindow.WindowStyle = WindowStyle.None;
            _parentWindow.ResizeMode = ResizeMode.NoResize;
            _parentWindow.WindowState = WindowState.Maximized;

            _isFullScreen = true;
            System.Diagnostics.Debug.WriteLine("▶️ Полноэкранный режим включен");
        }

        private void ExitFullScreen()
        {
            if (_parentWindow == null || !_isFullScreen) return;

            _parentWindow.WindowStyle = _originalWindowStyle;
            _parentWindow.ResizeMode = _originalResizeMode;
            _parentWindow.WindowState = _originalWindowState;

            _isFullScreen = false;
            System.Diagnostics.Debug.WriteLine("⏹️ Полноэкранный режим выключен");
        }

        private void InputOverlay_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Оверлей получил фокус");
        }

        private void InputOverlay_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Оверлей потерял фокус");
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
            catch
            {
                Dispatcher.Invoke(() => Disconnect(null, null));
            }
        }

        private void SendMousePosition()
        {
            if (!_isConnected || _lastX == -1 || _lastY == -1) return;

            double overlayWidth = InputOverlay.ActualWidth;
            double overlayHeight = InputOverlay.ActualHeight;

            if (overlayWidth == 0 || overlayHeight == 0) return;

            int serverX = (int)((_lastX / overlayWidth) * _serverWidth);
            int serverY = (int)((_lastY / overlayHeight) * _serverHeight);

            serverX = Math.Max(0, Math.Min((int)_serverWidth - 1, serverX));
            serverY = Math.Max(0, Math.Min((int)_serverHeight - 1, serverY));

            if (_lastSentX != serverX || _lastSentY != serverY)
            {
                SendCommand($"MOUSE_MOVE|{serverX},{serverY}");
                _lastSentX = serverX;
                _lastSentY = serverY;
            }
        }

        private void InputOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isConnected) return;

            var pos = e.GetPosition(InputOverlay);

            if (pos.X >= 0 && pos.X <= InputOverlay.ActualWidth &&
                pos.Y >= 0 && pos.Y <= InputOverlay.ActualHeight)
            {
                _lastX = (int)pos.X;
                _lastY = (int)pos.Y;
            }
        }

        private void InputOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isConnected) return;

            InputOverlay.Focus();
            Keyboard.Focus(InputOverlay);

            uint button = e.ChangedButton == MouseButton.Left ? 0u : 1u;
            SendCommand($"MOUSE_DOWN|{button}");
            e.Handled = true;
        }

        private void InputOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isConnected) return;
            uint button = e.ChangedButton == MouseButton.Left ? 0u : 1u;
            SendCommand($"MOUSE_UP|{button}");
            e.Handled = true;
        }

        private void InputOverlay_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isConnected) return;
            SendCommand($"MOUSE_WHEEL|{e.Delta}");
            e.Handled = true;
        }

        private void InputOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isConnected) return;

            // Проверка комбинации для выхода из полноэкранного режима
            if (_isFullScreen && e.Key == Key.Escape)
            {
                ExitFullScreen();
                e.Handled = true;
                return;
            }

            // Проверка комбинации для переключения полноэкранного режима
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                ToggleFullScreen();
                e.Handled = true;
                return;
            }

            // Игнорируем модификаторы
            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            SendCommand($"KEY_DOWN|{KeyInterop.VirtualKeyFromKey(e.Key)}");
            e.Handled = true;
        }

        private void InputOverlay_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_isConnected) return;

            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            SendCommand($"KEY_UP|{KeyInterop.VirtualKeyFromKey(e.Key)}");
            e.Handled = true;
        }
    }
}