﻿using MultiAlignCore.Algorithms.Options;
using PNNLOmics.Algorithms;
using PNNLOmics.Algorithms.FeatureClustering;
using PNNLOmics.Data;
using PNNLOmics.Data.Features;
using System;
using System.Collections.Generic;


namespace MultiAlignCore.Algorithms.FeatureFinding
{
    /// <summary>
    /// Finds UMC features based on m/z and uses a tree approach
    /// </summary>
    public class UmcTreeFeatureFinder: IFeatureFinder 
    {        
        /// <summary>
        /// Constructor
        /// </summary>
        public UmcTreeFeatureFinder()
        {            
        }

        /// <summary>
        /// Finds features
        /// </summary>
        /// <returns></returns>
        public List<UMCLight> FindFeatures( List<MSFeatureLight> msFeatures,
                                            LcmsFeatureFindingOptions options,
                                            ISpectraProvider provider)
        {
            var clusterer = new MsFeatureTreeClusterer<MSFeatureLight, UMCLight>
                            {
                                Tolerances =
                                    new FeatureTolerances
                                    {
                                        Mass            = options.InstrumentTolerances.Mass ,
                                        RetentionTime   = options.MaximumNetRange
                                    },
                                ScanTolerance   = options.MaximumScanRange,
                                SpectraProvider = provider
                                //TODO: Make sure we have a mass range for XIC's too....
                            };

            clusterer.SpectraProvider = provider;
            var features  = clusterer.Cluster(msFeatures);

            var minScan = int.MaxValue;
            var maxScan = int.MinValue;
            foreach (var feature in msFeatures)
            {
                minScan = Math.Min(feature.Scan, minScan);
                maxScan = Math.Max(feature.Scan, maxScan);
            }

            int id = 0;
            var newFeatures = new List<UMCLight>();
            foreach (var feature in features)
            {
                if (feature.MSFeatures.Count < 1)
                    continue;
                
                feature.NET             = Convert.ToDouble(feature.Scan - minScan) / Convert.ToDouble(maxScan - minScan);                
                feature.CalculateStatistics(ClusterCentroidRepresentation.Median);                
                feature.RetentionTime   = feature.NET;
                feature.ID              = id++;
                newFeatures.Add(feature);
            }
            return features;
        }
        /// <summary>
        /// Gets or sets the maximum NET values any two XIC's (within the same monoisotopic mass window) can be before they are not considered to be from the same feature.
        /// </summary>
        public double MaximumNet { get; set; }
        /// <summary>
        /// Gets or sets the maximum scan values any two deisotoped peaks can be before they are not considered to be from the same feature.
        /// </summary>
        public int MaximumScan { get; set; }
    }
}
