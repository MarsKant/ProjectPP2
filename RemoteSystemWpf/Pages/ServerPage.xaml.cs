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
        private CancellationTokenSource _cts;

        public ServerPage() => InitializeComponent();

        // --- МЕТОД УБИЙЦА ЗОМБИ ---
        private void KillZombieProcesses()
        {
            string[] engines = { "ffmpeg", "mediamtx" };

            foreach (var name in engines)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(); p.WaitForExit(500); } catch { }
                }
            }
            AddLog("[SYSTEM] Движки очищены.");
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("[SYSTEM] Очистка процессов...");
                KillZombieProcesses();

                // Считываем порт из интерфейса ОДИН РАЗ
                if (!int.TryParse(portBox.Text, out int cmdPort)) cmdPort = 8890;
                int videoPort = 8554;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string mtxPath = Path.Combine(baseDir, "MediaMTX", "mediamtx.exe");
                string ffmpegPath = Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe");

                // 1. MediaMTX
                _rtspServer = new Process { /* ... твои настройки ... */ };
                _rtspServer.StartInfo.FileName = mtxPath;
                _rtspServer.Start();
                Thread.Sleep(1000);

                // 2. FFmpeg
                string args = $"-f gdigrab -framerate 30 -i desktop -c:v libx264 -preset ultrafast -tune zerolatency -b:v 3M -rtsp_transport tcp -f rtsp rtsp://127.0.0.1:{videoPort}/stream";
                _ffmpeg = new Process { /* ... твои настройки ... */ };
                _ffmpeg.StartInfo.FileName = ffmpegPath;
                _ffmpeg.StartInfo.Arguments = args;
                _ffmpeg.Start();

                // 3. Сервер команд (Исправлено)
                _isListening = true;
                _listener = new TcpListener(IPAddress.Any, cmdPort); // Используем cmdPort
                _listener.Start();

                // Запускаем только цикл прослушивания, БЕЗ пересоздания listener
                new Thread(AcceptClientsLoop).Start();

                AddLog($"[OK] Сервер активен. Порт: {cmdPort}");
                Startbtn.IsEnabled = false;
                Stopbtn.IsEnabled = true;
            }
            catch (Exception ex) { AddLog($"[ERROR] {ex.Message}"); }
        }

        // Новый чистый метод для потока
        private void AcceptClientsLoop()
        {
            try
            {
                while (_isListening)
                {
                    // AcceptTcpClient блокирует поток, пока кто-то не подключится
                    var client = _listener.AcceptTcpClient();
                    Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception ex)
            {
                if (_isListening) // Показываем ошибку, только если мы не сами остановили сервер
                    Dispatcher.Invoke(() => AddLog("Сеть остановлена: " + ex.Message));
            }
        }

        private void ListenForClients()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, 8890); // Новый порт
                _listener.Start();

                while (_isListening)
                {
                    var client = _listener.AcceptTcpClient();
                    // Запускаем в Task, чтобы не вешать цикл
                    Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AddLog("Ошибка сети: " + ex.Message));
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                while (_isListening && client.Connected)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        foreach (var cmd in data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            // Выполняем команду
                            ExecuteCommand(cmd);
                        }
                    }
                    catch { break; } // Мягкий выход при дисконнекте
                }
            }
        }

        private void ExecuteCommand(string command)
        {
            try
            {
                string[] p = command.Split('|');
                switch (p[0])
                {
                    case "MOUSE_MOVE": SetCursorPos(int.Parse(p[1]), int.Parse(p[2])); break;
                    case "MOUSE_DOWN": mouse_event(p[1] == "0" ? 0x0002u : 0x0008u, 0, 0, 0, UIntPtr.Zero); break;
                    case "MOUSE_UP": mouse_event(p[1] == "0" ? 0x0004u : 0x0010u, 0, 0, 0, UIntPtr.Zero); break;
                    case "KEY_DOWN": keybd_event((byte)int.Parse(p[1]), 0, 0, UIntPtr.Zero); break;
                    case "KEY_UP": keybd_event((byte)int.Parse(p[1]), 0, 0x0002u, UIntPtr.Zero); break;
                }
            }
            catch { }
        }

        private void Stop()
        {
            _isListening = false;
            _cts?.Cancel();
            _listener?.Stop();
            try { _ffmpeg?.Kill(); _rtspServer?.Kill(); } catch { }
            Dispatcher.Invoke(() => { Startbtn.IsEnabled = true; Stopbtn.IsEnabled = false; });
        }

        private void Stop_Click(object sender, RoutedEventArgs e) => Stop();
        private void Page_Unloaded(object sender, RoutedEventArgs e) => Stop();
        private void AddLog(string m) => Dispatcher.Invoke(() => LogBox.Items.Add(m));

        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
        [DllImport("user32.dll")] static extern void keybd_event(byte b, byte s, uint f, UIntPtr e);
    }
}