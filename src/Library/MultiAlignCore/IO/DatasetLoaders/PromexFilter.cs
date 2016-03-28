﻿namespace MultiAlignCore.IO.DatasetLoaders
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using InformedProteomics.Backend.Utils;

    using MultiAlignCore.Data.Features;
    using MultiAlignCore.Data.MetaData;
    using MultiAlignCore.IO.Features;
    using MultiAlignCore.IO.RawData;
    using MultiAlignCore.IO.TextFiles;

    /// <summary>
    /// Loads and filters Promex datasets.
    /// </summary>
    public class PromexFilter : IDatasetLoader, IFeatureFilter<UMCLight>
    {
        /// <summary>
        /// Data access providers for loading and persisting data.
        /// </summary>
        private readonly FeatureDataAccessProviders dataAccessProviders;

        /// <summary>
        /// Initializes a new instance of the <see cref="PromexFilter" /> class. 
        /// </summary>
        /// <param name="dataAccessProviders">Data access providers for loading/persisting to database.</param>
        public PromexFilter(FeatureDataAccessProviders dataAccessProviders)
        {
            this.dataAccessProviders = dataAccessProviders;

            // Initialize all values with their defaults.
            this.ResetDefaults();
        }

        /// <summary>
        /// Gets or sets a value indicating whether features should be discarded if their
        /// normalized elution range falls outside of <see cref="ElutionTimeRange" />.
        /// </summary>
        public bool UseTimeRangeFilter { get; set; }

        /// <summary>
        /// Gets or sets the minimum and maximum normalized elution time to retain.
        /// </summary>
        public ElutionTimeRange<IElutionTimePoint> ElutionTimeRange { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether features should be discarded if their
        /// normalized elution length falls outside of <see cref="MinElutionLength" /> and
        /// <see cref="MaxElutionLength" />.
        /// </summary>
        public bool UseNetLengthFilter { get; set; }

        /// <summary>
        /// Gets or sets the minimum normalized elution length of the features.
        /// </summary>
        public double MinElutionLength { get; set; }

        /// <summary>
        /// Gets or sets the maximum normalized elution length of the features.
        /// </summary>
        public double MaxElutionLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether features should be discarded if their
        /// likelihood ratio is below <see cref="MinLikelihoodRatio" />.
        /// </summary>
        public bool UseLikelihoodRatioFilter { get; set; }

        /// <summary>
        /// Gets or sets the minimum feature likelihood ratio to retain.
        /// </summary>
        public double MinLikelihoodRatio { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether features should be discarded if their
        /// charge range falls outside of <see cref="MinChargeState" /> and <see cref="MaxChargeState" />.
        /// </summary>
        public bool UseChargeStateFilter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the smallest charge state to retain.
        /// </summary>
        public int MinChargeState { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the largest charge state to retain.
        /// </summary>
        public int MaxChargeState { get; set; }

        /// <summary>
        /// Get the features from the list that pass all filters.
        /// </summary>
        /// <param name="features">The feature list to filter.</param>
        /// <returns>A new list containing features that pass all filters.</returns>
        public List<UMCLight> FilterFeatures(List<UMCLight> features)
        {
            return features.FindAll(this.ShouldKeepFeature);
        }

        /// <summary>
        /// Determines if a feature passes all filters.
        /// </summary>
        /// <param name="feature">The feature to test.</param>
        /// <returns>A value indicating whether the feature passes all filters and should be kept.</returns>
        public bool ShouldKeepFeature(UMCLight feature)
        {
            var keepFeature = true;
            keepFeature &= !this.UseTimeRangeFilter || 
                           (feature.NetStart >= this.ElutionTimeRange.MinValue.Value &&
                            feature.NetEnd <= this.ElutionTimeRange.MaxValue.Value);

            keepFeature &= !this.UseLikelihoodRatioFilter || (feature.AverageDeconFitScore >= this.MinLikelihoodRatio);

            var elutionRange = feature.NetEnd - feature.NetStart;
            keepFeature &= !this.UseNetLengthFilter ||
                           (elutionRange >= this.MinElutionLength && elutionRange <= this.MaxElutionLength);

            return keepFeature;
        }

        /// <summary>
        /// Reset all settings to their default settings.
        /// </summary>
        public void ResetDefaults()
        {
            this.UseTimeRangeFilter = false;
            this.ElutionTimeRange = new ElutionTimeRange<IElutionTimePoint>(new NetTimePoint(), new NetTimePoint(1));

            this.UseNetLengthFilter = false;
            this.MinElutionLength = 0.01;
            this.MaxElutionLength = 0.2;

            this.UseLikelihoodRatioFilter = false;
            this.MinLikelihoodRatio = 0;

            this.UseChargeStateFilter = false;
            this.MinChargeState = 1;
            this.MaxChargeState = 50;
        }

        /// <summary>
        /// Load the Promex feature file and raw file.
        /// </summary>
        /// <param name="dataset">The dataset to load.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>The loaded features.</returns>
        public List<UMCLight> Load(DatasetInformation dataset, IProgress<ProgressData> progress = null)
        {
            // Read raw data.
            var spectraProvider = this.dataAccessProviders.ScanSummaryProviderCache.GetScanSummaryProvider(dataset.RawPath, dataset.DatasetId);

            // Min and max value have to be NETs for filtering Promex results.
            this.ElutionTimeRange = new ElutionTimeRange<IElutionTimePoint>(
                                        this.ElutionTimeRange.MinValue.ToNet(spectraProvider),
                                        this.ElutionTimeRange.MaxValue.ToNet(spectraProvider));

            // Read and filter feature file.
            var promexFileReader = new PromexFileReader(spectraProvider as InformedProteomicsReader, this);
            var features = promexFileReader.ReadFile(dataset.FeaturePath);

            return features.ToList();
        }
    }
}