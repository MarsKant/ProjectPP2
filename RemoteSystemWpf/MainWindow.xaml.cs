using System;
using System.Windows;
using System.Windows.Controls;

namespace RemoteSystemWpf
{
    public partial class MainWindow : Window
    {
        public static MainWindow main;
        public MainWindow()
        {
            main = this;
            InitializeComponent();
            SwapFrame(new Pages.SelectionPage());
        }

        public void SwapFrame(Page page)
        {
            frame.Navigate(page);
        }


    }
}