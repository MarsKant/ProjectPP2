using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteSystem.Classes
{
    public enum CommandType
    {
        ScreenData = 1,
        MouseMove = 2,
        MouseClick = 3,
        KeyPress = 4,
        MouseWheel = 5,
        ConnectionRequest = 6,
        ConnectionAccept = 7,
        Disconnect = 8
    }
}
