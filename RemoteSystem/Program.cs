using RemoteSystem.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RemoteAccessSystem
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Console.WriteLine("Выберите режим:");
            Console.WriteLine("1 - Сервер (компьютер к которому подключаются)");
            Console.WriteLine("2 - Клиент (компьютер с которого подключаются)");

            string choice = Console.ReadLine().Trim();
            while (choice != "1" && choice != "2")
            {
                Console.WriteLine("Неверный выбор! Введите 1 или 2:");
                choice = Console.ReadLine().Trim();
            }

            ShowWindow(GetConsoleWindow(), 0);

            var app = new System.Windows.Application();
            var mainWindow = new RemoteSystemWpf.MainWindow(choice);
            mainWindow.WindowState = System.Windows.WindowState.Maximized;

            app.ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
            app.Run(mainWindow);
        }
    }
}