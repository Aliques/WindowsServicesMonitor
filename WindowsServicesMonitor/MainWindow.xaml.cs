using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsServicesMonitor.ViewModels;

namespace WindowsServicesMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(this);
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            serviceList.Focus();
        }
    }
}
