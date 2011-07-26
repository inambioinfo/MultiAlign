using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MultiAlignCore.Algorithms.Alignment;
using MultiAlignCore.Algorithms.PeakMatching;
using MultiAlignCore.Data;
using MultiAlignCore.Data.Alignment;
using MultiAlignCore.Data.MassTags;
using MultiAlignCore.IO;
using MultiAlignCore.IO.Features;
using MultiAlignCore.IO.InputFiles;
using MultiAlignCore.IO.Mammoth;
using MultiAlignCore.IO.MTDB;
using MultiAlignEngine.Clustering;
using MultiAlignEngine.Features;
using MultiAlignEngine.MassTags;
using PNNLOmics.Algorithms;
using PNNLOmics.Algorithms.Alignment;
using PNNLOmics.Algorithms.FeatureClustering;
using PNNLOmics.Data;
using PNNLOmics.Data.Features;
using PNNLOmics.Data.MassTags;
using PNNLOmics.IO.FileReaders;
using PNNLOmics.Algorithms.FeatureMatcher.MSnLinker;

namespace MultiAlignCore.Algorithms
{        
    /// <summary>
    /// Transition class to remove the excess from the data storage object.
    ///     Remove garbage code and mixed-mode BS.
    /// </summary>
    public class MultiAlignAnalysisProcessor: IDisposable
    {   
        #region Events
        /// <summary>
        /// Fired when features are loaded.
        /// </summary>
        public event EventHandler<FeaturesLoadedEventArgs>      FeaturesLoaded;
        /// <summary>
        /// Fired when features are aligned.
        /// </summary>
        public event EventHandler<FeaturesAlignedEventArgs>     FeaturesAligned;
        /// <summary>
        /// Fired when features are clustered.
        /// </summary>
        public event EventHandler<FeaturesClusteredEventArgs>   FeaturesClustered;
        /// <summary>
        /// Fired when features are peak matched.
        /// </summary>
        public event EventHandler<FeaturesPeakMatchedEventArgs> FeaturesPeakMatched;
        /// <summary>
        /// Fired when a catastrophic error occurs.
        /// </summary>
        public event EventHandler<AnalysisErrorEventArgs> AnalysisError;
        /// <summary>
        /// Fired when the analysis is complete.
        /// </summary>
        public event EventHandler<AnalysisCompleteEventArgs> AnalysisComplete;
        /// <summary>
        /// Fired when a status update is ready.
        /// </summary>
        public event EventHandler<AnalysisStatusEventArgs> Status;
        #endregion

        #region Members
        /// <summary>
        /// Algorithms used in processing.
        /// </summary>
        private AlgorithmProvider m_algorithms;
        /// <summary>
        /// Holds all information about how to perform the analysis and how to store results.
        /// </summary>
        private MultiAlignAnalysis m_analysis;
        /// <summary>
        /// Thread in charge of performing the analysis.
        /// </summary>
        private Thread m_analysisThread;
        #endregion

