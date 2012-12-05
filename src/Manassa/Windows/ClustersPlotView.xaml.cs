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
using PNNLOmics.Data.Features;
using MultiAlignCore.Data.Imaging;
using Manassa.Data;

namespace Manassa.Windows
{
    /// <summary>
    /// Interaction logic for ClustersView.xaml
    /// </summary>
    public partial class ClustersPlotView : UserControl
    {
        public ClustersPlotView()
        {
            InitializeComponent();

        }

        public List<UMCClusterLight> Clusters
        {
            get { return (List<UMCClusterLight>)GetValue(ClustersProperty); }
            set { SetValue(ClustersProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Clusters.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ClustersProperty =
            DependencyProperty.Register("Clusters", typeof(List<UMCClusterLight>), typeof(ClustersPlotView),
            new PropertyMetadata(delegate(DependencyObject sender ,DependencyPropertyChangedEventArgs args)
            {
                var x = sender as ClustersPlotView;
                x.SetClusters();
            }));

        private void SetClusters()
        {
            ClustersImageData data          = AnalysisImageCreator.CreateClusterPlots(Clusters, PlotWidth, PlotHeight, false);
            ClustersImage                   = ImageConverter.ConvertImage(data.ClustersImage);
            ClustersDatasetSizeHistogram    = ImageConverter.ConvertImage(data.ClustersDatasetSizeHistogramImage);
            ClusterSizeHistogram            = ImageConverter.ConvertImage(data.ClustersSizeHistogramImage);
        }

        public int PlotWidth
        {
            get { return (int)GetValue(PlotWidthProperty); }
            set { SetValue(PlotWidthProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Width.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlotWidthProperty =
            DependencyProperty.Register("PlotWidth", typeof(int), typeof(ClustersPlotView), new UIPropertyMetadata(256));


        public int PlotHeight
        {
            get { return (int)GetValue(PlotHeightProperty); }
            set { SetValue(PlotHeightProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Width.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlotHeightProperty =
            DependencyProperty.Register("PlotHeight", typeof(int), typeof(ClustersPlotView), new UIPropertyMetadata(256));



        public BitmapImage ClustersImage
        {
            get { return (BitmapImage)GetValue(ClustersImageProperty); }
            set { SetValue(ClustersImageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ClustersImage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ClustersImageProperty =
            DependencyProperty.Register("ClustersImage", typeof(BitmapImage), typeof(ClustersPlotView));

        public BitmapImage ClusterSizeHistogram
        {
            get { return (BitmapImage)GetValue(ClusterSizeHistogramProperty); }
            set { SetValue(ClusterSizeHistogramProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ClusterSizeHistogram.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ClusterSizeHistogramProperty =
            DependencyProperty.Register("ClusterSizeHistogram", typeof(BitmapImage), typeof(ClustersPlotView));

        public BitmapImage ClustersDatasetSizeHistogram
        {
            get { return (BitmapImage)GetValue(ClustersDatasetSizeHistogramProperty); }
            set { SetValue(ClustersDatasetSizeHistogramProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ClustersDatasetSizeHistogram.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ClustersDatasetSizeHistogramProperty =
            DependencyProperty.Register("ClustersDatasetSizeHistogram", typeof(BitmapImage), typeof(ClustersPlotView));

    }
}
