﻿#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PNNLOmics.Algorithms.Distance;
using PNNLOmics.Algorithms.FeatureClustering;
using PNNLOmics.Data.Features;

#endregion

namespace MultiAlignTestSuite.Algorithms
{
    public class UMCClustering
    {
        public List<UMCClusterLight> ReadClusters(string path)
        {
            var clusters = new List<UMCClusterLight>();
            var data = File.ReadLines(path).ToList();

            var isClusters = true;

            var i = 1;

            var clusterMap = new Dictionary<int, UMCClusterLight>();

            while (i < data.Count && isClusters)
            {
                isClusters = !(data[i].ToLower().Contains("dataset"));
                if (isClusters)
                {
                    var cluster = new UMCClusterLight();
                    var lineData = data[i].Split(',');
                    cluster.Id = Convert.ToInt32(lineData[0]);
                    cluster.MassMonoisotopic = Convert.ToDouble(lineData[1]);
                    cluster.RetentionTime = Convert.ToDouble(lineData[2]);
                    cluster.Net = Convert.ToDouble(lineData[2]);
                    cluster.DriftTime = Convert.ToDouble(lineData[3]);
                    cluster.ChargeState = Convert.ToInt32(lineData[4]);

                    if (!clusterMap.ContainsKey(cluster.Id))
                    {
                        clusterMap.Add(cluster.Id, cluster);
                    }

                    clusters.Add(cluster);
                }
                i = i + 1;
            }

            i = i + 1;
            while (i < data.Count)
            {
                var line = data[i];
                var lineData = line.Split(',');
                if (line.Length > 6)
                {
                    var clusterID = Convert.ToInt32(lineData[0]);
                    var feature = new UMCLight();
                    feature.GroupId = Convert.ToInt32(lineData[1]);
                    feature.Id = Convert.ToInt32(lineData[2]);
                    feature.MassMonoisotopic = Convert.ToDouble(lineData[3]);
                    feature.RetentionTime = Convert.ToDouble(lineData[4]);
                    feature.NetAligned = Convert.ToDouble(lineData[4]);
                    feature.DriftTime = Convert.ToDouble(lineData[5]);
                    feature.ChargeState = Convert.ToInt32(lineData[6]);

                    if (clusterMap.ContainsKey(clusterID))
                    {
                        clusterMap[clusterID].AddChildFeature(feature);
                    }
                }
                i = i + 1;
            }

            return clusters;
        }

        public List<UMCLight> ReadFeatures(string path)
        {
            var features = new List<UMCLight>();
            var data = File.ReadLines(path).ToList();
            var isClusters = true;
            var i = 1;

            while (i < data.Count && isClusters)
            {
                isClusters = !(data[i].ToLower().Contains("dataset"));
                i = i + 1;
            }
            i = i + 1;
            while (i < data.Count)
            {
                var line = data[i];
                var lineData = line.Split(',');
                if (line.Length > 6)
                {
                    var clusterID = Convert.ToInt32(lineData[0]);
                    var feature = new UMCLight();
                    feature.GroupId = Convert.ToInt32(lineData[1]);
                    feature.Id = Convert.ToInt32(lineData[2]);
                    feature.MassMonoisotopicAligned = Convert.ToDouble(lineData[3]);
                    feature.RetentionTime = Convert.ToDouble(lineData[4]);
                    feature.MassMonoisotopic = feature.MassMonoisotopicAligned;
                    feature.NetAligned = Convert.ToDouble(lineData[4]);
                    feature.Net = feature.NetAligned;
                    feature.DriftTime = Convert.ToDouble(lineData[5]);
                    feature.ChargeState = Convert.ToInt32(lineData[6]);
                    features.Add(feature);
                }
                i = i + 1;
            }
            return features;
        }

