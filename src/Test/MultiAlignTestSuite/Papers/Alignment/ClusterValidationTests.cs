﻿#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MultiAlign.IO;
using MultiAlign.ViewModels.Instruments;
using MultiAlignCore.Algorithms;
using MultiAlignCore.Algorithms.Alignment;
using MultiAlignCore.Algorithms.Alignment.LcmsWarp;
using MultiAlignCore.Algorithms.Alignment.SpectralMatching;
using MultiAlignCore.Algorithms.Clustering;
using MultiAlignCore.Algorithms.FeatureFinding;
using MultiAlignCore.Algorithms.Options;
using MultiAlignCore.Algorithms.Regression;
using MultiAlignCore.Algorithms.SpectralProcessing;
using MultiAlignCore.Algorithms.Workflow;
using MultiAlignCore.Data;
using MultiAlignCore.Data.Alignment;
using MultiAlignCore.Data.Features;
using MultiAlignCore.Data.MassTags;
using MultiAlignCore.Data.MetaData;
using MultiAlignCore.Extensions;
using MultiAlignCore.IO;
using MultiAlignCore.IO.Features;
using MultiAlignCore.IO.RawData;
using MultiAlignCore.IO.TextFiles;
using MultiAlignCore.Utilities;
using NUnit.Framework;

#endregion

namespace MultiAlignTestSuite.Papers.Alignment
{
    [TestFixture]
    public class ClusterValidationTests : WorkflowBase
    {
        [Test]
        [TestCase(@"M:\data\proteomics\Papers\AlignmentPaper\data\annotated-QC-6\test-annotated-6.db3", 2, Ignore = true)]
        public void TestClusters(string databasetPath, int minMsMsCount)
        {
            var providers = DataAccessFactory.CreateDataAccessProviders(databasetPath, false);
            var clusters = providers.ClusterCache.FindAll();

            Console.WriteLine(@"Cluster ID\tCluster Size\tMs Ms Total\tMatching");
            foreach (var cluster in clusters)
            {
                var clusterPeptideMap = new ClusterIdentificationStatistic();

                if (cluster.MsMsCount > minMsMsCount)

                    cluster.ReconstructUMCCluster(providers, true, false, true, true);

                var hasIdentifications = false;
                foreach (var umc in cluster.UmcList)
                {
                    foreach (var feature in umc.Features)
                    {
                        foreach (var spectrum in feature.MSnSpectra)
                        {
                            foreach (var peptide in spectrum.Peptides)
                            {
                                var sequence = peptide.Sequence;
                                if (!clusterPeptideMap.Peptides.ContainsKey(sequence))
                                {
                                    clusterPeptideMap.Peptides.Add(sequence, 0);
                                    clusterPeptideMap.PeptideDatasets.Add(sequence, new List<int>());
                                }
                                clusterPeptideMap.PeptideDatasets[sequence].Add(umc.GroupId);
                                clusterPeptideMap.Peptides[sequence]++;
                                hasIdentifications = true;
                            }
                        }
                    }
                }

                if (hasIdentifications)
                    Console.WriteLine("{0}\t{1}\t{2}\t{3}", cluster.Id, cluster.UmcList.Count, cluster.MsMsCount,
                        clusterPeptideMap.TotalDatasetsObserved);
            }
        }

