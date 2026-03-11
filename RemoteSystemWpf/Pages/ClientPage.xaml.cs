using System;
using System.Windows;
using System.Windows.Controls;

namespace RemoteSystemWpf.Pages
{
    public partial class ClientPage : Page
    {
        public ClientPage()
        {
            InitializeComponent();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            string ip = IpBox.Text.Trim();
            MainWindow.main.SwapFrame(new StreamPage(ip));
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.SwapFrame(new SelectionPage());
        }
    }
}