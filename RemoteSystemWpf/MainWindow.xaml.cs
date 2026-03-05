using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace RemoteSystemWpf
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

        }
        public void SwapFrame(Page page) {
            frame.Navigate(page);
        }

        private void Client(object sender, RoutedEventArgs e)
        {
            HideUI();
            SwapFrame(new Pages.ClientPage());
        }

        private void Server(object sender, RoutedEventArgs e)
        {
            HideUI();
            SwapFrame(new Pages.ServerPage());
        }
        private void HideUI()
        {
            Choicer.Visibility = Visibility.Hidden;
            MainLabel.Visibility = Visibility.Hidden;
        }

    }
}
