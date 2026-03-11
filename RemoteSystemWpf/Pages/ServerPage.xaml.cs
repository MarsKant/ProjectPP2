using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using RemoteSystemWpf.Classes;

namespace RemoteSystemWpf.Pages
{
    public partial class ServerPage : Page
    {
        private Process _rtspServer;
        private Process _ffmpeg;
        private TcpListener _listener;
        private bool _isListening;

        public ServerPage() => InitializeComponent();

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.KillZombieProcesses();
                AddLog("Подготовка среды...", Brushes.LightGreen);

                int cmdPort = 8890;
                int videoPort = 8554;

                var host = Dns.GetHostEntry(Dns.GetHostName());
                var tailscaleIp = host.AddressList.FirstOrDefault(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork &&
                    ip.ToString().StartsWith("100."));

                var ipAddress = tailscaleIp ?? host.AddressList.FirstOrDefault(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip));

                if (ipAddress == null)
                {
                    AddLog("Ошибка: Сетевой интерфейс не найден.", Brushes.OrangeRed);
                    return;
                }

                string ipAddressString = ipAddress.ToString();
                string mtxDir = PathsConfig.MediaMTXDir;
                string mtxPath = PathsConfig.MediaMTXExe;
                string ffmpegPath = PathsConfig.FFmpegExe;

                Directory.CreateDirectory(mtxDir);
                string mtxConfig = "paths:\n  all:\n    source: publisher\n" +
                                   "readBufferCount: 2048\n" +
                                   "protocols: [tcp]\n" +
                                   $"rtspAddress: :{videoPort}\n";
                File.WriteAllText(Path.Combine(mtxDir, "mediamtx.yml"), mtxConfig);

                _rtspServer = new Process();
                _rtspServer.StartInfo.FileName = mtxPath;
                _rtspServer.StartInfo.WorkingDirectory = mtxDir;
                _rtspServer.StartInfo.CreateNoWindow = true;
                _rtspServer.StartInfo.UseShellExecute = false;
                _rtspServer.Start();

                Thread.Sleep(1500);

                string args = $"-f gdigrab -framerate 30 -i desktop " +
                              $"-c:v libx264 -preset ultrafast -tune zerolatency " +
                              $"-crf 18 -b:v 12000k -maxrate 15000k -bufsize 3000k " +
                              $"-g 30 -pix_fmt yuv420p " +
                              $"-f rtsp -rtsp_transport tcp rtsp://127.0.0.1:{videoPort}/stream";

                _ffmpeg = new Process();
                _ffmpeg.StartInfo.FileName = ffmpegPath;
                _ffmpeg.StartInfo.Arguments = args;
                _ffmpeg.StartInfo.CreateNoWindow = true;
                _ffmpeg.StartInfo.UseShellExecute = false;
                _ffmpeg.Start();

                _isListening = true;
                _listener = new TcpListener(IPAddress.Any, cmdPort);
                _listener.Start();

                new Thread(AcceptClientsLoop).Start();

                AddLog($"Сервер запущен (Tailscale: {(tailscaleIp != null ? "Да" : "Нет")})", Brushes.LightGreen);
                string accessID = GenerateID(ipAddressString);
                AddLog($"Ваш ID для подключения:", Brushes.LightGreen);
                AddLog($"{accessID}", Brushes.LightGreen);

                Startbtn.IsEnabled = false;
                Stopbtn.IsEnabled = true;
            }
            catch (Exception ex) { AddLog($"Ошибка: {ex.Message}", Brushes.OrangeRed); }
        }

        private void AcceptClientsLoop()
        {
            try
            {
                while (_isListening)
                {
                    var client = _listener.AcceptTcpClient();
                    Task.Run(() => HandleClient(client));
                }
            }
            catch { if (_isListening) Dispatcher.Invoke(() => AddLog("Сеть остановлена.", Brushes.OrangeRed)); }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                    int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
                    string sizeMsg = $"SIZE|{screenWidth}|{screenHeight}";
                    byte[] sizeData = Encoding.UTF8.GetBytes(sizeMsg);
                    stream.Write(sizeData, 0, sizeData.Length);
                    AddLog($"Клиент подключен.", Brushes.LightGreen);
                }
                catch { return; }

                byte[] buffer = new byte[2048];
                while (_isListening && client.Connected)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;
                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        foreach (var cmd in data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) ExecuteCommand(cmd);
                    }
                    catch { break; }
                }
            }
        }

        private void ExecuteCommand(string command)
        {
            try
            {
                string[] p = command.Split('|');
                if (p.Length < 2) return;
                switch (p[0])
                {
                    case "MOUSE_MOVE": SetCursorPos(int.Parse(p[1]), int.Parse(p[2])); break;
                    case "MOUSE_DOWN": mouse_event((p[1] == "LEFT" || p[1] == "0") ? 0x0002u : 0x0008u, 0, 0, 0, UIntPtr.Zero); break;
                    case "MOUSE_UP": mouse_event((p[1] == "LEFT" || p[1] == "0") ? 0x0004u : 0x0010u, 0, 0, 0, UIntPtr.Zero); break;
                    case "MOUSE_WHEEL": mouse_event(0x0800, 0, 0, (uint)int.Parse(p[1]), UIntPtr.Zero); break;
                    case "KEY_DOWN": keybd_event((byte)int.Parse(p[1]), 0, 0, UIntPtr.Zero); break;
                    case "KEY_UP": keybd_event((byte)int.Parse(p[1]), 0, 0x0002u, UIntPtr.Zero); break;
                }
            }
            catch { }
        }

        private void Stop()
        {
            _isListening = false;
            _listener?.Stop();
            if (_ffmpeg != null && !_ffmpeg.HasExited) _ffmpeg.Kill();
            if (_rtspServer != null && !_rtspServer.HasExited) _rtspServer.Kill();
            App.KillZombieProcesses();
            Dispatcher.Invoke(() => {
                Startbtn.IsEnabled = true;
                Stopbtn.IsEnabled = false;
                AddLog("Сервер остановлен.", Brushes.OrangeRed);
            });
        }

        private void Stop_Click(object sender, RoutedEventArgs e) => Stop();
        private void Page_Unloaded(object sender, RoutedEventArgs e) => Stop();

        private void AddLog(string m, Brush f = null)
        {
            Dispatcher.Invoke(() => {
                var item = new ListBoxItem
                {
                    Content = $"[{DateTime.Now:HH:mm:ss}] {m}",
                    Foreground = f ?? Brushes.LightGreen
                };
                LogBox.Items.Add(item);
                LogBox.ScrollIntoView(item);
            });
        }

        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
        [DllImport("user32.dll")] static extern void keybd_event(byte b, byte s, uint f, UIntPtr e);

        private void Back_Click(object sender, RoutedEventArgs e) => MainWindow.main.SwapFrame(new SelectionPage());

        private string GenerateID(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length != 4) return "000000000";

            long id = (long.Parse(parts[0]) << 24) |
                      (long.Parse(parts[1]) << 16) |
                      (long.Parse(parts[2]) << 8) |
                       long.Parse(parts[3]);

            return id.ToString().PadLeft(9, '0');
        }
    }
}