using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RemoteSystem.Classes
{
    public class InputController
    {
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const int MOUSEEVENTF_LEFTDOWN = 0x02;
        const int MOUSEEVENTF_LEFTUP = 0x04;
        const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        const int MOUSEEVENTF_RIGHTUP = 0x10;
        const int MOUSEEVENTF_MIDDLEDOWN = 0x20;
        const int MOUSEEVENTF_MIDDLEUP = 0x40;
        const int MOUSEEVENTF_WHEEL = 0x0800;

        const int WHEEL_DELTA = 120;

        const int KEYEVENTF_KEYDOWN = 0x0;
        const int KEYEVENTF_KEYUP = 0x2;

        public static void MoveMouse(int x, int y)
        {
            SetCursorPos(x, y);
        }

        public static void LeftClick()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            Console.WriteLine("Сервер: левый клик");
        }

        public static void RightClick()
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
            Console.WriteLine("Сервер: правый клик");
        }

        public static void MiddleClick()
        {
            mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
            Console.WriteLine("Сервер: средний клик");
        }

        public static void ScrollWheel(int delta)
        {
            try
            {
                mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
                Console.WriteLine($"Сервер: прокрутка {(delta > 0 ? "вверх" : "вниз")} {delta}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка прокрутки: " + ex.Message);
            }
        }

        public static void SendKey(byte keyCode, bool isDown)
        {
            try
            {
                if (isDown)
                    keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                else
                    keybd_event(keyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                Console.WriteLine($"Сервер: клавиша {(isDown ? "нажата" : "отпущена")}: {keyCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка отправки клавиши: " + ex.Message);
            }
        }
    }
}