        /// <summary>
        ///     Matches Peptides and then asks what clusters they are in
        /// </summary>
        [Test]
        [TestCase(@"M:\data\proteomics\Papers\AlignmentPaper\data\annotated-QC-6\clusterTests-matches.txt", 900, Ignore = true)]
        public void TestPeptides(string databasePath, int scanTolerance)
        {
            var data = File.ReadAllLines(databasePath).ToList();

            var currentPeptide = "";
            var currentDataset = 0;
            var currentClusterId = 0;
            var currentScan = 0;

            var same = 0;
            var notSame = 0;

            var peptides = new List<Peptide>();
            var clusterMap = new Dictionary<string, int>();

            for (var i = 1; i < data.Count; i++)
            {
                var columns = data[i].Split('\t');

                if (columns.Length < 5)
                    continue;

                var datasetId = Convert.ToInt32(columns[0]);
                var sequence = columns[2];
                var scan = Convert.ToInt32(columns[3]);
                var clusterId = Convert.ToInt32(columns[4]);

                var peptide = new Peptide
                {
                    Scan = scan,
                    Sequence = sequence,
                    GroupId = datasetId
                };

                // We found a match
                if (sequence == currentPeptide && datasetId != currentDataset)
                {
                    if (Math.Abs(scan - currentScan) < scanTolerance)
                    {
                        if (currentClusterId == clusterId)
                            same++;
                        else
                            notSame++;
                    }
                }

                currentClusterId = clusterId;
                currentDataset = datasetId;
                currentPeptide = sequence;
                currentScan = scan;
            }

            Console.WriteLine("Same: {0}\t Not Same{1}", same, notSame);
        }

        [Test]
        [TestCase(@"M:\data\proteomics\TestData\QC-Shew-Annotated2",
            @"M:\data\proteomics\TestData\QC-Shew-Annotated2\peptidematches-2.txt", Ignore = true)]
        public void TestPeptideBands(string directory,
            string matchPath)
        {
            // Loads the supported MultiAlign types
            var supportedTypes = DatasetLoader.SupportedFileTypes;
            var extensions = new List<string>();
            supportedTypes.ForEach(x => extensions.Add("*" + x.Extension));

            // Find our datasets
            var datasetLoader = new DatasetLoader();
            var datasets = datasetLoader.GetValidDatasets(directory, extensions, SearchOption.TopDirectoryOnly);

            // Options setup
            var instrumentOptions = InstrumentPresetFactory.Create(InstrumentPresets.LtqOrbitrap);
            var featureTolerances = new FeatureTolerances
            {
                Mass = instrumentOptions.Mass,
                Net = instrumentOptions.NetTolerance,
                DriftTime = instrumentOptions.DriftTimeTolerance
            };

            var msFilterOptions = new MsFeatureFilteringOptions
            {
                MinimumIntensity = 5000,
                ChargeRange = new FilterRange(1, 6),
                ShouldUseChargeFilter = true,
                ShouldUseDeisotopingFilter = true,
                ShouldUseIntensityFilter = true
            };

            var featureFindingOptions = new LcmsFeatureFindingOptions(featureTolerances)
            {
                MaximumNetRange = .002,
                MaximumScanRange = 50
            };

            var baselineDataset = datasets[0];

            UpdateStatus("Loading baseline features.");
            var msFeatures = UmcLoaderFactory.LoadMsFeatureData(baselineDataset.Features.Path);
            msFeatures = LcmsFeatureFilters.FilterMsFeatures(msFeatures, msFilterOptions);
            var finderFinder = FeatureFinderFactory.CreateFeatureFinder(FeatureFinderType.TreeBased);

            var peptideOptions = new SpectralOptions
            {
                ComparerType = SpectralComparison.CosineDotProduct,
                Fdr = .05,
                IdScore = 1e-09,
                MzBinSize = .5,
                MzTolerance = .5,
                NetTolerance = .1,
                RequiredPeakCount = 32,
                SimilarityCutoff = .75,
                TopIonPercent = .8
            };

            var features = new List<MSFeatureLight>();

            // Load the baseline reference set
            using (var rawProviderX = RawLoaderFactory.CreateFileReader(baselineDataset.RawFile.Path))
            {
                rawProviderX.AddDataFile(baselineDataset.RawFile.Path, 0);
                UpdateStatus("Creating Baseline LCMS Features.");
                var baselineFeatures = finderFinder.FindFeatures(msFeatures,
                    featureFindingOptions,
                    rawProviderX);

                LinkPeptidesToFeatures(baselineDataset.Sequence.Path,
                    baselineFeatures,
                    peptideOptions.Fdr,
                    peptideOptions.IdScore);

                baselineFeatures.ForEach(x => features.AddRange(x.MsFeatures));
                features = features.Where(x => x.HasMsMs()).ToList();
                features = features.OrderBy(x => x.Mz).ToList();

                var peptideList = new List<MSFeatureLight>();
                foreach (var feature in features)
                {
                    foreach (var spectrum in feature.MSnSpectra)
                    {
                        var peptideFound = false;
                        foreach (var peptide in spectrum.Peptides)
                        {
                            peptideList.Add(feature);
                            peptideFound = true;
                            break;
                        }

                        if (peptideFound)
                            break;
                    }
                }

                using (var writer = File.CreateText(matchPath))
                {
                    writer.WriteLine("Charge\tpmz\tscan\tNET\t");
                    foreach (var feature in peptideList)
                    {
                        writer.WriteLine("{0}\t{1}\t{2}\t{3}\t", feature.ChargeState, feature.Mz, feature.Scan,
                            feature.Net);
                    }
                }
            }
        }

