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
                AddLog("[SYSTEM] Очистка процессов через App...");
                App.KillZombieProcesses();

                if (!int.TryParse(portBox.Text, out int cmdPort)) cmdPort = 8890;
                int videoPort = 8554;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string mtxDir = Path.Combine(baseDir, "MediaMTX");
                string mtxPath = Path.Combine(mtxDir, "mediamtx.exe");
                string ffmpegPath = Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe");

                Directory.CreateDirectory(mtxDir);
                string mtxConfig = "paths:\n  all:\n    source: publisher\n" +
                                   "readBufferCount: 2048\n" +
                                   "protocols: [tcp]\n" +
                                   "rtspAddress: :8554\n";
                File.WriteAllText(Path.Combine(mtxDir, "mediamtx.yml"), mtxConfig);

                _rtspServer = new Process();
                _rtspServer.StartInfo.FileName = mtxPath;
                _rtspServer.StartInfo.WorkingDirectory = mtxDir;
                _rtspServer.StartInfo.CreateNoWindow = true;
                _rtspServer.StartInfo.UseShellExecute = false;
                _rtspServer.Start();

                Thread.Sleep(1000);

                string args = $"-f gdigrab -framerate 30 -i desktop " +
                              $"-c:v libx264 -preset ultrafast -tune zerolatency " +
                              $"-b:v 2500k -maxrate 2500k -bufsize 5000k -g 30 " +
                              $"-pix_fmt yuv420p -max_muxing_queue_size 1024 " +
                              $"-f rtsp -rtsp_transport tcp rtsp://127.0.0.1:{videoPort}/stream";

                _ffmpeg = new Process();
                _ffmpeg.StartInfo.FileName = ffmpegPath;
                _ffmpeg.StartInfo.Arguments = args;
                _ffmpeg.StartInfo.CreateNoWindow = true;
                _ffmpeg.StartInfo.UseShellExecute = false;
                _ffmpeg.Start();

                // 3. Сервер команд
                _isListening = true;
                _listener = new TcpListener(IPAddress.Any, cmdPort);
                _listener.Start();

                new Thread(AcceptClientsLoop).Start();

                AddLog($"[OK] Сервер активен. Порт: {cmdPort}");
                Startbtn.IsEnabled = false;
                Stopbtn.IsEnabled = true;
            }
            catch (Exception ex) { AddLog($"[ERROR] {ex.Message}"); }
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
            catch { if (_isListening) Dispatcher.Invoke(() => AddLog("Сеть остановлена.")); }
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
                    AddLog($"[NET] Клиент подключен.");
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
                        string[] commands = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var cmd in commands) ExecuteCommand(cmd);
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
                    case "MOUSE_MOVE":
                        SetCursorPos(int.Parse(p[1]), int.Parse(p[2]));
                        break;
                    case "MOUSE_DOWN":
                        uint downFlag = (p[1] == "LEFT" || p[1] == "0") ? 0x0002u : 0x0008u;
                        mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                        break;
                    case "MOUSE_UP":
                        uint upFlag = (p[1] == "LEFT" || p[1] == "0") ? 0x0004u : 0x0010u;
                        mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                        break;
                    case "MOUSE_WHEEL":
                        int wheelDelta = int.Parse(p[1]);
                        mouse_event(0x0800, 0, 0, (uint)wheelDelta, UIntPtr.Zero);
                        break;
                    case "KEY_DOWN":
                        keybd_event((byte)int.Parse(p[1]), 0, 0, UIntPtr.Zero);
                        break;
                    case "KEY_UP":
                        keybd_event((byte)int.Parse(p[1]), 0, 0x0002u, UIntPtr.Zero);
                        break;
                }
            }
            catch { }
        }

        private void Stop()
        {
            _isListening = false;
            _listener?.Stop();
            try
            {
                if (_ffmpeg != null && !_ffmpeg.HasExited) _ffmpeg.Kill();
                if (_rtspServer != null && !_rtspServer.HasExited) _rtspServer.Kill();
            }
            catch { }

            App.KillZombieProcesses();

            Dispatcher.Invoke(() => {
                Startbtn.IsEnabled = true;
                Stopbtn.IsEnabled = false;
                AddLog("[SYSTEM] Сервер остановлен.");
            });
        }

        private void Stop_Click(object sender, RoutedEventArgs e) => Stop();
        private void Page_Unloaded(object sender, RoutedEventArgs e) => Stop();
        private void AddLog(string m) => Dispatcher.Invoke(() => LogBox.Items.Add(m));

        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
        [DllImport("user32.dll")] static extern void keybd_event(byte b, byte s, uint f, UIntPtr e);
    }
}