        public List<UMCClusterLight> CreateSingletonClustersFromClusteredFeatures(List<UMCClusterLight> clusters)
        {
            var newClusters = new List<UMCClusterLight>();

            var i = 0;
            foreach (var cluster in clusters)
            {
                foreach (var feature in cluster.Features)
                {
                    var x = new UMCClusterLight();
                    x.MassMonoisotopic = feature.MassMonoisotopic;
                    x.RetentionTime = feature.RetentionTime;
                    x.Net = feature.NetAligned;
                    x.DriftTime = feature.DriftTime;
                    x.ChargeState = feature.ChargeState;
                    x.Id = i++;
                    x.AddChildFeature(feature);
                    newClusters.Add(x);
                }
            }
            return newClusters;
        }

        private void WriteClusters(List<UMCClusterLight> clusters)
        {
            Console.WriteLine("[Clusters]");
            Console.WriteLine("NET, Mass, DT");

            for (var i = 0; i < clusters.Count; i++)
            {
                Console.WriteLine("{0},{1},{2},{3}",
                    clusters[i].Id,
                    clusters[i].RetentionTime,
                    clusters[i].MassMonoisotopic,
                    clusters[i].DriftTime);
            }
            Console.WriteLine("[Features]");
            Console.WriteLine("Cluster ID, Dataset ID, Feature ID, charge, NET, Mass, DT");
            foreach (var clusterLight in clusters)
            {
                foreach (var feature in clusterLight.Features)
                {
                    Console.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                        clusterLight.Id,
                        feature.GroupId,
                        feature.Id,
                        feature.ChargeState,
                        feature.RetentionTime,
                        feature.MassMonoisotopic,
                        feature.DriftTime);
                }
            }
        }

        [Test(Description = "Tests how clusters form when the are within tolerance boolean is not set")]
        [TestCase(@"M:\data\proteomics\Clusters\twoClusters-split.csv", DistanceMetric.WeightedEuclidean, true)]
        [TestCase(@"M:\data\proteomics\Clusters\twoClusters-split.csv", DistanceMetric.Euclidean, true)]
        public void TestRestrictiveBoxMethod(string path, DistanceMetric dist, bool useBoxMethod)
        {
            var features = ReadFeatures(path);
            var clusterer = new UMCAverageLinkageClusterer<UMCLight, UMCClusterLight>();

            clusterer.ShouldTestClustersWithinTolerance = useBoxMethod;
            clusterer.Parameters.CentroidRepresentation = ClusterCentroidRepresentation.Mean;
            clusterer.Parameters.DistanceFunction = DistanceFactory<UMCLight>.CreateDistanceFunction(dist);
            clusterer.Parameters.OnlyClusterSameChargeStates = true;
            clusterer.Parameters.Tolerances.Mass = 10;
            clusterer.Parameters.Tolerances.DriftTime = .3;
            clusterer.Parameters.Tolerances.RetentionTime = .03;
            var clusters = clusterer.Cluster(features);
            var i = 0;
            clusters.ForEach(x => x.Id = i++);
            WriteClusters(clusters);
        }

        [Test(Description = "Tests how clusters form when the are within tolerance boolean is not set")]
        [TestCase(@"M:\data\proteomics\Clusters\twoClusters-split.csv", DistanceMetric.WeightedEuclidean)]
        [TestCase(@"M:\data\proteomics\Clusters\twoClusters-split.csv", DistanceMetric.Euclidean)]
        public void TestDistanceDistributions(string path, DistanceMetric dist)
        {
            var features = ReadFeatures(path);
            var clusterer = new UMCAverageLinkageClusterer<UMCLight, UMCClusterLight>();

            clusterer.ShouldTestClustersWithinTolerance = false;
            clusterer.Parameters.CentroidRepresentation = ClusterCentroidRepresentation.Mean;
            clusterer.Parameters.DistanceFunction = DistanceFactory<UMCLight>.CreateDistanceFunction(dist);
            clusterer.Parameters.OnlyClusterSameChargeStates = true;
            clusterer.Parameters.Tolerances.Mass = 10;
            clusterer.Parameters.Tolerances.DriftTime = .3;
            clusterer.Parameters.Tolerances.RetentionTime = .03;
            var clusters = clusterer.Cluster(features);

            var distances = new List<double>();
            foreach (var cluster in clusters)
            {
                var centroid = new UMCLight();
                centroid.MassMonoisotopicAligned = cluster.MassMonoisotopic;
                centroid.RetentionTime = cluster.RetentionTime;
                centroid.DriftTime = cluster.DriftTime;

                var func = clusterer.Parameters.DistanceFunction;
                foreach (var feature in cluster.Features)
                {
                    var distance = func(feature, centroid);
                    distances.Add(distance);
                }
                distances.Sort();
                var sum = 0;
                foreach (var distance in distances)
                {
                    sum++;
                    Console.WriteLine("{0},{1}", distance, sum);
                }
            }
        }

