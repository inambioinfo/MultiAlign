﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using InformedProteomics.Backend.Utils;
using MultiAlignCore.Algorithms;
using MultiAlignCore.Algorithms.FeatureFinding;
using MultiAlignCore.Algorithms.Workflow;
using MultiAlignCore.Data;
using MultiAlignCore.Data.MetaData;
using MultiAlignCore.Data.SequenceData;
using MultiAlignCore.IO.Features;
using MultiAlignCore.IO.RawData;
using MultiAlignCore.Algorithms.Clustering;
using MultiAlignCore.Data.Features;
using MultiAlignCore.IO.TextFiles;

#endregion

namespace MultiAlignCore.IO
{
    /// <summary>
    ///     This class is in charge of caching the appropriate LCMS Feature data to the underlying database.
    /// </summary>
    public class FeatureLoader : WorkflowBase
    {
        public FeatureDataAccessProviders Providers { get; set; }

        public void CacheFeatures(IList<UMCLight> features)
        {
            // SpectraTracker - Makes sure that we only record a MS spectra once, before we cache
            // this keeps us from trying to put duplicate entries into the MS/MS data 
            // table/container.
            var spectraTracker = new Dictionary<int, MSSpectra>();
            var msmsFeatures = new List<MSSpectra>();
            var mappedPeptides = new List<DatabaseSearchSequence>();
            var sequenceMaps = new List<SequenceToMsnFeature>();

            // This dictionary makes sure that the peptide was not seen already, since a peptide can be mapped multiple times...?                
            var matches = new List<MSFeatureToMSnFeatureMap>();
            var msFeatures = new List<MSFeatureLight>();
            var peptideId = 0;

            // Next we may want to map our MSn features to our parents.  This would allow us to do traceback...
            foreach (var feature in features)
            {
                var totalMsMs = 0;
                var totalIdentified = 0;
                var datasetId = feature.GroupId;
                msFeatures.AddRange(feature.MsFeatures);

                // For Each MS Feature
                foreach (var msFeature in feature.MsFeatures)
                {
                    totalMsMs += msFeature.MSnSpectra.Count;
                    // For each MS / MS 
                    foreach (var spectrum in msFeature.MSnSpectra)
                    {
                        var match = new MSFeatureToMSnFeatureMap
                        {
                            RawDatasetID = datasetId,
                            MSDatasetID = datasetId,
                            MSFeatureID = msFeature.Id,
                            MSMSFeatureID = spectrum.Id,
                            LCMSFeatureID = feature.Id
                        };
                        spectrum.GroupId = datasetId;
                        matches.Add(match);

                        if (spectraTracker.ContainsKey(spectrum.Id))
                            continue;

                        msmsFeatures.Add(spectrum);
                        spectraTracker.Add(spectrum.Id, spectrum);

                        // We are prepping the sequences that we found from peptides that were 
                        // matched only, not all of the results. 
                        // These maps here are made to help establish database search results to msms 
                        // spectra
                        foreach (var peptide in spectrum.Peptides)
                        {
                            peptide.GroupId = datasetId;
                            var newPeptide = new DatabaseSearchSequence(peptide, feature.Id)
                            {
                                GroupId = datasetId,
                                Id = peptideId++
                            };
                            mappedPeptides.Add(newPeptide);

                            var sequenceMap = new SequenceToMsnFeature
                            {
                                UmcFeatureId = feature.Id,
                                DatasetId = msFeature.GroupId,
                                MsnFeatureId = spectrum.Id,
                                SequenceId = peptide.Id
                            };
                            sequenceMaps.Add(sequenceMap);
                        }

                        totalIdentified += spectrum.Peptides.Count;
                    }
                }

                feature.MsMsCount = totalMsMs;
                feature.IdentifiedSpectraCount = totalIdentified;
            }

            var count = 0;
            //TODO: Fix!!! make sure sequence maps are unique
            sequenceMaps.ForEach(x => x.Id = count++);

            if (msmsFeatures.Count > 0)
            {
                Providers.MSnFeatureCache.AddAll(msmsFeatures);
            }

            if (matches.Count > 0)
            {
                Providers.MSFeatureToMSnFeatureCache.AddAll(matches);
            }

            if (sequenceMaps.Count > 0)
            {
                Providers.SequenceMsnMapCache.AddAll(sequenceMaps);
            }

            if (mappedPeptides.Count > 0)
            {
                Providers.DatabaseSequenceCache.AddAll(mappedPeptides);
            }

            if (msFeatures.Count > 0)
            {
                Providers.MSFeatureCache.DeleteByDatasetId(msFeatures[0].GroupId);
                Providers.MSFeatureCache.AddAllStateless(msFeatures);
            }

            if (features.Count > 0)
            {
                Providers.FeatureCache.DeleteByDataset(features[0].GroupId);
                Providers.FeatureCache.AddAllStateless(features);
            }
        }

