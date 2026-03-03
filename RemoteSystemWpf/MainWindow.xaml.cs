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
        MainWindow main;
        public MainWindow(string choice)
        {
            InitializeComponent();
            main = this;
            this.Closing += MainWindow_Closing;
            switch (choice)
            {
                case "1":
                    SwapFrame(new Pages.ServerPage());
                    break;
                case "2":
                    SwapFrame(new Pages.ClientPage());
                    break;
                default:
                    break;
            }

        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(0);
        }
        public void SwapFrame(Page page) {
            frame.Navigate(page);
        }
    }
}
