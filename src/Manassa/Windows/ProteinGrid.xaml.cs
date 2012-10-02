﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MultiAlignCore.Data;
using System.ComponentModel;
using PNNLOmics.Data.Features;
using PNNLOmics.Data.MassTags;
using PNNLOmics.Data;

namespace Manassa.Windows
{
    /// <summary>
    /// Interaction logic for ClusterGrid.xaml
    /// </summary>
    public partial class ProteinGrid : UserControl
    {
        private List<Protein> m_proteins;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ProteinGrid()
        {
            InitializeComponent();
            Proteins = new List<Protein>();
        }

        /// <summary>
        /// Gets or sets the clusters used in the analysis.
        /// </summary>
        public List<Protein> Proteins
        {
            get
            {
                return m_proteins;
            }
            set
            {
                m_proteins = value;
                if (value != null)
                {
                    m_dataGrid.ItemsSource = value;
                }
            }
        }

        public Protein SelectedProtein
        {
            get { return (Protein)GetValue(SelectedMassTagProperty); }
            set { SetValue(SelectedMassTagProperty, value); }
        }        
        public static readonly DependencyProperty SelectedMassTagProperty =
            DependencyProperty.Register("SelectedProtein", typeof(Protein), typeof(ProteinGrid)); 

       
        private void m_dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedProtein = m_dataGrid.SelectedItem as Protein;
        }    
    }
}