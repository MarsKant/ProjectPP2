using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RemoteSystem.Classes;

namespace RemoteSystemWpf.Pages
{
    public partial class ClientPage : Page
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private bool isConnected;
        private bool isKeyHandled;

        public ClientPage()
        {
            InitializeComponent();
            this.Loaded += ClientPage_Loaded;
            this.Unloaded += ClientPage_Unloaded;
        }

        private void ClientPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                isConnected = false;
                try
                {
                    stream?.Close();
                    client?.Close();
                }
                catch { }
            }
        }

        private void ClientPage_Loaded(object sender, RoutedEventArgs e)
        {
            InputOverlay.Focus();
        }

        private void ConnectClick(object sender, RoutedEventArgs e)
        {
            string ip = IpBox.Text;
            int port = int.Parse(PortBox.Text);

            try
            {
                client = new TcpClient();
                client.Connect(ip, port);
                client.ReceiveBufferSize = 65536;
                client.SendBufferSize = 65536;
                stream = client.GetStream();
                isConnected = true;

                ConnectBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = true;

                receiveThread = new Thread(ReceiveData);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                InputOverlay.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось подключиться к серверу: " + ex.Message);
            }
        }

        private void DisconnectClick(object sender, RoutedEventArgs e)
        {
            isConnected = false;
            try
            {
                stream?.Close();
                client?.Close();
            }
            catch { }

            ConnectBtn.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;

            ScreenImage.Source = null;
        }

        private void ReceiveData()
        {
            byte[] buffer = new byte[65536];
            List<byte> packetBuffer = new List<byte>();

            while (isConnected)
            {
                try
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            for (int i = 0; i < bytesRead; i++)
                            {
                                packetBuffer.Add(buffer[i]);
                            }

                            while (packetBuffer.Count >= 8)
                            {
                                int dataLen = BitConverter.ToInt32(packetBuffer.ToArray(), 4);
                                int totalPacketSize = 8 + dataLen;

                                if (packetBuffer.Count >= totalPacketSize)
                                {
                                    byte[] packetData = packetBuffer.GetRange(0, totalPacketSize).ToArray();
                                    packetBuffer.RemoveRange(0, totalPacketSize);

                                    NetworkPacket packet = NetworkPacket.Deserialize(packetData);
                                    if (packet != null && packet.Command == CommandType.ScreenData)
                                    {
                                        ProcessScreenData(packet.Data);
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                    Thread.Sleep(10);
                }
                catch
                {
                    break;
                }
            }
        }

        private void ProcessScreenData(byte[] imageData)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(imageData))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    Dispatcher.Invoke(() =>
                    {
                        ScreenImage.Source = bitmap;
                    });
                }
            }
            catch { }
        }

        private void ScreenImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                var position = e.GetPosition(ScreenImage);

                if (ScreenImage.Source != null)
                {
                    double scaleX = ScreenImage.Source.Width / ScreenImage.ActualWidth;
                    double scaleY = ScreenImage.Source.Height / ScreenImage.ActualHeight;

                    int remoteX = (int)(position.X * scaleX);
                    int remoteY = (int)(position.Y * scaleY);

                    remoteX = Math.Max(0, Math.Min((int)ScreenImage.Source.Width - 1, remoteX));
                    remoteY = Math.Max(0, Math.Min((int)ScreenImage.Source.Height - 1, remoteY));

                    byte[] data = new byte[8];
                    BitConverter.GetBytes(remoteX).CopyTo(data, 0);
                    BitConverter.GetBytes(remoteY).CopyTo(data, 4);

                    SendCommand(CommandType.MouseMove, data);
                }
            }
            catch { }
        }

        private void ScreenImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isConnected) return;

            byte button = 0;
            if (e.ChangedButton == MouseButton.Left)
                button = 1;
            else if (e.ChangedButton == MouseButton.Right)
                button = 2;
            else if (e.ChangedButton == MouseButton.Middle)
                button = 3;
            else
                return;

            SendCommand(CommandType.MouseClick, new byte[] { button });
            InputOverlay.Focus();
        }

        private void ScreenImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!isConnected) return;

            byte[] data = new byte[4];
            BitConverter.GetBytes(e.Delta).CopyTo(data, 0);
            SendCommand(CommandType.MouseWheel, data);
        }

        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
            if (isConnected && !isKeyHandled)
            {
                isKeyHandled = true;
                SendKeyPress((byte)KeyInterop.VirtualKeyFromKey(e.Key), true);
                e.Handled = true;
            }
        }

        private void Page_KeyUp(object sender, KeyEventArgs e)
        {
            if (isConnected)
            {
                isKeyHandled = false;
                SendKeyPress((byte)KeyInterop.VirtualKeyFromKey(e.Key), false);
                e.Handled = true;
            }
        }

        private void SendKeyPress(byte keyCode, bool isDown)
        {
            try
            {
                if (isConnected && stream != null)
                {
                    byte[] data = new byte[] { keyCode, (byte)(isDown ? 1 : 0) };
                    NetworkPacket packet = new NetworkPacket(CommandType.KeyPress, data);
                    byte[] packetData = packet.Serialize();
                    stream.Write(packetData, 0, packetData.Length);
                    stream.Flush();
                }
            }
            catch { }
        }

        private void SendCommand(CommandType command, byte[] data)
        {
            try
            {
                if (isConnected && stream != null)
                {
                    NetworkPacket packet = new NetworkPacket(command, data);
                    byte[] packetData = packet.Serialize();
                    stream.Write(packetData, 0, packetData.Length);
                    stream.Flush();
                }
            }
            catch { }
        }
    }
}