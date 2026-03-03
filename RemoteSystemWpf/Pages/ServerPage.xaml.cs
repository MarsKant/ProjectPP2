using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using RemoteSystem.Classes;
using RemoteSystemWpf.Classes;

namespace RemoteSystemWpf.Pages
{
    /// <summary>
    /// Логика взаимодействия для ServerPage.xaml
    /// </summary>
    public partial class ServerPage : Page
    {
        private TcpListener listener;
        private TcpClient currentClient;
        private NetworkStream clientStream;
        private Thread serverThread;
        private Thread screenThread;
        private bool isRunning;
        public ServerPage()
        {
            InitializeComponent();
            this.Unloaded += ServerPage_Unloaded;
        }
        private void ServerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                isRunning = false;
                try
                {
                    clientStream?.Close();
                    currentClient?.Close();
                    listener?.Stop();
                }
                catch { }
            }
        }

        private void Start(object sender, RoutedEventArgs e)
        {
            try
            {
                int port = int.Parse(portBox.Text);
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                isRunning = true;

                Log("Сервер запущен на порту " + port);
                Startbtn.IsEnabled = false;
                Stopbtn.IsEnabled = true;
                portBox.IsEnabled = false;

                serverThread = new Thread(AcceptClients);
                serverThread.IsBackground = true;
                serverThread.Start();
            }
            catch (Exception ex)
            {
                Log("Ошибка: " + ex.Message);
            }
        }

        private void Stop(object sender, RoutedEventArgs e)
        {
            isRunning = false;
            try
            {
                clientStream?.Close();
                currentClient?.Close();
                listener?.Stop();
            }
            catch { }

            Log("Сервер остановлен");
            Startbtn.IsEnabled = true;
            Stopbtn.IsEnabled = false;
            portBox.IsEnabled = true;
        }
        private void Log(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Log(message));
                return;
            }

            LogBox.Items.Add(DateTime.Now.ToString("HH:mm:ss") + " - " + message);
            if (LogBox.Items.Count > 0)
                LogBox.ScrollIntoView(LogBox.Items[LogBox.Items.Count - 1]);
        }
        private void AcceptClients()
        {
            while (isRunning)
            {
                try
                {
                    currentClient = listener.AcceptTcpClient();
                    currentClient.ReceiveBufferSize = 65536;
                    currentClient.SendBufferSize = 65536;
                    clientStream = currentClient.GetStream();

                    Log("Клиент подключился! IP: " + ((IPEndPoint)currentClient.Client.RemoteEndPoint).Address.ToString());

                    Thread commandThread = new Thread(HandleClientCommands);
                    commandThread.IsBackground = true;
                    commandThread.Start();

                    screenThread = new Thread(SendScreen);
                    screenThread.IsBackground = true;
                    screenThread.Start();
                }
                catch (Exception ex)
                {
                    if (isRunning)
                        Log("Ошибка подключения клиента: " + ex.Message);
                }
            }
        }
        private void HandleClientCommands()
        {
            byte[] buffer = new byte[4096];

            while (isRunning && currentClient != null && currentClient.Connected)
            {
                try
                {
                    if (clientStream.DataAvailable)
                    {
                        int bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            byte[] data = new byte[bytesRead];
                            Array.Copy(buffer, 0, data, 0, bytesRead);

                            NetworkPacket packet = NetworkPacket.Deserialize(data);
                            if (packet != null)
                            {
                                ProcessClientCommand(packet);
                            }
                        }
                    }
                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    Log("Ошибка обработки команд: " + ex.Message);
                    break;
                }
            }
        }
        private void ProcessClientCommand(NetworkPacket packet)
        {
            switch (packet.Command)
            {
                case CommandType.MouseMove:
                    if (packet.Data.Length >= 8)
                    {
                        int x = BitConverter.ToInt32(packet.Data, 0);
                        int y = BitConverter.ToInt32(packet.Data, 4);
                        InputController.MoveMouse(x, y);
                    }
                    break;

                case CommandType.MouseClick:
                    if (packet.Data.Length >= 1)
                    {
                        if (packet.Data[0] == 1)
                            InputController.LeftClick();
                        else if (packet.Data[0] == 2)
                            InputController.RightClick();
                        else if (packet.Data[0] == 3)
                            InputController.MiddleClick();
                    }
                    break;

                case CommandType.MouseWheel:
                    if (packet.Data.Length >= 4)
                    {
                        int delta = BitConverter.ToInt32(packet.Data, 0);
                        InputController.ScrollWheel(delta);
                    }
                    break;

                case CommandType.KeyPress:
                    if (packet.Data.Length >= 2)
                    {
                        InputController.SendKey(packet.Data[0], packet.Data[1] == 1);
                    }
                    break;
            }
        }
        private void SendScreen()
        {
            while (isRunning && currentClient != null && currentClient.Connected)
            {
                try
                {
                    byte[] screenData = ScreenCapture.CaptureScreen();
                    if (screenData != null && screenData.Length > 0)
                    {
                        var packet = new NetworkPacket(CommandType.ScreenData, screenData);
                        byte[] packetData = packet.Serialize();
                        clientStream.Write(packetData, 0, packetData.Length);
                        clientStream.Flush();

                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Log("Ошибка отправки экрана: " + ex.Message);
                    break;
                }
            }
        }
    }
}
