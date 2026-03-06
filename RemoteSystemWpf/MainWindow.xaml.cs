using System;
using System.Windows;
using System.Windows.Controls;
using LibVLCSharp.Shared;

namespace RemoteSystemWpf
{
    public partial class MainWindow : Window
    {
        public static MainWindow main;
        public MainWindow()
        {
            main = this;
            Core.Initialize();
            InitializeComponent();
            SwapFrame(new Pages.SelectionPage());
        }

        public void SwapFrame(Page page)
        {
            frame.Navigate(page);
        }


    }
}