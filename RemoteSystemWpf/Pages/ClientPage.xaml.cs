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
            string port = PortBox.Text.Trim();

            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
            {
                MessageBox.Show("Заполните IP и Порт");
                return;
            }

            // Переходим на страницу трансляции, передавая IP и порт
            MainWindow.main.SwapFrame(new StreamPage(ip, port));
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.SwapFrame(new SelectionPage());
        }
    }
}