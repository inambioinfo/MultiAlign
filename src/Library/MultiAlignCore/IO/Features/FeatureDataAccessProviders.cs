#region

using MultiAlignCore.Data;
using MultiAlignCore.IO.Analysis;
using MultiAlignCore.IO.Datasets;
using MultiAlignCore.IO.Hibernate;
using MultiAlignCore.IO.MassTags;
using MultiAlignCore.IO.MsMs;
using MultiAlignCore.IO.SequenceData;

#endregion

namespace MultiAlignCore.IO.Features
{
    using MultiAlignCore.IO.RawData;

    /// <summary>
    ///     Holds all feature access providers required by a MA analysis.
    /// </summary>
    public sealed class FeatureDataAccessProviders
    {
        public FeatureDataAccessProviders()
        {
            Synch = new object();
            this.ScanSummaryProviderCache = new ScanSummaryProviderCache();
            this.IdentificationProviderCache = new IdentificationProviderCache();
        }

        /// <summary>
        ///     Gets the object to synch on for concurrent access.
        /// </summary>
        public object Synch { get; private set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="featureCache">LCMS Features</param>
        /// <param name="clusterCache">LCMS Feature clusters</param>
        /// <param name="msFeatureCache">MS Features</param>
        /// <param name="msnFeatureCache">MS/MS Features</param>
        /// <param name="msFeatureMap">MS To LCMS Feature map</param>
        /// <param name="msnFeatureMap">MS to MSn Feature map</param>
        public FeatureDataAccessProviders(IUmcDAO featureCache,
            IUmcClusterDAO clusterCache,
            IMSFeatureDAO msFeatureCache,
            IMSnFeatureDAO msnFeatureCache,
            IMsnFeatureToMSFeatureDAO msnFeatureMap,
            IDatasetDAO datasetCache,
            IMassTagMatchDAO massTagMatches,
            IMassTagDAO massTags,
            IFactorDao factorCache,
            IDatasetToFactorMapDAO factorAssignmentCache,
            IMSMSClusterMapDAO msmsClusterCache,
            IDatabaseSearchSequenceDAO sequenceCache,
            ISequenceToMsnFeatureDAO sequenceMapCache) :
                this()
        {
            ClusterCache = clusterCache;
            FeatureCache = featureCache;
            MSFeatureCache = msFeatureCache;
            MSnFeatureCache = msnFeatureCache;
            MSFeatureToMSnFeatureCache = msnFeatureMap;
            DatasetCache = datasetCache;
            MassTagMatches = massTagMatches;
            MassTags = massTags;
            FactorAssignmentCache = factorAssignmentCache;
            FactorCache = factorCache;
            MSMSClusterCache = msmsClusterCache;
            DatabaseSequenceCache = sequenceCache;
            SequenceMsnMapCache = sequenceMapCache;
        }

        public AlignmentDataPointDAOHibernate AlignmentPointCache { get; set; }

        public ISequenceToMsnFeatureDAO SequenceMsnMapCache { get; set; }

        /// <summary>
        ///     Gets or sets the data provider for storing MS/MS Clusters.
        /// </summary>
        public IDatabaseSearchSequenceDAO DatabaseSequenceCache { get; set; }

        /// <summary>
        ///     Gets or sets the data provider for storing MS/MS Clusters.
        /// </summary>
        public IMSMSClusterMapDAO MSMSClusterCache { get; set; }

        public IFactorDao FactorCache { get; set; }
        public IDatasetToFactorMapDAO FactorAssignmentCache { get; set; }

        /// <summary>
        ///     Gets or sets the mass tags loaded.
        /// </summary>
        public IMassTagDAO MassTags { get; set; }

        /// <summary>
        ///     Gets or sets the cluster to mass tag matches.
        /// </summary>
        public IMassTagMatchDAO MassTagMatches { get; set; }

        /// <summary>
        ///     Gets or sets the cache where the datasets information are stored.
        /// </summary>
        public IDatasetDAO DatasetCache { get; set; }

        /// <summary>
        ///     Gets or sets the interface to teh MS Feature to LCMS Feature map.
        /// </summary>
        public IMsnFeatureToMSFeatureDAO MSFeatureToMSnFeatureCache { get; set; }

        /// <summary>
        ///     Gets or sets the data acces object to LCMS features
        /// </summary>
        public IUmcDAO FeatureCache { get; set; }

        /// <summary>
        ///     Gets or sets the data access object to clusters.
        /// </summary>
        public IUmcClusterDAO ClusterCache { get; set; }

        /// <summary>
        ///     Gets or sets the data acces object to MS features.
        /// </summary>
        public IMSFeatureDAO MSFeatureCache { get; set; }

        /// <summary>
        ///     Gets or sets the data acces object to MS features.
        /// </summary>
        public IMSnFeatureDAO MSnFeatureCache { get; set; }

        public ScanSummaryDAOHibernate ScanSummaryDao { get; set; }

        public ScanSummaryProviderCache ScanSummaryProviderCache { get; set; }

        public AnalysisOptionsDAOHibernate AnalysisOptionsDao { get; set; }

        public IdentificationProviderCache IdentificationProviderCache { get; set; }
    }
}