﻿using MultiAlignCore.Data;
using MultiAlignCore.IO.Parameters;
using PNNLOmics.Algorithms;
using PNNLOmics.Algorithms.Distance;
using PNNLOmics.Algorithms.FeatureClustering;
using PNNLOmics.Data.Features;
using System.ComponentModel;

namespace MultiAlignCore.Algorithms.Clustering
{
    /// <summary>
    /// Options for clustering LC-MS Features.
    /// </summary>
    public class LCMSFeatureClusteringOptions
    {       
        public LCMSFeatureClusteringOptions()
        {            
			MassTolerance		    = 6.0; 
			NETTolerance		    = 0.03; 
			DriftTimeTolerance      = 50.0;			
            LcmsFeatureClusteringAlgorithm     = LcmsFeatureClusteringAlgorithmType.AverageLinkage;
			ClusterCentroid         = ClusterCentroidRepresentation.Median;
            DistanceFunction        = DistanceMetric.WeightedEuclidean;
			IgnoreCharge		    = true;			
            AlignClusters	        = false;			
        }


        [ParameterFileAttribute("ClusteringAlgorithm", "LCMSFeatureClustering")]        
        [Category("Algorithm")]
        [Description("Determines the type of clustering algorithm to use.")]
        public LcmsFeatureClusteringAlgorithmType LcmsFeatureClusteringAlgorithm
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the enumeration for selecting a distance function to use for clustering.
        /// </summary>
        [ParameterFileAttribute("DistanceFunction", "LCMSFeatureClustering")]
        [Category("Distance Function")]
        [Description("Determines the distance function to use.")]
        public DistanceMetric DistanceFunction
        {
            get;
            set;
        }
        [ParameterFileAttribute("AlignClusters", "LCMSFeatureClustering")]
        [DataSummaryAttribute("Align clusters to database")]
        [Category("AMT")]
        [Description("This is only valid if you have a mass tag database.  Setting to True will align each cluster to the database.")]
        public bool AlignClusters { get; set; }

        [ParameterFileAttribute("ClusterRepresentativeType", "LCMSFeatureClustering")]
        [DataSummaryAttribute("Cluster Representative Type")]
        [Category("Centroid")]
        [Description("Determines how the centroid should be calculated.")]
        public ClusterCentroidRepresentation ClusterCentroid { get; set; }

        [ParameterFileAttribute("IgnoreCharge", "LCMSFeatureClustering")]
        [DataSummaryAttribute("Ignore Charge")]
        [Category("Ion Mobility")]
        [Description("Set Ignore Charge to True if you are not using Ion Mobility (IMS).  Set to False if you are using IMS.")]
        public bool IgnoreCharge { get; set; }

        [ParameterFileAttribute("DriftTimeTolerance", "LCMSFeatureClustering")]
        [DataSummaryAttribute("Drift Time Tolerance")]
        [Category("Tolerances")]
        [Description("For Ion Mobility Data.  Tolerance in drift time (ms) set to a large value if not using LC-MS")]
        public double DriftTimeTolerance { get; set; }


        [ParameterFileAttribute("MassTolerance", "LCMSFeatureClustering")]
        [DataSummaryAttribute("Mass Tolerance")]
        [Category("Tolerances")]
        [Description("Mass Tolerance in parts per million (PPM).")]
        public double MassTolerance { get; set; }

        [ParameterFileAttribute("NETTolerance", "LCMSFeatureClustering")]
        [DataSummaryAttribute("NET Tolerance")]
        [Category("Tolerances")]
        [Description("Normalized elution time (NET) tolerance.")]
        public double NETTolerance { get; set; }

        
    }
    
}