        [Test]
        [TestCase(@"M:\errors.txt",
            @"M:\errors-fit-05.txt",
            .05,
            5,
            Ignore = true)]
        [TestCase(@"M:\errors.txt",
            @"M:\errors-fit-02.txt",
            .02,
            5,
            Ignore = true)]
        [TestCase(@"M:\errors.txt",
            @"M:\errors-fit-20.txt",
            .2,
            5,
            Ignore = true)]
        [TestCase(@"M:\errors.txt",
            @"M:\errors-fit-30.txt",
            .3,
            15,
            Ignore = true)]
        [TestCase(@"M:\errors.txt",
            @"M:\errors-fit-10.txt",
            .1,
            5,
            Ignore = true)]
        [TestCase(@"M:\errors.txt",
            @"M:\errors-fit-90.txt",
            .9,
            5,
            Ignore = true)]
        public void TestAlignmentfunction(
            string path,
            string outputPath,
            double bandwidth,
            int robustnessIterators
            )
        {
            var data = File.ReadAllLines(path);

            var x = new List<double>();
            var y = new List<double>();

            for (var i = 1; i < data.Length; i++)
            {
                var columns = data[i].Split('\t');
                if (columns.Count() < 4)
                    continue;

                x.Add(Convert.ToDouble(columns[0]));
                y.Add(Convert.ToDouble(columns[2]));
            }
            using (var writer = File.CreateText(outputPath))
            {
                var loess = new LoessInterpolator(bandwidth, robustnessIterators);
                loess.MaxDistance = bandwidth;
                loess.Smooth(x, y, FitFunctionFactory.Create(FitFunctionTypes.Cubic));
                writer.WriteLine("NET\tNETY\tAligned\tNET\tERRORY\tERROR-Aligned");
                for (var i = 0; i < y.Count; i++)
                {
                    var value = loess.Predict(y[i]);
                    writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", x[i], y[i], value, x[i], y[i] - x[i], value - x[i]);
                }
            }
        }