        /// <summary>
        ///     Creates LCMS Features
        /// </summary>
        public List<UMCLight> CreateLcmsFeatures(
            DatasetInformation information,
            List<MSFeatureLight> msFeatures,
            LcmsFeatureFindingOptions options,
            LcmsFeatureFilteringOptions filterOptions,
            IProgress<ProgressData> progress = null)
        {
            // Make features
            if (msFeatures.Count < 1)
                throw new Exception("No features were found in the feature files provided.");

            UpdateStatus("Finding features.");
            ISpectraProvider provider = null;
            if (information.RawPath != null && !string.IsNullOrWhiteSpace(information.RawPath))
            {
                UpdateStatus("Using raw data to create better features.");
                provider = RawLoaderFactory.CreateFileReader(information.RawPath);
                provider.AddDataFile(information.RawPath, 0);
            }

            ValidateFeatureFinderMaxScanLength(information, options, filterOptions);

            var finder = FeatureFinderFactory.CreateFeatureFinder(FeatureFinderType.TreeBased);
            finder.Progress += (sender, args) => UpdateStatus(args.Message);
            var features = finder.FindFeatures(msFeatures, options, provider, information);

            UpdateStatus("Filtering features.");
            List<UMCLight> filteredFeatures;
            if (filterOptions.TreatAsTimeNotScan) //Feature length determined based on time (mins)
            {
                filteredFeatures = LcmsFeatureFilters.FilterFeatures(features,
                    filterOptions, information.ScanTimes);
            }
            else //Feature length determined based on scans
            {
                filteredFeatures = LcmsFeatureFilters.FilterFeatures(features, filterOptions);
            }

            UpdateStatus(string.Format("Filtered features from: {0} to {1}.", features.Count, filteredFeatures.Count));
            return filteredFeatures;
        }

        /// <summary>
        /// Make sure the value for options.MaximumScanRange, which is used by the Feature Finder, 
        /// is at least as large as the filterOptions.FeatureLengthRange.Maximum value, 
        /// which is used for filtering the features by length
        /// </summary>
        /// <param name="information"></param>
        /// <param name="options"></param>
        /// <param name="filterOptions"></param>
        private static void ValidateFeatureFinderMaxScanLength(
            DatasetInformation information,
            LcmsFeatureFindingOptions options,
            LcmsFeatureFilteringOptions filterOptions)
        {
            if (!filterOptions.TreatAsTimeNotScan)
            {
                if (options.MaximumScanRange < filterOptions.FeatureLengthRange.Maximum)
                {
                    // Bump up the scan range used by the LCMS Feature Finder to allow for longer featuers
                    options.MaximumScanRange = (int)filterOptions.FeatureLengthRange.Maximum;
                }
                return;
            }

            int maxScanLength;

            if (information.ScanTimes.Count == 0)
            {
                // FeatureLengthRange.Maximum is in minutes
                // Assume 3 scans/second (ballpark estimate)
                maxScanLength = (int)filterOptions.FeatureLengthRange.Maximum * 60 * 3;
            }
            else
            {
                // Find the average number of scans that spans FeatureLengthRange.Maximum minutes

                // Step through the dictionary to find the average number of scans per minute
                var minuteThreshold = 1;
                var scanCountCurrent = 0;
                var scanCountsPerMinute = new List<int>();

                foreach (var entry in information.ScanTimes)
                {
                    if (entry.Value < minuteThreshold)
                    {
                        scanCountCurrent++;
                    }
                    else
                    {
                        if (scanCountCurrent > 0)
                        {
                            scanCountsPerMinute.Add(scanCountCurrent);
                        }
                        scanCountCurrent = 0;
                        minuteThreshold++;
                    }
                }

                int averageScansPerMinute;
                if (scanCountsPerMinute.Count > 0)
                {
                    averageScansPerMinute = (int)scanCountsPerMinute.Average();
                }
                else
                {
                    averageScansPerMinute = 180;
                }

                maxScanLength = (int)(filterOptions.FeatureLengthRange.Maximum * averageScansPerMinute * 1.25);
            }

            if (options.MaximumScanRange < maxScanLength)
            {
                // Bump up the scan range used by the LCMS Feature Finder to allow for longer featuers
                options.MaximumScanRange = maxScanLength;
            }
        }

