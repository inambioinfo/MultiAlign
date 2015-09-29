﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using MultiAlignCore.Data.MetaData;
using MultiAlignRogue.ViewModels;

namespace MultiAlignRogue
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void WorkflowProgressBar_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ProgressBar bar = sender as ProgressBar;
            this.FileSelectionDataGrid.Margin = bar.IsVisible ? new Thickness(0, 0, 10, 29) : new Thickness(0, 0, 10, 0);
        }
    }
}