        [Test]
        [TestCase(@"M:\data\proteomics\TestData\QC-Shew-Annotated2",
            @"M:\data\proteomics\TestData\QC-Shew-Annotated2\matches",
            FeatureAlignmentType.SPECTRAL_ALIGNMENT,
            LcmsFeatureClusteringAlgorithmType.AverageLinkage,
            Ignore = true
            )]
        [TestCase(@"M:\data\proteomics\TestData\LipidTests",
            @"M:\data\proteomics\TestData\QC-Shew-Annotated2\lipidMatches",
            FeatureAlignmentType.SPECTRAL_ALIGNMENT,
            LcmsFeatureClusteringAlgorithmType.AverageLinkage,
            Ignore = true
            )]
        public void TestClustering(
            string directory,
            string outputPath,
            FeatureAlignmentType alignmentType,
            LcmsFeatureClusteringAlgorithmType clusterType)
        {
            var matchPath = string.Format("{0}.txt", outputPath);
            var errorPath = string.Format("{0}-errors.txt", outputPath);

            // Loads the supported MultiAlign types
            var supportedTypes = DatasetLoader.SupportedFileTypes;
            var extensions = new List<string>();
            supportedTypes.ForEach(x => extensions.Add("*" + x.Extension));

            // Find our datasets
            var datasetLoader = new DatasetLoader();
            var datasets = datasetLoader.GetValidDatasets(directory, extensions, SearchOption.TopDirectoryOnly);

            // Setup our alignment options
            var alignmentOptions = new AlignmentOptions();
            var spectralOptions = new SpectralOptions
            {
                ComparerType = SpectralComparison.CosineDotProduct,
                Fdr = .01,
                IdScore = 1e-09,
                MzBinSize = .5,
                MzTolerance = .5,
                NetTolerance = .1,
                RequiredPeakCount = 32,
                SimilarityCutoff = .75,
                TopIonPercent = .8
            };


            // Options setup
            var instrumentOptions = InstrumentPresetFactory.Create(InstrumentPresets.LtqOrbitrap);
            var featureTolerances = new FeatureTolerances
            {
                Mass = instrumentOptions.Mass + 6,
                Net = instrumentOptions.NetTolerance,
                DriftTime = instrumentOptions.DriftTimeTolerance
            };
            var featureFindingOptions = new LcmsFeatureFindingOptions(featureTolerances)
            {
                MaximumNetRange = .002,
                MaximumScanRange = 50
            };

            // Create our algorithms
            var finder = FeatureFinderFactory.CreateFeatureFinder(FeatureFinderType.TreeBased);
            var aligner = FeatureAlignerFactory.CreateDatasetAligner(alignmentType,
                alignmentOptions.LCMSWarpOptions,
                spectralOptions);
            var clusterer = ClusterFactory.Create(clusterType);
            clusterer.Parameters = new FeatureClusterParameters<UMCLight>
            {
                Tolerances = featureTolerances
            };

            RegisterProgressNotifier(aligner);
            RegisterProgressNotifier(finder);
            RegisterProgressNotifier(clusterer);

            var lcmsFilters = new LcmsFeatureFilteringOptions
            {
                FeatureLengthRangeScans = new FilterRange(50, 300)
            };
            var msFilterOptions = new MsFeatureFilteringOptions
            {
                MinimumIntensity = 5000,
                ChargeRange = new FilterRange(1, 6),
                ShouldUseChargeFilter = true,
                ShouldUseDeisotopingFilter = true,
                ShouldUseIntensityFilter = true
            };

            for (var i = 0; i < 1; i++)
            {
                var aligneeDatasets = datasets.Where((t, j) => j != i).ToList();
                PerformMultiAlignAnalysis(datasets[0],
                    aligneeDatasets,
                    featureFindingOptions,
                    msFilterOptions,
                    lcmsFilters,
                    spectralOptions,
                    finder,
                    aligner,
                    clusterer,
                    matchPath,
                    errorPath);
            }
        }