        /// <summary>
        ///     Load a single dataset from the provider.
        /// </summary>
        /// <returns></returns>
        public IList<UMCLight> LoadDataset(DatasetInformation dataset,
            MsFeatureFilteringOptions msFilteringOptions,
            LcmsFeatureFindingOptions lcmsFindingOptions,
            LcmsFeatureFilteringOptions lcmsFilteringOptions,
            IProgress<ProgressData> progress = null)
        {
            progress = progress ?? new Progress<ProgressData>();
            var progData = new ProgressData { IsPartialRange = true };
            IProgress<ProgressData> internalProgress = new Progress<ProgressData>(pd => progress.Report(progData.UpdatePercent(pd.Percent)));

            progData.MaxPercentage = 3;
            progData.Status = "Looking for existing features in the database.";
            UpdateStatus(string.Format("[{0}] - Loading dataset [{0}] - {1}.", dataset.DatasetId, dataset.DatasetName));
            var datasetId = dataset.DatasetId;
            var features = UmcLoaderFactory.LoadUmcFeatureData(dataset.Features.Path, dataset.DatasetId,
                Providers.FeatureCache);
            internalProgress.Report(new ProgressData { Percent = 100 });


            progData.StepRange(7);
            progData.Status = "Loading MS Feature Data.";
            UpdateStatus(string.Format("[{0}] Loading MS Feature Data [{0}] - {1}.", dataset.DatasetId,
                dataset.DatasetName));
            var msFeatures = UmcLoaderFactory.LoadMsFeatureData(dataset.Features.Path);
            internalProgress.Report(new ProgressData { Percent = 100 });

            progData.StepRange(10);
            progData.Status = "Loading scan summaries.";
            var scansInfo = UmcLoaderFactory.LoadScanSummaries(dataset.Scans.Path);
            dataset.BuildScanTimes(scansInfo);
            internalProgress.Report(new ProgressData { Percent = 100 });

            var msnSpectra = new List<MSSpectra>();

            progData.StepRange(100);

            // If we don't have any features, then we have to create some from the MS features
            // provided to us.
            if (features.Count < 1)
            {
                msFeatures = LcmsFeatureFilters.FilterMsFeatures(msFeatures, msFilteringOptions);
                msFeatures = Filter(msFeatures, ref dataset);

                progData.Status = "Creating LCMS features.";
                features = CreateLcmsFeatures(dataset,
                    msFeatures,
                    lcmsFindingOptions,
                    lcmsFilteringOptions,
                    internalProgress);

                //var maxScan = Convert.ToDouble(features.Max(feature => feature.Scan));
                //var minScan = Convert.ToDouble(features.Min(feature => feature.Scan));
                var maxScan = features.Max(feature => feature.Scan);
                var minScan = features.Min(feature => feature.Scan);
                var id = 0;
                var scanTimes = dataset.ScanTimes;

                foreach (var feature in features)
                {
                    feature.Id = id++;
                    //feature.Net = (Convert.ToDouble(feature.Scan) - minScan) / (maxScan - minScan);
                    feature.Net = (Convert.ToDouble(scanTimes[feature.Scan]) - scanTimes[minScan]) / (scanTimes[maxScan] - scanTimes[minScan]);
                    feature.MassMonoisotopicAligned = feature.MassMonoisotopic;
                    feature.NetAligned = feature.Net;
                    feature.GroupId = datasetId;
                    feature.SpectralCount = feature.MsFeatures.Count;

                    foreach (var msFeature in feature.MsFeatures.Where(msFeature => msFeature != null))
                    {
                        msFeature.UmcId = feature.Id;
                        msFeature.GroupId = datasetId;
                        msFeature.MSnSpectra.ForEach(x => x.GroupId = datasetId);
                        msnSpectra.AddRange(msFeature.MSnSpectra);
                    }
                }
            }
            else
            {
                if (!UmcLoaderFactory.AreExistingFeatures(dataset.Features.Path))
                {
                    var i = 0;
                    foreach (var feature in features)
                    {
                        feature.GroupId = datasetId;
                        feature.Id = i++;
                    }
                }

                // Otherwise, we need to map the MS features to the LCMS Features provided.
                // This would mean that we extracted data from an existing database.
                if (msFeatures.Count > 0)
                {
                    var map = FeatureDataConverters.MapFeature(features);
                    foreach (var feature in
                        from feature in msFeatures
                        let doesFeatureExists = map.ContainsKey(feature.UmcId)
                        where doesFeatureExists
                        select feature)
                    {
                        map[feature.UmcId].AddChildFeature(feature);
                    }
                }

                internalProgress.Report(new ProgressData { Percent = 100 });
            }


            // Process the MS/MS data with peptides
            UpdateStatus("Reading List of Peptides");
            var sequenceProvider = PeptideReaderFactory.CreateReader(dataset.SequencePath);
            if (sequenceProvider != null)
            {
                UpdateStatus("Reading List of Peptides");
                var peptides = sequenceProvider.Read(dataset.SequencePath);
                var count = 0;
                var peptideList = peptides.ToList();
                peptideList.ForEach(x => x.Id = count++);

                UpdateStatus("Linking MS/MS to any known Peptide/Metabolite Sequences");

                var linker = new PeptideMsMsLinker();
                linker.LinkPeptidesToSpectra(msnSpectra, peptideList);
            }
            return features;
        }

