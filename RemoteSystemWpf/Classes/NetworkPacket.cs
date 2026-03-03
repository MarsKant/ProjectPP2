using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteSystem.Classes
{
    public class NetworkPacket
    {
        public CommandType Command { get; set; }
        public byte[] Data { get; set; }

        public NetworkPacket(CommandType cmd, byte[] data)
        {
            Command = cmd;
            Data = data;
        }

        public byte[] Serialize()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] cmdBytes = BitConverter.GetBytes((int)Command);
                byte[] lenBytes = BitConverter.GetBytes(Data.Length);

                ms.Write(cmdBytes, 0, 4);
                ms.Write(lenBytes, 0, 4);
                ms.Write(Data, 0, Data.Length);

                return ms.ToArray();
            }
        }

        public static NetworkPacket Deserialize(byte[] buffer)
        {
            if (buffer.Length < 8) return null;

            CommandType cmd = (CommandType)BitConverter.ToInt32(buffer, 0);
            int dataLen = BitConverter.ToInt32(buffer, 4);

            if (buffer.Length < 8 + dataLen) return null;

            byte[] data = new byte[dataLen];
            Array.Copy(buffer, 8, data, 0, dataLen);

            return new NetworkPacket(cmd, data);
        }
    }
}
