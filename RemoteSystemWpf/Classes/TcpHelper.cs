using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteSystem.Classes
{
    public class TcpHelper
    {
        private TcpClient client;
        private NetworkStream stream;
        private byte[] buffer = new byte[65536];
        private List<byte> receivedData = new List<byte>();

        public bool Connected { get; private set; }

        public bool Connect(string ip, int port)
        {
            try
            {
                client = new TcpClient();
                client.Connect(ip, port);
                client.ReceiveBufferSize = 65536;
                client.SendBufferSize = 65536;
                stream = client.GetStream();
                Connected = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка подключения: " + ex.Message);
                return false;
            }
        }

        public void Send(NetworkPacket packet)
        {
            if (!Connected) return;
            try
            {
                byte[] data = packet.Serialize();
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка отправки: " + ex.Message);
            }
        }

        public NetworkPacket Receive()
        {
            if (!Connected || !stream.DataAvailable) return null;

            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    byte[] received = new byte[bytesRead];
                    Array.Copy(buffer, 0, received, 0, bytesRead);
                    return NetworkPacket.Deserialize(received);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка приема: " + ex.Message);
            }
            return null;
        }

        public void Disconnect()
        {
            Connected = false;
            try
            {
                stream?.Close();
                client?.Close();
            }
            catch { }
        }
    }
}