        /// <summary>
        ///     Filters the list of MS Features that may be from MS/MS deisotoped data.
        /// </summary>
        public List<MSFeatureLight> Filter(List<MSFeatureLight> msFeatures, ref DatasetInformation dataset)
        {
            string rawPath = dataset.RawPath;
            if (rawPath == null || string.IsNullOrWhiteSpace(rawPath))
                return msFeatures;

            // First find all unique scans
            var scanMap = new Dictionary<int, bool>();
            foreach (var feature in msFeatures)
            {
                if (!scanMap.ContainsKey(feature.Scan))
                {
                    // Assume all scans are parents
                    scanMap.Add(feature.Scan, true);
                }
            }
            // Then parse each to figure out if this is true.
            var fullScans = new Dictionary<int, bool>();
            var scanTimes = dataset.ScanTimes;
            using (var provider = RawLoaderFactory.CreateFileReader(rawPath))
            {
                if (provider == null)
                {
                    UpdateStatus(string.Format("Warning: Raw file not found ({0}); scan times are not available!", System.IO.Path.GetFileName(rawPath)));
                }
                else
                {
                    UpdateStatus(string.Format("Reading scan info from {0}", System.IO.Path.GetFileName(rawPath)));

                    provider.AddDataFile(rawPath, 0);
                    foreach (var scan in scanMap.Keys)
                    {
                        ScanSummary summary = provider.GetScanSummary(scan, 0);

                        if (summary == null) { continue;}
                        if (summary.MsLevel == 1) { fullScans.Add(scan, true); }         
                        if (scanTimes.ContainsKey(scan)){ scanTimes[scan] = summary.Time; }
                        else { scanTimes.Add(scan, summary.Time); }                    
                    }
                    dataset.ScanTimes = scanTimes;
                }
            }

            return msFeatures.Where(x => fullScans.ContainsKey(x.Scan)).ToList();
        }
    }
}