        /// <summary>
        ///     Runs the MultiAlign analysis
        /// </summary>
        public void PerformMultiAlignAnalysis(DatasetInformation baselineDataset,
            IEnumerable<DatasetInformation> aligneeDatasets,
            LcmsFeatureFindingOptions featureFindingOptions,
            MsFeatureFilteringOptions msFilterOptions,
            LcmsFeatureFilteringOptions lcmsFilterOptions,
            SpectralOptions peptideOptions,
            MultiAlignCore.Algorithms.FeatureFinding.IFeatureFinder featureFinder,
            IFeatureAligner<IEnumerable<UMCLight>,
                IEnumerable<UMCLight>,
                AlignmentData> aligner,
            IClusterer<UMCLight, UMCClusterLight> clusterer,
            string matchPath,
            string errorPath)
        {
            UpdateStatus("Loading baseline features.");
            var msFeatures = UmcLoaderFactory.LoadMsFeatureData(baselineDataset.Features.Path);
            msFeatures = LcmsFeatureFilters.FilterMsFeatures(msFeatures, msFilterOptions);

            // Load the baseline reference set
            using (var rawProviderX = new InformedProteomicsReader())
            {
                rawProviderX.AddDataFile(baselineDataset.RawFile.Path, 0);
                UpdateStatus("Creating Baseline LCMS Features.");
                var baselineFeatures = featureFinder.FindFeatures(msFeatures,
                    featureFindingOptions,
                    rawProviderX);
                LinkPeptidesToFeatures(baselineDataset.Sequence.Path, baselineFeatures, peptideOptions.Fdr,
                    peptideOptions.IdScore);

                var providerX = new CachedFeatureSpectraProvider(rawProviderX, baselineFeatures);

                // Then load the alignee dataset
                foreach (var dataset in aligneeDatasets)
                {
                    var aligneeMsFeatures = UmcLoaderFactory.LoadMsFeatureData(dataset.Features.Path);
                    aligneeMsFeatures = LcmsFeatureFilters.FilterMsFeatures(aligneeMsFeatures, msFilterOptions);
                    using (var rawProviderY = new InformedProteomicsReader())
                    {
                        rawProviderY.AddDataFile(dataset.RawFile.Path, 0);

                        UpdateStatus("Finding alignee features");
                        var aligneeFeatures = featureFinder.FindFeatures(aligneeMsFeatures,
                            featureFindingOptions,
                            rawProviderY);
                        LinkPeptidesToFeatures(dataset.Sequence.Path, aligneeFeatures, peptideOptions.Fdr,
                            peptideOptions.IdScore);

                        var providerY = new CachedFeatureSpectraProvider(rawProviderY, aligneeFeatures);

                        // cluster before we do anything else....
                        var allFeatures = new List<UMCLight>();
                        allFeatures.AddRange(baselineFeatures);
                        allFeatures.AddRange(aligneeFeatures);
                        foreach (var feature in allFeatures)
                        {
                            feature.Net = feature.Net;
                            feature.MassMonoisotopicAligned = feature.MassMonoisotopic;
                        }

                        // This tells us the differences before we align.
                        var clusters = clusterer.Cluster(allFeatures);
                        var preAlignment = AnalyzeClusters(clusters);

                        aligner.AligneeSpectraProvider = providerY;
                        aligner.BaselineSpectraProvider = providerX;


                        UpdateStatus("Aligning data");
                        // Aligner data
                        var data = aligner.Align(baselineFeatures, aligneeFeatures);
                        var matches = data.Matches;


                        WriteErrors(errorPath, matches);

                        // create anchor points for LCMSWarp alignment
                        var massPoints = new List<RegressionPoint>();
                        var netPoints = new List<RegressionPoint>();
                        foreach (var match in matches)
                        {
                            var massError = FeatureLight.ComputeMassPPMDifference(match.AnchorPointX.Mz,
                                match.AnchorPointY.Mz);
                            var netError = match.AnchorPointX.Net - match.AnchorPointY.Net;
                            var massPoint = new RegressionPoint(match.AnchorPointX.Mz, 0, massError, netError);
                            massPoints.Add(massPoint);

                            var netPoint = new RegressionPoint(match.AnchorPointX.Net, 0, massError, netError);
                            netPoints.Add(netPoint);
                        }


                        foreach (var feature in allFeatures)
                        {
                            feature.UmcCluster = null;
                            feature.ClusterId = -1;
                        }
                        // Then cluster after alignment!
                        UpdateStatus("clustering data");
                        clusters = clusterer.Cluster(allFeatures);
                        var postAlignment = AnalyzeClusters(clusters);

                        UpdateStatus("Note\tSame\tDifferent");
                        UpdateStatus(string.Format("Pre\t{0}\t{1}", preAlignment.SameCluster,
                            preAlignment.DifferentCluster));
                        UpdateStatus(string.Format("Post\t{0}\t{1}", postAlignment.SameCluster,
                            postAlignment.DifferentCluster));

                        SaveMatches(matchPath, matches);
                    }
                }
            }

            DeRegisterProgressNotifier(aligner);
            DeRegisterProgressNotifier(featureFinder);
            DeRegisterProgressNotifier(clusterer);
        }

