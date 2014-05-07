﻿using System.Windows;
using System.Windows.Controls;
using PNNLOmics.Data;

namespace MultiAlign.Windows.Viewers.Proteins
{
    /// <summary>
    /// Interaction logic for MassTagDetail.xaml
    /// </summary>
    public partial class ProteinDetail : UserControl
    {
        public ProteinDetail()
        {
            InitializeComponent();
        }

        public Protein Protein
        {
            get { return (Protein)GetValue(ProteinProperty); }
            set { SetValue(ProteinProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MassTag.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ProteinProperty =
            DependencyProperty.Register("Protein", typeof(Protein), typeof(ProteinDetail));


    }
}
