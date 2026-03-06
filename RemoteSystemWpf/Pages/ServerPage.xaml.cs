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
                Stop(); // Сброс локальных переменных

                if (!int.TryParse(portBox.Text, out int cmdPort)) cmdPort = 8889;
                int videoPort = 8554; // MediaMTX по умолчанию использует 8554

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string mtxPath = Path.Combine(baseDir, "MediaMTX", "mediamtx.exe");
                string ffmpegPath = Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe");

                // 1. Запуск MediaMTX
                AddLog("[DEBUG] Запуск MediaMTX...");
                _rtspServer = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = mtxPath,
                        WorkingDirectory = Path.GetDirectoryName(mtxPath),
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                _rtspServer.Start();
                Thread.Sleep(2000); // Ждем инициализации сети

                // 2. Запуск FFmpeg
                AddLog("[DEBUG] Запуск FFmpeg...");
                // Параметр -rtsp_transport tcp обязателен для стабильности
                string args = $"-f gdigrab -framerate 30 -i desktop -c:v libx264 -preset ultrafast -tune zerolatency -b:v 3M -rtsp_transport tcp -f rtsp rtsp://127.0.0.1:{videoPort}/stream";
                _ffmpeg = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = args,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                _ffmpeg.Start();

                // 3. Запуск сервера команд
                _isListening = true;
                _listener = new TcpListener(IPAddress.Any, cmdPort);
                _listener.Start();

                _cts = new CancellationTokenSource();
                new Thread(ListenForClients).Start();

                AddLog($"[OK] Сервер активен. Команды: {cmdPort}, Видео: {videoPort}");
                Startbtn.IsEnabled = false;
                Stopbtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                AddLog($"[ERROR] {ex.Message}");
                KillZombieProcesses();
            }
        }

        private void ListenForClients()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, 8889); // Жестко задай порт для теста
                _listener.Start();
                AddLog("[OK] Сервер команд запущен на порту 8889");

                while (_isListening)
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    AddLog("[NET] Клиент управления подключен!");
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.IsBackground = true; // Важно, чтобы поток не вешал приложение
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                AddLog($"[КРИТ] Ошибка сервера команд: {ex.Message}");
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (_isListening && client.Connected)
                {
                    try
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;
                        ExecuteCommand(line);
                    }
                    catch { break; }
                }
            }
            AddLog("[NET] Клиент отключен");
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