        private static void WriteErrors(string errorPath, IEnumerable<SpectralAnchorPointMatch> matches)
        {
            using (var writer = File.CreateText(errorPath))
            {
                writer.WriteLine(
                    "NET\tMass\tNET\tMass\tNETA\tMassA\tNETA\tMassA\tNetError\tMassError\tScore");
                foreach (var match in matches)
                {
                    var massError = FeatureLight.ComputeMassPPMDifference(match.AnchorPointX.Mz, match.AnchorPointY.Mz);
                    var netError = match.AnchorPointX.Net - match.AnchorPointY.NetAligned;

                    writer.WriteLine("{0:F5}\t{1:F5}\t{2:F5}\t{3:F5}\t{4:F5}\t{5:F5}\t{6:F5}\t{7:F5}\t{8:F5}\t",
                        match.AnchorPointX.Net,
                        match.AnchorPointX.Mz,
                        match.AnchorPointY.Net,
                        match.AnchorPointY.Mz,
                        match.AnchorPointY.NetAligned,
                        match.AnchorPointY.MzAligned,
                        netError,
                        massError,
                        match.SimilarityScore);
                }
            }
        }


        private void SaveMatches(string path, IEnumerable<SpectralAnchorPointMatch> matches)
        {
            using (var writer = File.CreateText(path))
            {
                writer.WriteLine(
                    "NET-apx\tNET-apy\tNETAligned-apy\tmz-apx\tmzAligned-apx\tmz-apy\tmzAligned-apy\tScan-x\tScan-y\tpmz-x\tpmz-y\tpmonomass-x\tpmonomass-y\tpNET-x\tpNET-y\tpNETa-x\tpNETa-y\tpmonomass-x\tpmonomassyx\tpmonomass-errorppm\tpmz-errorppm");
                foreach (var match in matches)
                {
                    if (match.AnchorPointX.Spectrum == null)
                        continue;

                    if (match.AnchorPointY.Spectrum == null)
                        continue;


                    var parentFeatureX = match.AnchorPointX.Spectrum.ParentFeature;
                    var parentFeatureY = match.AnchorPointY.Spectrum.ParentFeature;


                    var data =
                        string.Format(
                            "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}\t{14}\t{15}\t{16}\t{17}\t{18}\t{19}\t{20}\t",
                            match.AnchorPointX.Net,
                            match.AnchorPointY.Net,
                            match.AnchorPointY.NetAligned,
                            match.AnchorPointX.Mz,
                            match.AnchorPointX.MzAligned,
                            match.AnchorPointY.Mz,
                            match.AnchorPointY.MzAligned,
                            parentFeatureX.Scan,
                            parentFeatureY.Scan,
                            parentFeatureX.Mz,
                            parentFeatureY.Mz,
                            parentFeatureX.MassMonoisotopic,
                            parentFeatureY.MassMonoisotopic,
                            parentFeatureX.GetParentFeature().Net,
                            parentFeatureY.GetParentFeature().Net,
                            parentFeatureX.GetParentFeature().NetAligned,
                            parentFeatureY.GetParentFeature().NetAligned,
                            parentFeatureX.GetParentFeature().MassMonoisotopicAligned,
                            parentFeatureY.GetParentFeature().MassMonoisotopicAligned,
                            FeatureLight.ComputeMassPPMDifference(parentFeatureX.Mz, parentFeatureY.Mz),
                            FeatureLight.ComputeMassPPMDifference(parentFeatureX.GetParentFeature().MassMonoisotopicAligned,
                                parentFeatureY.GetParentFeature().MassMonoisotopicAligned)
                            );

                    writer.WriteLine(data);
                }
            }
        }

