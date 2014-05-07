﻿using System.Collections.Generic;
using MultiAlignCore.Extensions;
using PNNLOmics.Data.Features;
using PNNLOmics.Extensions;

namespace MultiAlignCore.Data.Features
{
    public static class ClusterStats
    {
        /// <summary>
        /// Creates a cluster size histogram for dataset members.
        /// </summary>
        /// <param name="clusters"></param>
        /// <returns></returns>
        public static float[] GetClusterMemberSizes(List<UMCClusterLight> clusters)
        {            
            if (clusters.Count < 1)
                return null;

            var clusterMaps = clusters.CreateClusterDatasetMemeberSizeHistogram();
                        
            // Find the maximum cluster size.
            var sizes = new List<int>();
            foreach (var key in clusterMaps.Keys)
            {
                sizes.Add(key);
            }
            sizes.Sort();
            var maxClusters = sizes[sizes.Count - 1] + 3;

            // Create the histogram.
            var bins  = new float[maxClusters];
            var freqs = new float[maxClusters];

            var i = 0;
            for (i = 0; i < maxClusters; i++)
            {
                if (clusterMaps.ContainsKey(i))
                {
                    freqs[i] = clusterMaps[i];
                }
                else
                {
                    freqs[i] = 0;
                }
            }

            return freqs;
        }

    }
}
