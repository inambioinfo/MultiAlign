﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using PNNLOmics.Data;
using MultiAlignCore.Data;

namespace Manassa.Windows
{
    /// <summary>
    /// Interaction logic for ClusterGrid.xaml
    /// </summary>
    public partial class DatasetGrid : UserControl
    {
        private List<DatasetInformation> m_datasets;

        public DatasetGrid()
        {
            InitializeComponent();
            Datasets = new List<DatasetInformation>();
        }

        /// <summary>
        /// Gets or sets the clusters used in the analysis.
        /// </summary>
        public List<DatasetInformation> Datasets
        {
            get
            {
                return m_datasets;
            }
            set
            {
                m_datasets = value;
                if (value != null)
                {
                    m_dataGrid.ItemsSource = value;
                }
            }
        }

        public DatasetInformation SelectedDataset
        {
            get { return (DatasetInformation)GetValue(SelectedClusterProperty); }
            set { SetValue(SelectedClusterProperty, value); }
        }        
        public static readonly DependencyProperty SelectedClusterProperty =
            DependencyProperty.Register("SelectedDataset", typeof(DatasetInformation), typeof(DatasetGrid)); 

       
        private void m_dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedDataset = m_dataGrid.SelectedItem as DatasetInformation;
        }
    }
}