        [Test]
        [TestCase(@"M:\data\proteomics\Clusters\clusterBaseline-01.csv", DistanceMetric.WeightedEuclidean)]
        public void TestDistancesEuclidean(string path, DistanceMetric dist)
        {
            var func = DistanceFactory<UMCClusterLight>.CreateDistanceFunction(DistanceMetric.Euclidean);
            var oldClusters = ReadClusters(path);
            var clusters = CreateSingletonClustersFromClusteredFeatures(new List<UMCClusterLight> {oldClusters[1]});

            Console.WriteLine("Distance, Mass, NET, DT, Mass, Net, DT");

            for (var i = 0; i < clusters.Count; i++)
            {
                for (var j = i + 1; j < clusters.Count; j++)
                {
                    var distance = func(clusters[i], clusters[j]);
                    Console.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                        distance,
                        clusters[i].MassMonoisotopic,
                        clusters[i].Net,
                        clusters[i].DriftTime,
                        clusters[j].MassMonoisotopic,
                        clusters[j].Net,
                        clusters[j].DriftTime);
                }
            }
        }

        [Test]
        public void TestDistanceChangeEuclidean()
        {
            var cluster = new UMCClusterLight();
            cluster.MassMonoisotopic = 500;
            cluster.Net = .5;
            cluster.RetentionTime = .5;
            cluster.DriftTime = 20;


            var euclid = new EuclideanDistanceMetric<UMCClusterLight>();
            DistanceFunction<UMCClusterLight> func = euclid.EuclideanDistance;

            var deltaNet = .01;
            double deltaMassPPM = 1;
            double deltaDriftTime = 1;

            Console.WriteLine("Mass Diff, Mass Dist, Net, Net Dist, Drift, Drift Dist");

            for (var i = 0; i < 50; i++)
            {
                var clusterD = new UMCClusterLight();
                var clusterN = new UMCClusterLight();
                var clusterM = new UMCClusterLight();

                clusterM.DriftTime = cluster.DriftTime + deltaDriftTime;
                clusterM.Net = cluster.Net + deltaNet;
                clusterM.RetentionTime = cluster.RetentionTime + deltaNet;
                clusterM.MassMonoisotopic = FeatureLight.ComputeDaDifferenceFromPPM(cluster.MassMonoisotopic,
                    deltaMassPPM*i);


                clusterN.DriftTime = cluster.DriftTime + deltaDriftTime;
                clusterN.Net = cluster.Net + (deltaNet*i);
                clusterN.RetentionTime = cluster.RetentionTime + (deltaNet*i);
                clusterN.MassMonoisotopic = FeatureLight.ComputeDaDifferenceFromPPM(cluster.MassMonoisotopic,
                    deltaMassPPM);


                clusterD.DriftTime = cluster.DriftTime + (deltaDriftTime*i);
                clusterD.Net = cluster.Net + deltaNet;
                clusterD.RetentionTime = cluster.RetentionTime + deltaNet;
                clusterD.MassMonoisotopic = FeatureLight.ComputeDaDifferenceFromPPM(cluster.MassMonoisotopic,
                    deltaMassPPM);

                var distM = func(cluster, clusterM);
                var distN = func(cluster, clusterN);
                var distD = func(cluster, clusterD);

                var output = string.Format("{0},{1},{2},{3},{4},{5}", deltaMassPPM*i, distM, deltaNet*i, distN,
                    deltaDriftTime*i, distD);
                Console.WriteLine(output);
            }
        }
    }
}