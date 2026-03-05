using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using System.Runtime.InteropServices;

namespace RemoteSystemWpf.Pages
{
    public partial class ServerPage : Page
    {
        private Process _rtspServerProcess;
        private Process _ffmpegProcess;
        private TcpListener _inputListener;
        private Thread _inputThread;
        private bool _isListening = false;

        public ServerPage()
        {
            InitializeComponent();
        }

        private void Start_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(portBox.Text, out int port))
                {
                    AddLog("Неверный порт");
                    return;
                }

                // Используем относительные пути
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string serverPath = Path.Combine(baseDir, "MediaMTX", "mediamtx.exe");
                string ffmpegPath = Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe");

                if (!File.Exists(serverPath))
                {
                    AddLog($"MediaMTX не найден по пути: {serverPath}");
                    return;
                }

                if (!File.Exists(ffmpegPath))
                {
                    AddLog($"FFmpeg не найден по пути: {ffmpegPath}");
                    return;
                }

                Stop();

                // Запустить RTSP сервер
                _rtspServerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = serverPath,
                        WorkingDirectory = Path.Combine(baseDir, "MediaMTX"),
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                _rtspServerProcess.Start();

                Thread.Sleep(2000);

                string arguments = $"-f gdigrab -framerate 15 -i desktop -c:v libx264 -preset ultrafast -tune zerolatency -b:v 1M -f rtsp rtsp://localhost:{port}/stream";

                _ffmpegProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                _ffmpegProcess.Start();

                StartInputServer(port + 1);

                AddLog($"Трансляция запущена на порту {port}");
                AddLog($"RTSP: rtsp://{GetLocalIP()}:{port}/stream");
                AddLog($"Управление: {GetLocalIP()}:{port + 1}");

                Startbtn.IsEnabled = false;
                Stopbtn.IsEnabled = true;
                portBox.IsEnabled = false;
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка: {ex.Message}");
                Stop();
            }
        }

        private void Stop_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Stop();
        }

        private void StartInputServer(int port)
        {
            try
            {
                _inputListener = new TcpListener(IPAddress.Any, port);
                _inputListener.Start();
                _isListening = true;

                _inputThread = new Thread(() =>
                {
                    while (_isListening)
                    {
                        try
                        {
                            var client = _inputListener.AcceptTcpClient();
                            var clientThread = new Thread(HandleInputClient);
                            clientThread.Start(client);
                        }
                        catch { }
                    }
                });
                _inputThread.Start();

                AddLog($"Сервер управления запущен на порту {port}");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка запуска сервера управления: {ex.Message}");
            }
        }

        private void HandleInputClient(object obj)
        {
            var client = (TcpClient)obj;
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);

            try
            {
                while (_isListening && client.Connected)
                {
                    string command = reader.ReadLine();
                    if (!string.IsNullOrEmpty(command))
                    {
                        ExecuteCommand(command);
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => AddLog($"Ошибка: {ex.Message}")));
            }
            finally
            {
                client.Close();
            }
        }

        private void ExecuteCommand(string command)
        {
            try
            {
                string[] parts = command.Split('|');
                if (parts.Length < 2) return;

                string cmdType = parts[0];
                string cmdData = parts[1];

                switch (cmdType)
                {
                    case "MOUSE_MOVE":
                        string[] coords = cmdData.Split(',');
                        if (coords.Length == 2)
                        {
                            int x = int.Parse(coords[0]);
                            int y = int.Parse(coords[1]);
                            SetCursorPos(x, y);
                        }
                        break;

                    case "MOUSE_DOWN":
                        uint button = uint.Parse(cmdData);
                        mouse_event(button == 0 ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                        break;

                    case "MOUSE_UP":
                        button = uint.Parse(cmdData);
                        mouse_event(button == 0 ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                        break;

                    case "MOUSE_WHEEL":
                        int delta = int.Parse(cmdData);
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
                        break;

                    case "KEY_DOWN":
                        keybd_event((byte)int.Parse(cmdData), 0, 0, UIntPtr.Zero);
                        break;

                    case "KEY_UP":
                        keybd_event((byte)int.Parse(cmdData), 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void Stop()
        {
            try
            {
                _isListening = false;

                _ffmpegProcess?.Kill();
                _ffmpegProcess?.Dispose();
                _rtspServerProcess?.Kill();
                _rtspServerProcess?.Dispose();

                _inputListener?.Stop();
                _inputThread?.Join(1000);

                AddLog("Трансляция остановлена");

                Startbtn.IsEnabled = true;
                Stopbtn.IsEnabled = false;
                portBox.IsEnabled = true;
            }
            catch { }
        }

        private void Page_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Stop();
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (LogBox != null)
                {
                    LogBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                    LogBox.ScrollIntoView(LogBox.Items[LogBox.Items.Count - 1]);
                }
            });
        }

        private string GetLocalIP()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;
    }
}