        #region Constructor
        /// <summary>
        /// Default constructor.
        /// </summary>
        public MultiAlignAnalysisProcessor()
        {
            m_analysis   = null;
            m_algorithms = null;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the object that provides the algorithms for clustering, aligning, etc.
        /// </summary>
        public AlgorithmProvider AlgorithmProvders
        {
            get
            {
                return m_algorithms;
            }
            set
            {
                m_algorithms = value;
            }
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Dispose method that will kill the analysis thread.
        /// </summary>
        public void Dispose()
        {
            StopAnalysis();
                                    
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }
        /// <summary>
        /// Aborts the analysis thread.
        /// </summary>
        private void AbortAnalysisThread(Thread threadToAbort)
        {
            try
            {
                if (threadToAbort != null && threadToAbort.IsAlive)
                    threadToAbort.Abort();
            }
            finally
            {                
                threadToAbort = null;
            }
        }
        #endregion

        #region Delegate Handlers / Marshallers
        /// <summary>
        /// Updates listeners with status messages.
        /// </summary>
        /// <param name="message"></param>
        private void UpdateStatus(string message)
        {
            if (Status != null)
            {
                Status(this, new AnalysisStatusEventArgs(message, 0));
            }
        }
        #endregion

        #region Analysis Methods
        /// <summary>
        /// Links MSMS data to MS Features.
        /// </summary>
        /// <param name="providers"></param>
        private void LinkMSFeaturesToMSMS(FeatureDataAccessProviders providers, List<DatasetInformation> information)
        {
            foreach (DatasetInformation info in information)
            {                                    
                int id                          = Convert.ToInt32(info.DatasetId);
                List<MSSpectra> spectra         = providers.MSnFeatureCache.FindByDatasetId(id);
                List<MSFeatureLight> features   = providers.MSFeatureCache.FindByDatasetId(id);
                
                UpdateStatus(string.Format("Linking {0} MS features to {1} MSMS features for dataset {0}",
                                        features.Count,                        
                                        spectra.Count,
                                        info.DatasetName
                                        ));
                IMSnLinker linker               = MSnLinkerFactory.CreateLinker(MSnLinkerType.BoxMethod);
                linker.Tolerances               = new FeatureTolerances();
                linker.Tolerances.Mass          = m_analysis.MSLinkerOptions.MzTolerance;
                linker.LinkMSFeaturesToMSn(features, spectra);

                List<MSFeatureToMSnFeatureMap> matches = new List<MSFeatureToMSnFeatureMap>();
                foreach (MSFeatureLight feature in features)
                {                    
                    if (feature.MSnSpectra.Count > 0)
                    {
                        foreach(MSSpectra spectrum in feature.MSnSpectra)
                        {
                            MSFeatureToMSnFeatureMap match  = new MSFeatureToMSnFeatureMap();
                            match.RawDatasetID  = id;
                            match.MSDatasetID   = id;
                            match.MSFeatureID   = feature.ID;
                            match.MSMSFeatureID = spectrum.ID;
                            matches.Add(match);
                        }
                    }
                }
                
                UpdateStatus(string.Format("Found {0} matching MS-MSn features", matches.Count));
                providers.MSFeatureToMSnFeatureCache.AddAll(matches);                
            }
        }
        /// <summary>
        /// Loads raw file data.
        /// </summary>
        /// <param name="otherFiles"></param>
        /// <param name="analysisPath"></param>
        /// <param name="dataProviders"></param>
        private void LoadOtherData( List<InputFile>             otherFiles,                                    
                                    string                      analysisPath,
                                    FeatureDataAccessProviders  dataProviders)
        {
            Dictionary<string, int> datasetMap  = new Dictionary<string, int>();
            Dictionary<int, int> datasetIDMap   = new Dictionary<int, int>();
            IRawDataFileReader  rawReader       = null;
            ScansFileReader     scansReader     = new ScansFileReader();
            IMSnFeatureDAO      msnCache        = dataProviders.MSnFeatureCache;

            // Map the dataset ids
            foreach(DatasetInformation information in m_analysis.MetaData.Datasets)
            {
                string datasetName = DatasetInformation.CleanNameDatasetNameOfExtensions(information.DatasetName);
                datasetMap.Add(datasetName, Convert.ToInt32(information.DatasetId));
                datasetIDMap.Add(Convert.ToInt32(information.DatasetId), 0);
            }

            int datasetID = 0;
            foreach (InputFile file in otherFiles)
            {
                string path = file.Path;
                if (path == null)
                    continue;

                string datasetName  = DatasetInformation.CleanNameDatasetNameOfExtensions(path);
                datasetName         = Path.GetFileNameWithoutExtension(datasetName);                
                bool containsKey    = datasetMap.ContainsKey(datasetName);
                if (containsKey)
                {
                    datasetID = datasetMap[datasetName];
                }
                else
                {
                    bool hasID = true;
                    datasetID  = 0;
                    while(hasID)
                    {
                        datasetID++;
                        hasID = datasetIDMap.ContainsKey(datasetID);
                    }
                    datasetMap.Add(datasetName, datasetID);
                    datasetIDMap.Add(datasetID, 0);
                }

                UpdateStatus("Loading auxillary file " + Path.GetFileName(path) + ".");                
                switch(file.FileType)
                {
                    case InputFileType.Scans:                            
                        List<ScanSummary> scans     = scansReader.ReadFile(path).ToList();
                        break;
                    case InputFileType.Raw:
                        rawReader                   = RawLoaderFactory.CreateFileReader(path);
                        List<MSSpectra> msnSpectra  = rawReader.ReadMSMSSpectra(path);
                        int id                      = 0;
                        foreach (MSSpectra spectra in msnSpectra)
                        {
                            spectra.ID      = id++;
                            spectra.GroupID = datasetID;
                        }
                        try
                        {
                            msnCache.AddAll(msnSpectra);
                        }
                        catch(Exception ex)
                        {
                            throw ex;

                        }
                        break;
                }                                
            }
        }          
        /// <summary>
        /// Load the data from the dataset information objects to the cache at the analysis Path
        /// </summary>
        /// <param name="datasets">Datasets to load.</param>
        /// <param name="options">Options to use for UMC finding if required.</param>
        /// <param name="analysisPath">Path to save data to.</param>
        private void LoadDatasetData(List<DatasetInformation> datasets,
                                     clsUMCFindingOptions     options,
                                     string analysisPath)
        {            
            IUmcDAO featureCache            = m_analysis.DataProviders.FeatureCache;
            IMSFeatureDAO msFeatureCache    = m_analysis.DataProviders.MSFeatureCache;
            IGenericDAO<MSFeatureToLCMSFeatureMap> map = m_analysis.DataProviders.MSFeatureToLCMSFeatureCache;

            foreach (DatasetInformation dataset in datasets)
            {
                UpdateStatus("Loading dataset " + dataset.DatasetName + ".");
                List<clsUMC> features = UMCLoaderFactory.LoadData(  dataset,
                                                                    featureCache,
                                                                    msFeatureCache,
                                                                    map,
                                                                    options);

                UpdateStatus("Loaded dataset " + dataset.DatasetName + ". Adding to cache.");

                featureCache.AddAll(features);
                if (FeaturesLoaded != null)
                {
                    FeaturesLoadedEventArgs args = new FeaturesLoadedEventArgs(dataset, features);
                    FeaturesLoaded(this, args);
                }
                features.Clear();
                features = null;
            }
        }          
        /// <summary>
        /// Performs clustering using the mammoth framework.
        /// </summary>
        /// <param name="analysis"></param>
        /// <param name="clusterer"></param>
        /// <returns></returns>
        public void PerformClustering(MultiAlignAnalysis                    analysis,
                                      IClusterer<UMCLight, UMCClusterLight> clusterer)
        {
            UpdateStatus("Using Cluster Algorithm: " + clusterer.ToString());

            // Tolerances
            FeatureTolerances tolerances                        = new FeatureTolerances();
            FeatureClusterParameters<UMCLight> parameters       = new FeatureClusterParameters<UMCLight>();
            tolerances.DriftTime                                = analysis.ClusterOptions.DriftTimeTolerance;
            tolerances.Mass                                     = analysis.ClusterOptions.MassTolerance;
            tolerances.RetentionTime                            = analysis.ClusterOptions.NETTolerance;
            parameters.CentroidRepresentation                   = PNNLOmics.Data.Features.ClusterCentroidRepresentation.Mean;
            parameters.Tolerances                               = tolerances;            
            parameters.OnlyClusterSameChargeStates              = (analysis.ClusterOptions.IgnoreCharge == false);
            if (analysis.ClusterOptions.ClusterRepresentativeType == enmClusterRepresentativeType.MEDIAN)
            {
                parameters.CentroidRepresentation = ClusterCentroidRepresentation.Median;
            }

            clusterer.Parameters = parameters;                        
            // This just tells us whether we are using mammoth memory partitions or not.            
            UpdateStatus("Clustering will run with all features loaded into memory. <recursion levels now ignored>");            

            string databaseName = Path.Combine(m_analysis.MetaData.AnalysisPath, m_analysis.MetaData.AnalysisName);
            int maxChargeState = 15;
            int minChargeState = 1;

            

            // Connect to the database.
            using (MammothDatabase database = new MammothDatabase(databaseName))
            {
                database.Connect();
                MammothDatabaseRange range = new MammothDatabaseRange(  -9000,
                                                                        90000,
                                                                        -10,
                                                                        10,
                                                                        -100,
                                                                        7000);
                List<int> chargeStatesToCluster = new List<int>();
                if (analysis.ClusterOptions.IgnoreCharge)
                {
                    chargeStatesToCluster.Add(-1);
                }
                else
                {
                    for (int i = minChargeState; i <= maxChargeState; i++)
                    {
                        chargeStatesToCluster.Add(i);
                    }
                }

                int clusterCountID = 0;
                foreach (int chargeState in chargeStatesToCluster)
                {
                    UpdateStatus("Retrieving features for clustering from cache.");

                    range.SingleChargeState         = chargeState;
                    List<UMCLight> features         = database.GetNonClusteredFeatures(range);

                    // Determine the number of features to cluster.
                    int featureCount = 0;
                    if (features != null)
                    {
                        featureCount = features.Count;
                    }

                    if (chargeState < 0)
                    {
                        UpdateStatus(string.Format("Clustering all charge states.  Total Features = {0}.", featureCount));
                    }
                    else
                    {
                        UpdateStatus(string.Format("Clustering Charge State = {0}.  Total features = {1}", 
                                                    chargeState, 
                                                    featureCount));
                    }

                    List<UMCClusterLight> clusters  = new List<UMCClusterLight>();
                    clusters                        = clusterer.Cluster(features, clusters);

                    UpdateStatus("Updating cluster id numbers.");
                    foreach (UMCClusterLight cluster in clusters)
                    {
                        cluster.ID = clusterCountID++;
                    }

                    UpdateStatus("Updating feature cache with cluster id's.");
                    database.UpdateFeaturesAndClusters(features);

                    UpdateStatus("Finished clustering charge state.");
                }
            }
        }        
        /// <summary>
        /// Aligns all of the datasets.
        /// </summary>
        public void PerformAlignment(MultiAlignAnalysis analysis)
        {
            // Connect to database of features.                
            IUmcDAO featureCache = m_analysis.DataProviders.FeatureCache;
            
            // Load the baseline data.
            List<clsUMC> baselineFeatures   = null;
            int baselineDatasetID           = -1;
            DatasetInformation baselineInfo = null;

            UpdateStatus("Confirming baseline.");
            if (!analysis.UseMassTagDBAsBaseline)
            {
                int i = 0;
                foreach(DatasetInformation info in analysis.MetaData.Datasets)
                {
                    //TODO: Check the path of the dataset as the dataset name!
                    if (info.DatasetName == analysis.BaselineDatasetName)
                    {
                        baselineDatasetID = i;
                        break;
                    }
                    i++;
                }
                baselineInfo        = analysis.MetaData.Datasets[baselineDatasetID];

                UpdateStatus("Loading baseline features from " + baselineInfo.DatasetName + " for alignment.");
                baselineFeatures    = featureCache.FindByDatasetId(baselineDatasetID);
            }

            if (baselineInfo == null && m_analysis.DriftTimeAlignmentOptions.ShouldAlignDriftTimes)
            {
                baselineFeatures = new List<clsUMC>();
                // Convert the mass tags to features.                
                foreach (MassTagLight tag in analysis.MassTagDatabase.MassTags)
                {                    
                    clsUMC umc               = new clsUMC();
                    umc.ChargeRepresentative = 0;
                    umc.Net                  = tag.NET;
                    umc.MassCalibrated       = tag.MassMonoisotopic;
                    umc.DriftTime            = tag.DriftTime;
                    umc.Id                   = tag.ID;
                    umc.ChargeRepresentative = Convert.ToInt16(tag.ChargeState);
                    baselineFeatures.Add(umc);
                }
            }
            
            // Align pairwise and cache results intermediately.
            IFeatureAligner aligner = m_algorithms.Aligner;

            m_analysis.AlignmentOptions.Clear();

            for (int datasetNum = 0; datasetNum < m_analysis.MetaData.Datasets.Count; datasetNum++)
            {
                if (datasetNum != baselineDatasetID)
                {

                    DatasetInformation datasetInfo = m_analysis.MetaData.Datasets[datasetNum];

                    UpdateStatus("Retrieving data from " + datasetInfo.DatasetName + " for alignment.");
                    List<clsUMC> features               = featureCache.FindByDatasetId(datasetNum);
                    classAlignmentData alignmentData    = null;

                    //// We dont track the UMC Index...this was used previously by the UMC Data object :(
                    //// Here we are applying a temp fix to iterate through the features and assign its unique ID.                    
                    int featureIDIndex = 0;
                    foreach (clsUMC feature in features)
                    {
                        feature.mint_umc_index = featureIDIndex++;
                    }

                    // LCMSWarp alignment.
                    m_analysis.AlignmentOptions.Add(m_analysis.DefaultAlignmentOptions);

                    KeyValuePair<DriftTimeAlignmentResults<UMC, UMC>, DriftTimeAlignmentResults<UMC, UMC>> pair
                        = new KeyValuePair<DriftTimeAlignmentResults<UMC, UMC>, DriftTimeAlignmentResults<UMC, UMC>>();
                    
                    if (baselineInfo != null)
                    {
                        UpdateStatus("Aligning " + datasetInfo.DatasetName + " to baseline.");
                        alignmentData = aligner.AlignFeatures(baselineFeatures,
                                                            features,
                                                            analysis.AlignmentOptions[datasetNum]);


                    }
                    else
                    {
                        UpdateStatus("Aligning " + datasetInfo.DatasetName + " to mass tag database.");
                        alignmentData = aligner.AlignFeatures(analysis.MassTagDatabase,
                                                            features,
                                                            analysis.AlignmentOptions[datasetNum]);
                    }

                    // Drift time alignment.
                    if (analysis.DriftTimeAlignmentOptions.ShouldAlignDriftTimes)
                    {
                        pair = AlignDriftTimes(features, baselineFeatures, analysis.DriftTimeAlignmentOptions);
                    }

                    alignmentData.aligneeDataset = datasetInfo.DatasetName;                    
                    analysis.AlignmentData.Add(alignmentData);


                    UpdateStatus("Updating cache with aligned features.");
                    featureCache.UpdateAll(features);

                    if (FeaturesAligned != null)
                    {

                        DriftTimeAlignmentResults<UMC, UMC> alignedResults = null;
                        DriftTimeAlignmentResults<UMC, UMC> offsetResults  = null;
                        if (analysis.DriftTimeAlignmentOptions.ShouldAlignDriftTimes )
                        {
                            alignedResults = pair.Key;                            
                            offsetResults  = pair.Value;
                        }

                        FeaturesAlignedEventArgs args = new FeaturesAlignedEventArgs(baselineInfo,
                                                                                     datasetInfo,
                                                                                     alignmentData,
                                                                                     alignedResults, 
                                                                                     offsetResults);
                        FeaturesAligned(this, args);
                    }
                    features.Clear();
                    features = null;
                }
                else
                {
                    m_analysis.AlignmentData.Add(null);
                    m_analysis.AlignmentOptions.Add(null);
                }
            }
        }
        /// <summary>
        /// Correct for the drift times.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="baselineFeatures"></param>
        /// <param name="options"></param>
        private KeyValuePair<DriftTimeAlignmentResults<UMC, UMC>, DriftTimeAlignmentResults<UMC, UMC>> AlignDriftTimes(List<clsUMC> features, List<clsUMC> baselineFeatures, DriftTimeAlignmentOptions options)
        {
            UpdateStatus("Correcting drift times.");
            List<UMC> baselineUMCs  = new List<UMC>();
            List<UMC> aligneeUMCs   = new List<UMC>();

            UpdateStatus("Mapping data structures.");
            Dictionary<int, clsUMC> featureIDMap = new Dictionary<int, clsUMC>();
            // TODO: Convert the clsUmc's to umcs...
            foreach (clsUMC feature in features)
            {
                UMC umc                     = new UMC();
                umc.MassMonoisotopicAligned = feature.MassCalibrated;
                umc.NETAligned              = feature.Net;
                umc.DriftTime               = Convert.ToSingle(feature.DriftTime);                
                umc.ID                      = feature.Id;
                umc.ChargeState             = feature.ChargeRepresentative;
                aligneeUMCs.Add(umc);
                featureIDMap.Add(feature.Id, feature);
            }
            
            foreach (clsUMC feature in baselineFeatures)
            {
                UMC umc                     = new UMC();
                umc.MassMonoisotopicAligned = feature.MassCalibrated;
                umc.NETAligned              = feature.Net;         
                umc.DriftTime               = Convert.ToSingle(feature.DriftTime);
                umc.ID                      = feature.Id;
                umc.ChargeState             = feature.ChargeRepresentative;       
                baselineUMCs.Add(umc);
            }

            // filter based on charge state.                  
            int chargeMax = options.MaxChargeState;
            int chargeMin = options.MinChargeState;

            UpdateStatus(string.Format("Filtering Features Min Charge: {0} <= charge <= Max Charge {1}",chargeMin, chargeMax));
            var filteredQuery       =       from   feature in aligneeUMCs
                                            where  feature.ChargeState <= chargeMax && feature.ChargeState >= chargeMin
                                            select feature;
            List<UMC> filteredUMCs  = filteredQuery.ToList();

            UpdateStatus("Finding Aligned Matches and correcting drift times.");
            DriftTimeAlignmentResults<UMC, UMC> alignedResults = 
                            DriftTimeAlignment<UMC, UMC>.AlignObservedEnumerable(aligneeUMCs, 
                                                                                filteredUMCs, 
                                                                                baselineUMCs, 
                                                                                options.MassPPMTolerance,
                                                                                options.NETTolerance);


            DriftTimeAlignmentResults<UMC, UMC> offsetResults = null;
            if (options.ShouldPerformOffset)
            {
                UpdateStatus("Adjusting drift time offsets.");
                List<UMC> aligneeData = aligneeUMCs;
                if (!options.ShouldUseAllObservationsForOffsetCalculation)
                {
                    UpdateStatus("Using only filtered matches for offset correction.");
                    aligneeData = filteredUMCs;
                }
                else
                {
                    UpdateStatus("Using all feature matches for offset correction.");
                }
                offsetResults = DriftTimeAlignment<UMC, UMC>.CorrectForOffset(aligneeData, baselineUMCs, options.MassPPMTolerance, options.NETTolerance, options.DriftTimeTolerance);
            }


            UpdateStatus("Remapping data structures for persistence to database.");
            //TODO: Then repopulate the drift times post...
            foreach(UMC umc in aligneeUMCs)
            {
                featureIDMap[umc.ID].DriftTime = umc.DriftTimeAligned;
            }

            KeyValuePair<DriftTimeAlignmentResults<UMC, UMC>, DriftTimeAlignmentResults<UMC, UMC>> pair =
                            new KeyValuePair<DriftTimeAlignmentResults<UMC, UMC>, DriftTimeAlignmentResults<UMC, UMC>>(alignedResults,
                                                                                                                       offsetResults);
            return pair;
        }        
        /// <summary>
        /// Performs peak matching with loaded clusters. 
        /// </summary>
        public void PerformPeakMatching()
        {
            bool shouldPeakMatch = true;
            if (shouldPeakMatch)
            {
                if (m_analysis.MassTagDatabase != null)
                {
                    List<clsCluster>                oldClusters     = m_analysis.DataProviders.ClusterCache.FindAll();
                    IPeakMatcher<UMCClusterLight>   peakMatcher     = m_algorithms.PeakMatcher;


                    if (!m_analysis.ClusterOptions.AlignClusters)
                    {
                        UpdateStatus("Adjusting masses and NETs for clusters for peak matching.");

                        // If the clusters are not aligned, then the 
                        // peak matching jobs may fail as they require the data structure to have 
                        // calibrated data.  Talk about coupling!
                        foreach (clsCluster cluster in oldClusters)
                        {
                            cluster.MassCalibrated  = cluster.Mass;
                            cluster.NetAligned      = cluster.Net;
                        }
                    }
                    else
                    {
                        //TODO: Align clusters.
                        UpdateStatus("Adjusting masses and NETs for clusters for peak matching.");
                        // If the clusters are not aligned, then the 
                        // peak matching jobs may fail as they require the data structure to have 
                        // calibrated data.  Talk about coupling!
                        foreach (clsCluster cluster in oldClusters)
                        {
                            cluster.MassCalibrated  = cluster.Mass;
                            cluster.NetAligned      = cluster.Net;
                        }
                        m_algorithms.Aligner.AlignFeatures(m_analysis.MassTagDatabase, oldClusters, m_analysis.DefaultAlignmentOptions);
                        m_analysis.DataProviders.ClusterCache.UpdateAll(oldClusters);
                    }
                    
                    
                    // Temp hack while we wait for hibernate to be fixed to allow for OMICs structures to be used.
                    List<UMCClusterLight> clusters = new List<UMCClusterLight>();
                    foreach (clsCluster oldCluster in oldClusters)
                    {
                        UMCClusterLight cluster     = new UMCClusterLight();
                        cluster.ID                  = oldCluster.Id;
                        cluster.MassMonoisotopic    = oldCluster.MassCalibrated;
                        cluster.NET                 = oldCluster.NetAligned;
                        cluster.DriftTime           = oldCluster.DriftTime;
                        clusters.Add(cluster);
                    }

                    if (m_analysis.PeakMatchingOptions.UseSTAC)
                    {
                        
                        UpdateStatus("Peak matching with STAC");
                        m_analysis.STACResults = peakMatcher.PerformSTAC(clusters,
                                                                            m_analysis.MassTagDatabase,
                                                                            m_analysis.STACOptions);

                        List<FeatureMatchLight<UMCClusterLight, MassTagLight>> matches = peakMatcher.ConvertSTACResultsToPeakResults( m_analysis.STACResults,
                                                                                                                          m_analysis.MassTagDatabase,
                                                                                                                          clusters);   
                     

                    }
                    else
                    {
                        UpdateStatus("Traditional Peak matching.");
                        List<FeatureMatchLight<UMCClusterLight, MassTagLight>> matches = peakMatcher.PerformPeakMatching(clusters, 
                                                                                                m_analysis.MassTagDatabase, 
                                                                                                m_analysis.PeakMatchingOptions,
                                                                                                0.0);

                        UpdateStatus("Traditional Peak matching with 11-dalton shift.");
                        List<FeatureMatchLight<UMCClusterLight, MassTagLight>> shiftedMatches = peakMatcher.PerformPeakMatching(clusters,
                                                                                                m_analysis.MassTagDatabase,
                                                                                                m_analysis.PeakMatchingOptions, 
                                                                                                11.0);

                        m_analysis.PeakMatchingResults          = matches;
                        m_analysis.ShiftedPeakMatchingResults   = matches;
                    }


                    UpdateStatus("Updating database with peak matched results.");
                    PeakMatchResultsWriter writer   = new PeakMatchResultsWriter();
                    int matchedMassTags             = 0;
                    int matchedProteins             = 0;
                    writer.WritePeakMatchResults(m_analysis, out matchedMassTags, out matchedProteins);

                    UpdateStatus(string.Format("Found {0} mass tag matches. Matching to {1} potential proteins.",
                                                                                                matchedMassTags,
                                                                                                matchedProteins));

                    if (m_analysis.PeakMatchingOptions.WriteResultsBackToMTS && m_analysis.MetaData.JobID != -1)
                    {
                        //string databasePath                 = "";
                        MTSPeakMatchResultsWriter mtsWriter = new MTSSqlServerPeakMatchResultWriter();
                        //mtsWriter.WriteClusters(m_analysis, 
                        //                        clusters,
                        //                        databasePath);
                    }
                    else if (m_analysis.PeakMatchingOptions.WriteResultsBackToMTS)
                    {
                        UpdateStatus("Cannot write mass tag results back to database.  The Job ID was not specified for this analysis.");
                    }
                }
            }
        }
        #endregion

        #region Analysis Start/Stop
        /// <summary>
        /// Starts a multi-Align analysis job.
        /// </summary>
        public void StartAnalysis(MultiAlignAnalysis analysis)
        {
            if (m_algorithms == null)
            {
                throw new NullReferenceException("The algorithms have not been set for this analysis.");
            }


            if (analysis == null)
            {
                throw new NullReferenceException("The analysis data storage cannot be null.");
            }

            if (analysis.DataProviders == null)
            {
                throw new NullReferenceException("The data cache providers have not been set for this analysis.");
            }

            // Make sure we start with a fresh analysis.
            AbortAnalysisThread(m_analysisThread);

            m_analysis = analysis;

            ThreadStart threadStart  = new ThreadStart(PerformAnalysis);
            m_analysisThread         = new Thread(threadStart);
            m_analysisThread.Start();
        }
        /// <summary>
        /// Aborts the analysis thread.
        /// </summary>
        public void StopAnalysis()
        {
            AbortAnalysisThread(m_analysisThread);
        }
        /// <summary>
        /// Starts the main analysis.
        /// </summary>
        private void PerformAnalysis()
        {
            try
            {
                MassTagDatabase database = null;

                UpdateStatus("Setting up parameters");

                // Load the mass tag database if we are aligning, or if we are 
                // peak matching (but aligning to a reference dataset.
                if (m_analysis.UseMassTagDBAsBaseline)
                {
                    UpdateStatus("Loading Mass Tag database from database:  " + m_analysis.MassTagDBOptions.mstrDatabase);
                    
                    database = MTDBLoaderFactory.LoadMassTagDB(m_analysis.MassTagDBOptions, m_analysis.MetaData.AnalysisSetupInfo.Database.DatabaseFormat);
                }
                else
                {
                    if (m_analysis.MassTagDBOptions.menm_databaseType != MassTagDatabaseType.None)
                    {
                        UpdateStatus("Loading Mass Tag database from database:  " + m_analysis.MassTagDBOptions.mstrDatabase);
                        database = MTDBLoaderFactory.LoadMassTagDB(m_analysis.MassTagDBOptions, m_analysis.MetaData.AnalysisSetupInfo.Database.DatabaseFormat);
                    }
                }

                if (database != null)
                {
                    int totalMassTags = database.MassTags.Count;
                    UpdateStatus("Loaded " + totalMassTags.ToString() + " mass tags.");
                }

                m_analysis.MassTagDatabase = database;

                UpdateStatus("Loading auxillary data files.");
                LoadOtherData(m_analysis.MetaData.OtherFiles,
                                Path.Combine(m_analysis.MetaData.AnalysisPath, m_analysis.MetaData.AnalysisName),
                                m_analysis.DataProviders);

                UpdateStatus("Loading dataset data files.");
                LoadDatasetData(m_analysis.MetaData.Datasets,
                                m_analysis.UMCFindingOptions,
                                Path.Combine(m_analysis.MetaData.AnalysisPath, m_analysis.MetaData.AnalysisName));

                UpdateStatus("Linking MS Features to MSn Features.");
                LinkMSFeaturesToMSMS(m_analysis.DataProviders, m_analysis.MetaData.Datasets);

                UpdateStatus("Aligning datasets.");
                PerformAlignment(m_analysis);

                UpdateStatus("Performing clustering.");
                PerformClustering(m_analysis, m_algorithms.Clusterer);

                UpdateStatus("Performing Peak Matching.");
                PerformPeakMatching();

                UpdateStatus(string.Format("Analysis {0} Completed.", m_analysis.MetaData.AnalysisName));
            }
            catch (OutOfMemoryException ex)
            {
                if (AnalysisError != null)
                {
                    AnalysisError(this, new AnalysisErrorEventArgs("Out of memory.", ex));
                }
                return;
            }
            catch (Exception ex)
            {
                if (AnalysisError != null)
                {
                    AnalysisError(this, new AnalysisErrorEventArgs("Handled Error. ", ex));
                }
                return;
            }
           

            if (AnalysisComplete != null)
            {
                AnalysisComplete(this, new AnalysisCompleteEventArgs(m_analysis));
            }            
        }
        #endregion     
    }
}
