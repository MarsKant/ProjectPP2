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

                if (!int.TryParse(portBox.Text, out int cmdPort)) cmdPort = 8890;
                int videoPort = 8554;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string mtxDir = Path.Combine(baseDir, "MediaMTX");
                string mtxPath = Path.Combine(mtxDir, "mediamtx.exe");
                string ffmpegPath = Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe");

                // НОВОЕ: Автоматически создаем конфиг, чтобы разрешить путь /stream
                Directory.CreateDirectory(mtxDir);
                File.WriteAllText(Path.Combine(mtxDir, "mediamtx.yml"), "paths:\n  all:\n");

                // 1. MediaMTX
                _rtspServer = new Process();
                _rtspServer.StartInfo.FileName = mtxPath;
                _rtspServer.StartInfo.WorkingDirectory = mtxDir; // Важно, чтобы он увидел .yml
                _rtspServer.StartInfo.CreateNoWindow = true;
                _rtspServer.StartInfo.UseShellExecute = false;
                _rtspServer.Start();

                Thread.Sleep(1000); // Ждем запуска сервера

                // 2. FFmpeg
                string args = $"-f gdigrab -framerate 30 -i desktop -c:v libx264 -preset ultrafast -tune zerolatency -b:v 3M -rtsp_transport tcp -f rtsp rtsp://127.0.0.1:{videoPort}/stream";
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
                if (p.Length < 2) return;

                switch (p[0])
                {
                    case "MOUSE_MOVE":
                        SetCursorPos(int.Parse(p[1]), int.Parse(p[2]));
                        break;

                    case "MOUSE_DOWN":
                        // Проверяем на "LEFT" или "0" для совместимости
                        uint downFlag = (p[1] == "LEFT" || p[1] == "0") ? 0x0002u : 0x0008u;
                        mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                        break;

                    case "MOUSE_UP":
                        uint upFlag = (p[1] == "LEFT" || p[1] == "0") ? 0x0004u : 0x0010u;
                        mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                        break;

                    case "KEY_DOWN":
                        keybd_event((byte)int.Parse(p[1]), 0, 0, UIntPtr.Zero);
                        break;

                    case "KEY_UP":
                        keybd_event((byte)int.Parse(p[1]), 0, 0x0002u, UIntPtr.Zero);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Добавьте лог, чтобы видеть ошибки парсинга
                Dispatcher.Invoke(() => AddLog("[CMD ERROR] " + ex.Message));
            }
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