        /// <summary>
        ///     Links a list of peptides to the features provided if the dataset has knowledge of the sequence file file
        /// </summary>
        private void LinkPeptidesToFeatures(string sequencePath, List<UMCLight> aligneeFeatures, double fdr,
            double idScore)
        {
            // Get the peptides associated with this feature set.
            var peptideReaderY = PeptideReaderFactory.CreateReader(sequencePath);
            if (peptideReaderY == null)
                return;

            // Load the peptide Y
            UpdateStatus("Linking peptides to ms/ms");
            var linker = new PeptideMsMsLinker();
            var peptides = peptideReaderY.Read(sequencePath).ToList();
            var filteredPeptides = peptides.ToList().Where(x => PeptideUtility.PassesCutoff(x, idScore, fdr)).ToList();

            var msnSpectra = new List<MSSpectra>();
            foreach (var feature in aligneeFeatures)
            {
                foreach (var msFeature in feature.MsFeatures)
                {
                    msnSpectra.AddRange(msFeature.MSnSpectra);
                }
            }


            linker.LinkPeptidesToSpectra(msnSpectra, filteredPeptides);
        }

        /// <summary>
        ///     Extracts the peptides from the given features
        /// </summary>
        /// <param name="features"></param>
        /// <returns></returns>
        private IEnumerable<Peptide> ExtractPeptides(IEnumerable<UMCLight> features)
        {
            var peptides = new List<Peptide>();
            foreach (var feature in features)
            {
                foreach (var msFeature in feature.MsFeatures)
                {
                    foreach (var spectrum in msFeature.MSnSpectra)
                    {
                        peptides.AddRange(spectrum.Peptides);
                    }
                }
            }
            return peptides;
        }

        private PeptideMatchesData AnalyzeClusters(List<UMCClusterLight> clusters)
        {
            var peptides = new Dictionary<string, List<Peptide>>();
            foreach (var cluster in clusters)
            {
                var clusterPeptides = ExtractPeptides(cluster.Features);
                foreach (var peptide in clusterPeptides)
                {
                    if (!peptides.ContainsKey(peptide.Sequence))
                    {
                        peptides.Add(peptide.Sequence, new List<Peptide>());
                    }
                    peptides[peptide.Sequence].Add(peptide);
                }
            }

            // analyze now...
            var matches = new PeptideMatchesData();

            foreach (var sequence in peptides.Keys)
            {
                var map = new Dictionary<int, int>();
                foreach (var peptide in peptides[sequence])
                {
                    var parent = peptide.GetParentUmc();
                    if (parent != null)
                    {
                        if (parent.UmcCluster != null)
                        {
                            var id = parent.UmcCluster.Id;
                            if (!map.ContainsKey(id))
                            {
                                map.Add(id, 0);
                            }
                            map[id]++;
                        }
                    }
                }

                if (map.Count > 1)
                    matches.DifferentCluster++;
                else
                    matches.SameCluster++;
            }
            return matches;
        }


        /// <summary>
        ///     Updates the status to console
        /// </summary>
        /// <param name="message"></param>
        protected override void UpdateStatus(string message)
        {
            Console.WriteLine("\t" + message);
            base.UpdateStatus(message);
        }

        private void ExportAlignmentData(AlignmentData data,
            DatasetInformation baselineDatasetInformation,
            DatasetInformation alignDatasetInformation,
            IEnumerable<UMCLight> baselineFeatures,
            IEnumerable<UMCLight> aligneeFeatures)
        {
            var netValues = new List<double>();
            var massValues = new List<double>();


            var anchorPoints = data.Matches;
            foreach (var match in anchorPoints)
            {
                netValues.Add(match.AnchorPointX.Net - match.AnchorPointY.Net);
                massValues.Add(match.AnchorPointX.Mass - match.AnchorPointY.Mass);
            }


            var netHist =
                MatchCountHistogramBuilder.CreateResidualHistogram(-.05, .05, .01, netValues);


            var netHistogram = new Dictionary<double, int>();

            Console.WriteLine();
            for (var i = 0; i < netHist.Bins.Count; i++)
            {
                netHistogram.Add(netHist.Bins[i], Convert.ToInt32(netHist.Data[i]));
                Console.WriteLine("{0}\t{1}", netHist.Bins[i], netHist.Data[i]);
            }
        }

        public class PeptideMatchesData
        {
            public int SameCluster { get; set; }
            public int DifferentCluster { get; set; }
        }
    }
}