﻿namespace MultiAlignRogue.Alignment
{
    using System;
    using System.Windows.Media.Imaging;

    using MultiAlign.Data;

    using MultiAlignCore.Data.Alignment;
    using MultiAlignCore.Drawing;

    class AlignmentViewModel
    {
        public BitmapImage HeatmapImage { get; private set; }
        public BitmapImage NetScanImage { get; private set; }
        public BitmapImage MassHistogram { get; private set; }
        public BitmapImage NetHistogram { get; private set; }        
        public BitmapImage MassMzImage { get; private set; }
        public BitmapImage MassScanImage { get; private set; }
        public String WindowTitle { get; private set; }
        
        public AlignmentViewModel()
        {

        }

        public AlignmentViewModel(AlignmentData alignment)
        {
            this.WindowTitle = String.Format("{0} Alignment Data",alignment.aligneeDataset);
            var residuals = alignment.ResidualData;

            var heatmap = HeatmapFactory.CreateAlignedHeatmap(alignment.heatScores);
            var netResidual = ScatterPlotFactory.CreateResidualPlot(residuals.Scan, residuals.LinearCustomNet,
                residuals.LinearNet, "NET Residuals", "Scans", "NET");
            var massHistogram = HistogramFactory.CreateHistogram(alignment.netErrorHistogram, "Mass Error", "Mass Error (ppm)");
            var netHistogram = HistogramFactory.CreateHistogram(alignment.netErrorHistogram, "NET Error", "NET Error");            
            var massMzResidual = ScatterPlotFactory.CreateResidualPlot(residuals.Mz, residuals.MzMassError,
                residuals.MzMassErrorCorrected, "Mass Residuals", "m/z", "Mass Errors");
            var massScanResidual = ScatterPlotFactory.CreateResidualPlot(residuals.Scan, residuals.MzMassError,
                residuals.MzMassErrorCorrected, "Mass Residuals", "Scan", "Mass Errors");
            
            this.HeatmapImage = ImageConverter.ConvertImage(PlotImageUtility.CreateImage(heatmap));
            this.NetScanImage = ImageConverter.ConvertImage(PlotImageUtility.CreateImage(netResidual));
            this.MassHistogram = ImageConverter.ConvertImage(PlotImageUtility.CreateImage(massHistogram));
            this.NetHistogram = ImageConverter.ConvertImage(PlotImageUtility.CreateImage(netHistogram));         
            this.MassMzImage = ImageConverter.ConvertImage(PlotImageUtility.CreateImage(massMzResidual));
            this.MassScanImage = ImageConverter.ConvertImage(PlotImageUtility.CreateImage(massScanResidual));          
        }

    }
}
