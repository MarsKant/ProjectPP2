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

                // ЖЕСТКО РАЗДЕЛЯЕМ ПОРТЫ:
                int cmdPort = 8890;   // Команды всегда идут сюда
                int videoPort = 8554; // Видео всегда идет отсюда

                _tcpClient = new TcpClient();

                // 1. Подключаем управление (к порту 8890)
                var connectTask = _tcpClient.ConnectAsync(ip, cmdPort);

                if (await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask)
                {
                    await connectTask;
                    _stream = _tcpClient.GetStream();

                    ConnectBtn.IsEnabled = false;
                    DisconnectBtn.IsEnabled = true;

                    // 2. Ждем 1.5 секунды, пока FFmpeg на сервере "разогреется"
                    await Task.Delay(1500);

                    // 3. Запуск видео потока (с порта 8554)
                    string rtspUrl = $"rtsp://{ip}:{videoPort}/stream";
                    var media = new Media(_libVLC, new Uri(rtspUrl));
                    media.AddOption(":rtsp-transport=tcp");
                    media.AddOption(":network-caching=200");
                    _mediaPlayer.Play(media);

                    // 4. Возвращаем фокус нашему прозрачному слою, 
                    // иначе клавиатура не будет работать (VLC забирает фокус себе)
                    InputOverlay.Focus();
                }
                else
                {
                    MessageBox.Show($"Сервер управления (порт {cmdPort}) не отвечает.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка подключения: " + ex.Message);
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
            if (_stream == null) return;

            // Получаем позицию мыши относительно элемента InputOverlay
            Point position = e.GetPosition(InputOverlay);

            // ВАЖНО: Вместо передачи экранных координат окна, 
            // мы вычисляем их относительно РЕАЛЬНОГО разрешения сервера.
            // Если на сервере экран 1920x1080, укажи эти цифры:
            double serverWidth = 1920;
            double serverHeight = 1080;

            // Вычисляем коэффициент (на сколько нужно умножить координату)
            double xCoord = (position.X / InputOverlay.ActualWidth) * serverWidth;
            double yCoord = (position.Y / InputOverlay.ActualHeight) * serverHeight;

            // Ограничиваем, чтобы не вылетало за границы
            int finalX = (int)Math.Max(0, Math.Min(serverWidth, xCoord));
            int finalY = (int)Math.Max(0, Math.Min(serverHeight, yCoord));

            SendCommand($"MOUSE_MOVE|{finalX}|{finalY}");
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