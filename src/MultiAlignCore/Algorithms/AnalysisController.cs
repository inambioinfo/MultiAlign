﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MultiAlignCore;
using MultiAlignCore.Algorithms;
using MultiAlignCore.Algorithms.Clustering;
using MultiAlignCore.Algorithms.Features;
using MultiAlignCore.Data;
using MultiAlignCore.Data.MassTags;
using MultiAlignCore.IO;
using MultiAlignCore.IO.Features;
using MultiAlignCore.IO.Features.Hibernate;
using MultiAlignCore.IO.InputFiles;
using MultiAlignCore.IO.MTDB;
using System.Threading;
using MultiAlignCore.IO.Parameters;
using PNNLOmics.Data.Features;
using System.ComponentModel;
using MultiAlignCore.Extensions;
using System.Collections.ObjectModel;
using PNNLOmics.Data;
using System.Text;


namespace MultiAlignCore.Algorithms
{
    /// <summary>
    /// Builds an analysis object and the required dependencies for processing.
    /// </summary>
    public class AnalysisController
    {
        public event EventHandler AnalysisComplete;
        public event EventHandler AnalysisError;
        public event EventHandler AnalysisCancelled;

        #region Analysis Config and Reporting     
        private IAnalysisReportGenerator        m_reportCreator;
        private AnalysisConfig                  m_config;
        Dictionary<int, int> m_chargeMap;
        #endregion

        public AnalysisController()
        {
            m_config        = null;
            m_reportCreator = null;
            m_chargeMap     = new Dictionary<int, int>();   
        }

        #region Properties
        /// <summary>
        /// Gets or sets the processor associated with this analysis.
        /// </summary>
        public MultiAlignAnalysisProcessor Processor
        {
            get;
            private set;
        }
        /// <summary>
        /// Gets or sets the 
        /// </summary>
        public AnalysisConfig Config
        {
            get
            {
                return m_config;
            }
            set
            {
                m_config = value;
            }
        }
        #endregion

        #region Data Provider Setup
        /// <summary>
        /// Sets up the NHibernate caches for storing and retrieving data.
        /// </summary>
        /// <param name="analysisPath"></param>
        /// <returns></returns>
        private  FeatureDataAccessProviders SetupDataProviders(string path, bool createNew)
        {
            try
            {
                return DataAccessFactory.CreateDataAccessProviders(path, createNew);
            }
            catch (System.IO.IOException ex)
            {
                Logger.PrintMessage("Could not access the database.  Is it opened somewhere else?");
                throw ex;
            }
        }
        /// <summary>
        /// Creates data providers to the database of the analysis name and path provided.
        /// </summary>
        /// <returns></returns>
        private  FeatureDataAccessProviders SetupDataProviders(bool createNewDatabase)
        {
            FeatureDataAccessProviders providers = null;
            Logger.PrintMessage("Setting up data providers for caching and storage.");
            try
            {
                string path = AnalysisPathUtils.BuildAnalysisName(m_config.AnalysisPath, m_config.AnalysisName);
                providers = SetupDataProviders(path, createNewDatabase);
            }
            catch (System.IO.IOException ex)
            {
                Logger.PrintMessage(ex.Message);
                Logger.PrintMessage(ex.StackTrace);
                throw ex;
            }
            return providers;
        }
        /// <summary>
        /// Cleans up the old database providers.
        /// </summary>
        private  void CleanupDataProviders()
        {
            NHibernateUtil.Dispose();
        }
        #endregion

        #region Processor Event Handlers
        /// <summary>
        /// Terminates the application when the analysis is complete.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private  void processor_AnalysisComplete(object sender, AnalysisCompleteEventArgs e)
        {
            m_config.Report.PushEndHeader();
            ExportData(m_config.Analysis.DataProviders, AnalysisPathUtils.BuildAnalysisName(m_config.AnalysisPath, m_config.AnalysisName), e.Analysis.MetaData.Datasets.ToList());            
            m_config.triggerEvent.Set();
        }
        /// <summary>
        /// Extractor progress.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void extractor_Progress(object sender, PNNLOmics.Algorithms.ProgressNotifierArgs e)
        {
            Logger.PrintMessage(e.Message, true);
        }
        /// <summary>
        /// Logs when features are aligned.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void processor_FeaturesAligned(object sender, FeaturesAlignedEventArgs e)
        {
            Logger.PrintMessage("Creating feature alignment plots.");
            m_reportCreator.CreateAlignmentPlots(e);

            ReportPeptideFeatures(e.AligneeDatasetInformation, e.AlignedFeatures);
        }
        private void ReportPeptideFeatures(DatasetInformation information, List<UMCLight> features)
        {
            if (m_config.ShouldCreatePeptideScanFiles)
            {
                string path = Path.Combine(m_config.AnalysisPath, information.DatasetName + "_peptide_scans.csv");
                PeptideScanWriter writer = new PeptideScanWriter();
                writer.Write(path, features);
            }
        }
        /// <summary>
        /// Logs when features are clustered.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void processor_FeaturesClustered(object sender, FeaturesClusteredEventArgs e)
        {
            if (m_config.ShouldCreatePlots)
            {
                m_reportCreator.CreateClusterPlots(e);
            }

            if (m_config.ShouldCreateChargeStatePlots)
            {
                m_reportCreator.CreateChargePlots(m_chargeMap);
            }
        }
        /// <summary>
        /// Logs when features are peak matched.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void processor_FeaturesPeakMatched(object sender, FeaturesPeakMatchedEventArgs e)
        {
            Logger.PrintMessage("Creating peak match plots");
            if (m_config.ShouldCreatePlots)
            {
                m_reportCreator.CreatePeakMatchedPlots(e);
            }
        }
        /// <summary>
        /// Logs when features are loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void processor_FeaturesLoaded(object sender, FeaturesLoadedEventArgs e)
        {
            Logger.PrintMessage(string.Format("Loaded {0} features from {1}", e.Features.Count, e.DatasetInformation.DatasetName));
            foreach (UMCLight feature in e.Features)
            {
                int charge = feature.ChargeState;
                if (!m_chargeMap.ContainsKey(charge))
                {
                    m_chargeMap.Add(charge, 0);
                }
                m_chargeMap[charge]++;
            }
        }
        void processor_MassTagsLoaded(object sender, MassTagsLoadedEventArgs e)
        {
            m_reportCreator.CreateMassTagPlot(e);            
        }
        /// <summary>
        /// Logs status messages.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void processor_Status(object sender, AnalysisStatusEventArgs e)
        {
            Logger.PrintMessage(e.StatusMessage);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void processor_FeaturesExtracted(object sender, MultiAlignCore.Algorithms.MSLinker.FeaturesExtractedEventArgs e)
        {
            string extractionPath = Path.Combine(m_config.AnalysisPath, m_config.ExporterNames.ClusterMSMSPath);

            FeatureExtractionTableWriter writer = new MultiAlignCore.IO.Features.FeatureExtractionTableWriter();
            writer.WriteData(extractionPath, e);
        }
        #endregion

        #region Processor Events
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void processor_AnalysisError(object sender, AnalysisErrorEventArgs e)
        {
            Logger.PrintMessage(string.Format("There was an error while performing the analysis.  {0} : {1}", e.ErrorMessage, e.Exception.Message));
            if (e.Exception.StackTrace != null)
            {
                Logger.PrintMessage(string.Format("\n{0}", e.Exception.StackTrace));
            }
            m_config.errorException = e.Exception;

            try
            {
                m_config.errorEvent.Set();
            }
            catch
            { }
        }
        #endregion

        #region Help
        /// <summary>
        /// Prints the help message.
        /// </summary>
        private void PrintHelp()
        {
            Logger.PrintMessage(" ", false);
            Logger.PrintMessage("usage: MultiAlignConsole [options]", false);
            Logger.PrintMessage(" ", false);
            Logger.PrintMessage(" Input File Format Notes: ", false);
            Logger.PrintMessage("    MS Feature Input Files -  MultiAlign needs a separate file to determine what deisotoped MS features or LCMS Features", false);
            Logger.PrintMessage("                              to load.", false);
            Logger.PrintMessage("                              You can use these file types:", false);
            Logger.PrintMessage("                                  *LCMSFeatures.txt", false);
            Logger.PrintMessage("                                  *_isos.csv", false);
            Logger.PrintMessage("                      Single Dataset - (You must specify a database to align to and peak match with.)", false);
            Logger.PrintMessage("                              [Files]", false);
            Logger.PrintMessage("                              pathOfFile_isos.csv", false);
            Logger.PrintMessage("                      Multiple Dataset - (If you don't specify a baseline database, you must specify a database to align to.)", false);
            Logger.PrintMessage("                              [Files]", false);
            Logger.PrintMessage("                              pathOfFile1_isos.csv", false);
            Logger.PrintMessage("                              pathOfFile2_isos.csv", false);
            Logger.PrintMessage("                      Specifying a baseline - This is done by placing an asterisk after one of the dataset names.", false);
            Logger.PrintMessage("                      (NOTE: If you do not specify a baseline, a database must be used for alignment)", false);
            Logger.PrintMessage("                              [Files]", false);
            Logger.PrintMessage("                              pathOfFile1_isos.csv", false);
            Logger.PrintMessage("                              pathOfFile2_isos.csv*", false);
            Logger.PrintMessage("                      Specifying a baseline - This is done by placing an asterisk after one of the dataset names.", false);
            Logger.PrintMessage("                              [Files]", false);
            Logger.PrintMessage("                              pathOfFile1_isos.csv", false);
            Logger.PrintMessage("                              pathOfFile2_isos.csv*", false);
            Logger.PrintMessage("    MS/MS Data Linking - linking MS Features to MS/MS spectra can be done by specifying the RAW dataset files.", false);
            Logger.PrintMessage("                         This currently only works for 32-bit (x86) versions and with Thermo Finnigan data files.", false);
            Logger.PrintMessage("                         To use this feature, you must have matching dataset (MS Feature Input Files) file ", false);
            Logger.PrintMessage("                         names (extension excluded).", false);
            Logger.PrintMessage("                              [Raw]", false);
            Logger.PrintMessage("                              pathOfFile1.Raw", false);
            Logger.PrintMessage("    Peak Matching -   To perform peak matching to an Accurate Mass and Time Tag Database (AMT DB) you need to specify", false);
            Logger.PrintMessage("                      the name of the database in the input file. ", false);
            Logger.PrintMessage("                      To do this with a local SQLite database use: ", false);
            Logger.PrintMessage("                              [Database]", false);
            Logger.PrintMessage("                              sqlite = pathOfDatabase.mdb", false);
            Logger.PrintMessage("                      To use a meta-sample text file (comma separated): ", false);
            Logger.PrintMessage("                              [Database]", false);
            Logger.PrintMessage("                              metasample = path to meta sample file", false);
            Logger.PrintMessage("                      To use one of the Mass Tag System's (MTS) databases use - PNNL Only: ", false);
            Logger.PrintMessage("                              [Database]", false);
            Logger.PrintMessage("                              database = nameOfDatabase", false);
            Logger.PrintMessage("                              server   = serverDatabaseLivesOn", false);
            Logger.PrintMessage("[Options]", false);
            Logger.PrintMessage(" ", false);
            Logger.PrintMessage("   -files  inputFile.txt ", false);
            Logger.PrintMessage("          ASCII Text file with input file names.", false);
            Logger.PrintMessage("          In list of files use asterik to indicate the baseline choice, e.g. 'dataset *'", false);
            Logger.PrintMessage("   -name analysisName  ", false);
            Logger.PrintMessage("          Name to give analysis.", false);
            Logger.PrintMessage("   -log logPath.txt", false);
            Logger.PrintMessage("          Path to provide for log files.", false);
            Logger.PrintMessage("   -h", false);
            Logger.PrintMessage("          Prints this help message.", false);
            Logger.PrintMessage("   -help", false);
            Logger.PrintMessage("          Prints this help message.", false);
            Logger.PrintMessage("   -html htmlPathName.html", false);
            Logger.PrintMessage("          Name to give output HTML plot file.", false);
            Logger.PrintMessage("   -params parameterFile.xml  ", false);
            Logger.PrintMessage("          XML file defining MultiAlign parameters.", false);
            Logger.PrintMessage("   -path  AnalysisPath      ", false);
            Logger.PrintMessage("          File directory of where to put MultiAlign output.  Can be relative or absolute.", false);
            Logger.PrintMessage("   -centroid      ", false);
            Logger.PrintMessage("          To use centroid distance as clustering algorithm.", false);
            Logger.PrintMessage("   -factors factorFilePath", false);
            Logger.PrintMessage("          Path to factor definition file to be loaded.", false);
            Logger.PrintMessage("   -exportClusters clusterFileName     ", false);
            Logger.PrintMessage("          Exports clusters and their LC-MS features to the file name specified.  This file will be sent to the analysis path folder you specified.", false);
            Logger.PrintMessage("   -exportCrossTab  crossTabFileName     ", false);
            Logger.PrintMessage("          Exports clusters and their LC-MS features in cross tab fashion.  Each row is a cluster.  No mass tags are exported.  This file will be sent to the analysis path folder you specified.", false);
            Logger.PrintMessage("   -exportAbundances  crossTabFileName     ", false);
            Logger.PrintMessage("          Exports cluster ids and the abundances of their LC-MS features in cross tab fashion.  Each row is a cluster.  No mass tags are exported.  This file will be sent to the analysis path folder you specified.", false);
            Logger.PrintMessage("   -exportDTA      ", false);
            Logger.PrintMessage("          Exports all MS/MS spectra in the DTA format.", false);
            Logger.PrintMessage("   -plots   [databaseName]  ", false);
            Logger.PrintMessage("          Creates plots for final analysis.  If [databaseName] specified when not running analysis, this will create plots post-analysis.", false);
            Logger.PrintMessage("   -useFactors ", false);
            Logger.PrintMessage("          Flags MultiAlign to use factors.  If no factor file is provided, then MultiAlign will attempt to contact DMS -- PNNL Network only.", false);
        }/// <summary>
        /// Writes the parameters to the log file and database.
        /// </summary>
        /// <param name="analysis"></param>
        private void PrintParameters(MultiAlignAnalysis analysis, bool insertIntoDatabase)
        {
            Logger.PrintMessage("Parameters Loaded");
            Dictionary<string, object> options = new Dictionary<string, object>();
            options.Add("MS Linker Options", analysis.Options.MSLinkerOptions);
            options.Add("UMC Finding Options", analysis.Options.FeatureFindingOptions);
            options.Add("Feature Filtering Options", analysis.Options.FeatureFilterOptions);
            options.Add("Mass Tag Database Options", analysis.Options.MassTagDatabaseOptions);
            options.Add("Alignment Options", analysis.Options.AlignmentOptions);
            options.Add("Drift Time Alignment Options", analysis.Options.DriftTimeAlignmentOptions);
            options.Add("Cluster Options", analysis.Options.ClusterOptions);
            options.Add("STAC Options", analysis.Options.STACOptions);

            List<ParameterHibernateMapping> allmappings = new List<MultiAlignCore.IO.Parameters.ParameterHibernateMapping>();
            foreach (string key in options.Keys)
            {
                object o = options[key];
                Logger.PrintMessage(key, true);
                List<string> parameters = ParameterUtility.ConvertParameterObjectToStrings(o);
                foreach (string parameter in parameters)
                {
                    Logger.PrintMessage("\t" + parameter, true);
                }

                List<ParameterHibernateMapping> mappings = ParameterUtility.ExtractParameterMapObjects(o, key);
                allmappings.AddRange(mappings);
            }

            ParameterHibernateMapping assemblyMap = new ParameterHibernateMapping();
            string assemblyData = ApplicationUtility.GetAssemblyData();
            assemblyMap.OptionGroup = "Assembly Info";
            assemblyMap.Parameter = "Version";
            assemblyMap.Value = assemblyData;
            allmappings.Add(assemblyMap);

            ParameterHibernateMapping systemMap = new ParameterHibernateMapping();
            string systemData = ApplicationUtility.GetSystemData();
            systemMap.OptionGroup = "Assembly Info";
            systemMap.Parameter = "System Info";
            systemMap.Value = systemData;
            allmappings.Add(systemMap);

            if (insertIntoDatabase)
            {
                Logger.PrintMessage("Writing parameters to the analysis database.");
                GenericDAOHibernate<ParameterHibernateMapping> parameterCache = new GenericDAOHibernate<MultiAlignCore.IO.Parameters.ParameterHibernateMapping>();
                parameterCache.AddAll(allmappings);
            }
        }

        #endregion
        
        #region Exporting
        private int ExportData(FeatureDataAccessProviders providers,
                                string databasePath,
                                List<DatasetInformation> datasets)
        {
            if (m_config.ClusterExporters.Count > 0)
            {
                List<UMCClusterLight> clusters = providers.ClusterCache.FindAll();                                                        
                if (clusters.Count < 1)
                {
                    Logger.PrintMessage("No clusters present in the database.");
                    return 1;
                }
                List<UMCLight> features = providers.FeatureCache.FindAllClustered();

                Dictionary<int, UMCClusterLight> clusterFeatureMap = new Dictionary<int, UMCClusterLight>();
                foreach (UMCClusterLight cluster in clusters)
                {
                    clusterFeatureMap[cluster.ID] = cluster;
                }
                foreach (UMCLight umc in features)
                {
                    clusterFeatureMap[umc.ClusterID].AddChildFeature(umc);
                }

                Logger.PrintMessage("Checking for mass tag matches");
                List<ClusterToMassTagMap> clusterMatches = providers.MassTagMatches.FindAll();

                Logger.PrintMessage("Checking for mass tags");
                List<PNNLOmics.Data.MassTags.MassTagLight> massTags = providers.MassTags.FindAll();

                Dictionary<int, List<ClusterToMassTagMap>> clusterMap = new Dictionary<int, List<ClusterToMassTagMap>>();
                if (clusterMatches.Count > 0)
                {
                    foreach (ClusterToMassTagMap map in clusterMatches)
                    {
                        if (!clusterMap.ContainsKey(map.ClusterId))
                        {
                            clusterMap.Add(map.ClusterId, new List<ClusterToMassTagMap>());
                        }
                        clusterMap[map.ClusterId].Add(map);
                    }
                }

                Dictionary<string, PNNLOmics.Data.MassTags.MassTagLight> tags = new Dictionary<string, PNNLOmics.Data.MassTags.MassTagLight>();
                if (massTags.Count > 0)
                {
                    foreach (PNNLOmics.Data.MassTags.MassTagLight tag in massTags)
                    {
                        string key = tag.ConformationID + "-" + tag.ID;
                        if (!tags.ContainsKey(key))
                        {
                            tags.Add(key, tag);
                        }
                    }
                }

                Logger.PrintMessage("Exporting Data");
                foreach (IFeatureClusterWriter writer in m_config.ClusterExporters)
                {
                    Logger.PrintMessage("Exporting in " + writer.ToString() + " format to " + m_config.AnalysisPath);
                    writer.WriteClusters(clusters,
                                         clusterMap,
                                         datasets,
                                         tags);
                }
            }
            return 0;
        }
        #endregion
        
        #region Construction
        private  void ConstructPlotPath()
        {
            Logger.PrintMessage("Creating Plot Thumbnail Path");
            // set the plot save path.
            m_config.plotSavePath = Path.Combine(m_config.AnalysisPath, m_reportCreator.PlotPath);

            // Find out where it's located.
            if (!Directory.Exists(m_config.plotSavePath))
            {
                Directory.CreateDirectory(m_config.plotSavePath);
            }
        }
        /// <summary>
        /// Creates the analysis processor and synchronizs the events.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="providers"></param>
        /// <returns></returns>
        private  MultiAlignAnalysisProcessor ConstructAnalysisProcessor(AlgorithmBuilder builder, FeatureDataAccessProviders providers)
        {
            MultiAlignAnalysisProcessor processor = new MultiAlignAnalysisProcessor();
            processor.AnalysisError         += new EventHandler<AnalysisErrorEventArgs>(processor_AnalysisError);
            processor.FeaturesAligned       += new EventHandler<FeaturesAlignedEventArgs>(processor_FeaturesAligned);
            processor.FeaturesLoaded        += new EventHandler<FeaturesLoadedEventArgs>(processor_FeaturesLoaded);
            processor.MassTagsLoaded        += new EventHandler<MassTagsLoadedEventArgs>(processor_MassTagsLoaded);
            processor.FeaturesClustered     += new EventHandler<FeaturesClusteredEventArgs>(processor_FeaturesClustered);
            processor.FeaturesPeakMatched   += new EventHandler<FeaturesPeakMatchedEventArgs>(processor_FeaturesPeakMatched);
            processor.AnalysisComplete      += new EventHandler<AnalysisCompleteEventArgs>(processor_AnalysisComplete);
            processor.Status                += new EventHandler<AnalysisStatusEventArgs>(processor_Status);            
            processor.BaselineFeaturesLoaded += new EventHandler<BaselineFeaturesLoadedEventArgs>(processor_BaselineFeaturesLoaded);
            processor.FeaturesExtracted     += new EventHandler<MultiAlignCore.Algorithms.MSLinker.FeaturesExtractedEventArgs>(processor_FeaturesExtracted);
            m_config.Analysis.DataProviders  = providers;
            processor.AlgorithmProviders     = builder.GetAlgorithmProvider(m_config.Analysis.Options);

            return processor;
        }

        void processor_BaselineFeaturesLoaded(object sender, BaselineFeaturesLoadedEventArgs e)
        {
            m_reportCreator.CreateBaselinePlots(e);

            if (e.DatasetInformation != null && e.Features != null)
            {
                ReportPeptideFeatures(e.DatasetInformation, e.Features);
            }
        }

        /// <summary>
        /// Sets up the analysis essentials including analysis path, log path, and prints the version and parameter information
        /// to the log.
        /// </summary>
        private  void SetupAnalysisEssentials()
        {
            // Create the analysis path and log file paths.
            ConstructAnalysisPath();
            string dateSuffix = ConstructLogPath();
            
            // Log the version information to the log.
            Logger.PrintVersion();
            Logger.PrintSpacer();

            // Build Plot Path
            m_config.plotSavePath = AnalysisPathUtils.BuildPlotPath(m_config.AnalysisPath);

            // Build analysis name                  
            bool containsExtensionDB3 = m_config.AnalysisName.EndsWith(".db3");
            if (!containsExtensionDB3)
            {
                m_config.AnalysisName += ".db3";
            }

            // create application and analysis.
            Logger.PrintMessage("Starting MultiAlign Console Application.");
            Logger.PrintMessage("Creating analysis: ");
            Logger.PrintMessage("\t" + m_config.AnalysisName);
            Logger.PrintMessage("Storing analysis: ");
            Logger.PrintMessage("\t" + Path.GetFullPath(m_config.AnalysisPath));
            if (m_config.InputPaths != null && m_config.InputPaths.Length > 0)
            {
                Logger.PrintMessage("Using Files:  ");
                Logger.PrintMessage("\tFull Path: " + Path.GetFullPath(m_config.InputPaths));
                Logger.PrintMessage("\tFile Name: " + Path.GetFileName(m_config.InputPaths));
            }
            else
            {
                Logger.PrintMessage("No input files specified.");
            }

            if (m_config.ParameterFile != null)
            {
                Logger.PrintMessage("Using Parameters: ");
                Logger.PrintMessage("\tFull Path: " + Path.GetFullPath(m_config.ParameterFile));
                Logger.PrintMessage("\tFile Name: " + Path.GetFileName(m_config.ParameterFile));
            }
            else
            {
                Logger.PrintMessage("No parameter file specified.");
            }
        }
        private  void ReadParameterFile()
        {
            // Setup the parameters.
            Logger.PrintMessage("Loading parameters.");
            // Make sure we have parameters!
            if (!File.Exists(m_config.ParameterFile))
            {
                Logger.PrintMessage("The parameter file does not exist.");
                //return 1;
            }
            XMLParamterFileReader reader = new XMLParamterFileReader();
            MultiAlignAnalysis analysis = m_config.Analysis;
            reader.ReadParameterFile(m_config.ParameterFile, ref analysis);
        }
        private  string ConstructLogPath()
        {
            // Create the LOG FILE.
            string dateSuffix = AnalysisPathUtils.BuildDateSuffix();
            if (m_config.logPath == null)
            {
                m_config.logPath = AnalysisPathUtils.BuildLogPath(m_config.AnalysisPath,
                                                            m_config.AnalysisName,
                                                            dateSuffix);                
            }
            else
            {                
                m_config.logPath = Path.Combine(m_config.AnalysisPath,
                                         m_config.logPath,
                                         dateSuffix);                
            }
            Logger.LogPath = m_config.logPath;
            return dateSuffix;
        }
        private  void ConstructAnalysisPath()
        {
            //Create the analysis directory.
            if (!Directory.Exists(m_config.AnalysisPath))
            {
                Logger.PrintMessage("Creating analysis path " + m_config.AnalysisPath);
                Directory.CreateDirectory(m_config.AnalysisPath);
            }
            else
            {
                Logger.PrintMessage("Analysis path " + m_config.AnalysisPath + " already exists.");
            }
        }
        private  MultiAlignAnalysis ConstructAnalysisObject(InputAnalysisInfo analysisSetupInformation)
        {
            Logger.PrintMessage("Creating Analysis Objects.");
            MultiAlignAnalysis analysis             = new MultiAlignAnalysis();
            analysis.MetaData.AnalysisPath          = m_config.AnalysisPath;
            analysis.MetaData.AnalysisName          = m_config.AnalysisName;
            analysis.Options.AlignmentOptions.IsAlignmentBaselineAMasstagDB = true;

            analysis.MetaData.ParameterFile         = m_config.ParameterFile;
            analysis.MetaData.InputFileDefinition   = m_config.InputPaths;
            analysis.MetaData.AnalysisSetupInfo     = analysisSetupInformation;
            return analysis;
        }
        private MultiAlignAnalysis ConstructAnalysisObject(FeatureDataAccessProviders providers)
        {
            Logger.PrintMessage("Creating Analysis Objects.");
            MultiAlignAnalysis analysis             = new MultiAlignAnalysis();
            
            // Need to extract from the database.
            analysis.MetaData.AnalysisPath          = m_config.AnalysisPath;
            analysis.MetaData.AnalysisName          = m_config.AnalysisName;
            analysis.Options.AlignmentOptions.IsAlignmentBaselineAMasstagDB = true;            
            analysis.MetaData.ParameterFile         = m_config.ParameterFile;
            analysis.MetaData.InputFileDefinition   = m_config.InputPaths;                        
            analysis.MetaData.AnalysisSetupInfo     = null;
            analysis.DataProviders                  = providers;
            analysis.Clusters                       = providers.ClusterCache.FindAll();
            analysis.MetaData.Datasets              = providers.DatasetCache.FindAll().ToObservableCollection();

            MassTagDatabaseLoaderCache provider     = new MassTagDatabaseLoaderCache();
            provider.Provider                       = analysis.DataProviders.MassTags;
            analysis.MassTagDatabase                = provider.LoadDatabase();
            
            return analysis;
        }
        private  bool ReadInputDefinitionFile(out InputAnalysisInfo analysisSetupInformation, out bool useMTDB)
        {
            // Read the input datasets.
            if (!File.Exists(m_config.InputPaths))
            {
                Logger.PrintMessage(string.Format("The input file {0} does not exist.", m_config.InputPaths));
                //return 1;
            }
            else
            {
                Logger.PrintMessage("Copying input file to output directory.");
                try
                {
                    string dateSuffix = AnalysisPathUtils.BuildDateSuffix();
                    string newPath = Path.GetFileNameWithoutExtension(m_config.InputPaths);
                    newPath = newPath + "_" + dateSuffix + ".txt";
                    File.Copy(m_config.InputPaths, Path.Combine(m_config.AnalysisPath, newPath));
                }
                catch (Exception ex)
                {
                    Logger.PrintMessage("Could not copy the input file to the output directory.  " + ex.Message);
                }
            }

            Logger.PrintMessage("Parsing Input Filenames and Databases.");
            useMTDB = false;
            analysisSetupInformation = null;
            try
            {
                analysisSetupInformation = MultiAlignFileInputReader.ReadInputFile(m_config.InputPaths);
            }
            catch (Exception ex)
            {
                Logger.PrintMessage("The input file had some bad lines in it.  " + ex.Message);
                return false;
            }
            Logger.PrintMessage("Found " + analysisSetupInformation.Files.Count.ToString() + " files.");

            // Validate the mass tag database settings.
            try
            {
                useMTDB = analysisSetupInformation.Database.ValidateDatabaseType();
            }
            catch (AnalysisMTDBSetupException ex)
            {
                Logger.PrintMessage("There was a problem with the mass tag database specification.  " + ex.Message);
                return false;
            }
            return true;
        }
        private  void ExportParameterFile()
        {
            // Output the settings to INI for viewing.
            string outParamName = Path.GetFileNameWithoutExtension(m_config.ParameterFile);
            string outParamPath = Path.Combine(m_config.AnalysisPath, outParamName);
            XMLParameterFileWriter xmlWriter = new XMLParameterFileWriter();
            xmlWriter.WriteParameterFile(outParamPath + ".xml", m_config.Analysis);
        }
        /// <summary>
        /// Constructs the baseline databases.
        /// </summary>
        /// <param name="analysisSetupInformation"></param>
        /// <param name="useMTDB"></param>
        private  bool ConstructBaselines(InputAnalysisInfo analysisSetupInformation, AnalysisMetaData analysisMetaData, bool useMTDB)
        {
            Logger.PrintMessage("Confirming baseline selections.");
            if (useMTDB)
            {
                switch (analysisSetupInformation.Database.DatabaseFormat)
                {
                    case MassTagDatabaseFormat.APE:
                        Logger.PrintMessage("Using local APE Cache Mass Tag Database at location: ");
                        Logger.PrintMessage(string.Format("\tFull Path: {0}", analysisSetupInformation.Database.LocalPath));
                        Logger.PrintMessage(string.Format("\tDatabase Name: {0}", Path.GetFileName(analysisSetupInformation.Database.LocalPath)));
                        m_config.Analysis.MetaData.Database = analysisSetupInformation.Database;
                        break;                                               
                    case MassTagDatabaseFormat.SQL:
                        Logger.PrintMessage("Using Mass Tag Database:");
                        Logger.PrintMessage(string.Format("\tServer:        {0}", analysisSetupInformation.Database.DatabaseServer));
                        Logger.PrintMessage(string.Format("\tDatabase Name: {0}", analysisSetupInformation.Database.DatabaseName));
                        m_config.Analysis.MetaData.Database = analysisSetupInformation.Database;
                        break;
                    case MassTagDatabaseFormat.Sqlite:
                        Logger.PrintMessage("Using local Sqlite Mass Tag Database at location: ");
                        Logger.PrintMessage(string.Format("\tFull Path: {0}", analysisSetupInformation.Database.LocalPath));
                        Logger.PrintMessage(string.Format("\tDatabase Name: {0}", Path.GetFileName(analysisSetupInformation.Database.LocalPath)));

                        m_config.Analysis.MetaData.Database = analysisSetupInformation.Database;
                        break;
                    case MassTagDatabaseFormat.MetaSample:
                        Logger.PrintMessage("Using local MetaSample Mass Tag Database at location: ");
                        Logger.PrintMessage(string.Format("\tFull Path: {0}", analysisSetupInformation.Database.LocalPath));
                        Logger.PrintMessage(string.Format("\tDatabase Name: {0}", Path.GetFileName(analysisSetupInformation.Database.LocalPath)));
                        m_config.Analysis.MetaData.Database = analysisSetupInformation.Database;
                        break;
                }

                // Validate the baseline
                if (analysisSetupInformation.BaselineFile == null)
                {
                    m_config.Analysis.Options.AlignmentOptions.IsAlignmentBaselineAMasstagDB = true;
                    Logger.PrintMessage(string.Format("Using mass tag database {0} as the alignment baseline.", analysisSetupInformation.Database.DatabaseName));
                }
                else
                {
                    m_config.Analysis.Options.AlignmentOptions.IsAlignmentBaselineAMasstagDB = false;
                    m_config.Analysis.MetaData.BaselineDataset = null;
                    foreach (DatasetInformation info in analysisMetaData.Datasets)
                    {
                        if (info.Features.Path == analysisSetupInformation.BaselineFile.Path)
                        {
                            m_config.Analysis.MetaData.BaselineDataset  = info;
                            info.IsBaseline                             = true;
                            m_config.Analysis.DataProviders.DatasetCache.Update(info);
                        }
                    }
                    Logger.PrintMessage(string.Format("Using dataset {0} as the alignment baseline.", m_config.Analysis.MetaData.BaselineDataset));
                }
            }
            else
            {
                m_config.Analysis.MetaData.Database                                      = analysisSetupInformation.Database;
                m_config.Analysis.Options.AlignmentOptions.IsAlignmentBaselineAMasstagDB = false;
                if (analysisSetupInformation.BaselineFile == null)
                {
                    Logger.PrintMessage("No baseline dataset or database was selected.");
                    return false;
                }
                else
                {
                    m_config.Analysis.MetaData.BaselineDataset = null;
                    foreach (DatasetInformation info in analysisMetaData.Datasets)
                    {
                        if (info.Features.Path == analysisSetupInformation.BaselineFile.Path)
                        {
                            info.IsBaseline = true;
                            m_config.Analysis.MetaData.BaselineDataset = info;
                            m_config.Analysis.DataProviders.DatasetCache.Update(info);
                        }
                    }
                    Logger.PrintMessage(string.Format("Using dataset {0} as the alignment baseline.", m_config.Analysis.MetaData.BaselineDataset));
                }
            }
            return true;
        }
        /// <summary>
        /// Loads factors from file or other.
        /// </summary>
        /// <param name="analysisSetupInformation"></param>
        /// <param name="datasets"></param>
        private  void ConstructFactorInformation(InputAnalysisInfo analysisSetupInformation, ObservableCollection<DatasetInformation> datasets, FeatureDataAccessProviders providers)
        {
            MultiAlignCore.IO.Factors.MAGEFactorAdapter mage = new MultiAlignCore.IO.Factors.MAGEFactorAdapter();

            if (analysisSetupInformation.FactorFile == null)
            {
                Logger.PrintMessage("Loading Factor Information from DMS");
                mage.LoadFactorsFromDMS(datasets, providers);
            }
            else
            {
                Logger.PrintMessage("Loading Factor Information from file: " + analysisSetupInformation.FactorFile);
                mage.LoadFactorsFromFile(analysisSetupInformation.FactorFile, datasets, providers);
            }
        }
        /// <summary>
        /// Constructs dataset infromation from the input analysis information.
        /// </summary>
        /// <param name="analysisSetupInformation"></param>
        /// <param name="analysis"></param>
        private  void ConstructDatasetInformation(InputAnalysisInfo analysisSetupInformation, MultiAlignAnalysis analysis, bool insertIntoDatabase)
        {
            // Create dataset information.
            Logger.PrintMessage("Creating dataset and other input information.");

            List<DatasetInformation> datasets = DatasetInformation.CreateDatasetsFromInputFile(analysisSetupInformation.Files);
            analysis.MetaData.Datasets.AddRange(datasets);
            
            if (insertIntoDatabase)
            {                                
                m_config.Analysis.DataProviders.DatasetCache.AddAll(datasets);
            }
        }

        
        /// <summary>
        /// Determine what exporting features need to be had.
        /// </summary>
        private  void ConstructExporting()
        {
            if (m_config.ExporterNames.ClusterScanPath != null)
            {
                m_config.ClusterExporters.Add(new UMCClusterScanWriter(Path.Combine(m_config.AnalysisPath, m_config.ExporterNames.ClusterScanPath)));
            }
            if (m_config.ExporterNames.CrossTabPath != null)
            {
                UMCClusterCrossTabWriter writer = new UMCClusterCrossTabWriter(Path.Combine(m_config.AnalysisPath, m_config.ExporterNames.CrossTabPath));
           
                MultiAlignCore.Algorithms.FeatureFinding.AbundanceReportingType reporting    = MultiAlignCore.Algorithms.FeatureFinding.AbundanceReportingType.Sum;
                MultiAlignCore.Algorithms.FeatureFinding.AbundanceReportingType umcAbundance = MultiAlignCore.Algorithms.FeatureFinding.AbundanceReportingType.Sum;
                if (m_config.Analysis != null)
                {                    
                    reporting    = m_config.Analysis.Options.ConsolidationOptions.AbundanceType;
                    umcAbundance = m_config.Analysis.Options.FeatureFindingOptions.UMCAbundanceReportingType;
                }
                writer.Consolidator = FeatureConsolidatorFactory.CreateConsolidator(reporting,
                                                                                    umcAbundance);
                m_config.ClusterExporters.Add(writer);
            }
            if (m_config.ExporterNames.CrossTabAbundance != null)
            {
                //UMCClusterAbundanceCrossTabWriter writer = new UMCClusterAbundanceCrossTabWriter(Path.Combine(m_config.AnalysisPath,
                //                                                                                 m_config.ExporterNames.CrossTabAbundance));


                UMCClusterAbundanceSumCrossTabWriter writerSumOnly = new UMCClusterAbundanceSumCrossTabWriter(Path.Combine(m_config.AnalysisPath,
                                                                                                 m_config.ExporterNames.CrossTabAbundance));

                MultiAlignCore.Algorithms.FeatureFinding.AbundanceReportingType reporting    = MultiAlignCore.Algorithms.FeatureFinding.AbundanceReportingType.Sum;
                MultiAlignCore.Algorithms.FeatureFinding.AbundanceReportingType umcAbundance = MultiAlignCore.Algorithms.FeatureFinding.AbundanceReportingType.Sum;
                if (m_config.Analysis != null)
                {
                    reporting = m_config.Analysis.Options.ConsolidationOptions.AbundanceType;
                    umcAbundance = m_config.Analysis.Options.FeatureFindingOptions.UMCAbundanceReportingType;
                }
                writerSumOnly.Consolidator = FeatureConsolidatorFactory.CreateConsolidator(reporting,
                                                                                    umcAbundance);
                
                m_config.ClusterExporters.Add(writerSumOnly);                
            }
        }
        /// <summary>
        /// Create the clustering algorithms.
        /// </summary>
        /// <param name="builder"></param>
        private  void ConstructClustering(AlgorithmBuilder builder)
        {
            // Setup algorithm providers.
            if (m_config.options.ContainsKey("-centroid"))
            {
                Logger.PrintMessage("Building centroid clusterer");
                builder.BuildClusterer(ClusteringAlgorithmType.Centroid);
            }
            else if (m_config.options.ContainsKey("-singlelinkage"))
            {

                Logger.PrintMessage("Building single linkage clusterer");
                builder.BuildClusterer(ClusteringAlgorithmType.SingleLinkage);
            }
            else
            {
                Logger.PrintMessage("Built average linkage clusterer.");
            }
        }
        #endregion

        #region Cleanup 
        public void CleanupOldAnalysisBranches(AnalysisConfig config)
        {
            switch (config.InitialStep)
            {
                case AnalysisStep.None:
                    break;
                case AnalysisStep.LoadMTDB:
                    break;
                case AnalysisStep.FindFeatures:
                    break;
                case AnalysisStep.Alignment:
                    config.Analysis.DataProviders.FeatureCache.ClearAlignmentData();
                    config.Analysis.DataProviders.ClusterCache.ClearAllClusters();
                    config.Analysis.DataProviders.MassTagMatches.ClearAllMatches();  
                    break;
                case AnalysisStep.Clustering:
                    config.Analysis.DataProviders.ClusterCache.ClearAllClusters();
                    config.Analysis.DataProviders.MassTagMatches.ClearAllMatches();  
                    break;
                case AnalysisStep.ClusterQC:
                    break;
                case AnalysisStep.PeakMatching:
                    config.Analysis.DataProviders.MassTagMatches.ClearAllMatches();                        
                    break;
                default:
                    break;
            }
        }
        #endregion
        private BackgroundWorker m_worker = new BackgroundWorker();

        /// <summary>
        /// This is horrible.  A common ground of feature / functionality should be made here.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="reporter"></param>
        /// <returns></returns>
        public void StartMultiAlignGUI(AnalysisConfig config, IAnalysisReportGenerator reporter)
        { 
            m_worker         = new BackgroundWorker();
            m_worker.DoWork += new DoWorkEventHandler(m_worker_DoWork);

            m_workerManager = new WorkerObject(m_worker);

            m_reportCreator = reporter;
            m_config        = config;

            m_worker.WorkerSupportsCancellation = true;            
            m_worker.RunWorkerAsync();            
        }
        /// <summary>
        /// Main bulk for processing setup for the GUI version
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void m_worker_DoWork(object sender, DoWorkEventArgs e)
        {

            // Builds the list of algorithm providers.
            AlgorithmBuilder builder = new AlgorithmBuilder();

            // Use this to signal when the analysis is done.              
            m_config.triggerEvent = new ManualResetEvent(false);
            m_config.errorEvent = new ManualResetEvent(false);
            m_config.stopEvent = new ManualResetEvent(false);
            m_config.errorException = null;


            m_config.Analysis.MetaData.AnalysisPath = m_config.AnalysisPath;
            m_config.Analysis.MetaData.AnalysisName = m_config.AnalysisName;

            /// /////////////////////////////////////////////////////////////
            /// Setup log path, analysis path, and print version to log file.            
            /// /////////////////////////////////////////////////////////////                        
            SetupAnalysisEssentials();

            /// /////////////////////////////////////////////////////////////
            /// Determine if we have specified a valid database to extract
            /// data from or to re-start an analysis.
            /// /////////////////////////////////////////////////////////////    
            string databasePath = Path.Combine(m_config.AnalysisPath, m_config.AnalysisName);
            bool databaseExists = File.Exists(databasePath);
            bool createDatabase = true;

            createDatabase = ShouldCreateDatabase(m_config.Analysis.AnalysisType, databaseExists);

            // make sure that we were not told to skip to a new part of the analysis.
            if (m_config.InitialStep >= AnalysisStep.Alignment)
            {
                createDatabase = false;
            }


            AnalysisType validated = m_config.Analysis.AnalysisType;
            AnalysisConfig config  = m_config;

            switch (m_config.Analysis.AnalysisType)
            {
                case AnalysisType.FactorImporting:
                    ImportFactors(config, databaseExists);
                    break;
                case AnalysisType.Full:
                    PerformAnalysisGUI(config, builder, validated, createDatabase, m_workerManager);
                    break;
                case AnalysisType.ExportDataOnly:
                    ExportData(config, builder, databasePath, databaseExists);
                    break;
                case AnalysisType.ExportSICs:
                    PerformAnalysisGUI(config, builder, validated, createDatabase, m_workerManager);
                    break;
            }
        }

        

        /// <summary>
        /// Processes the MA analysis data.
        /// </summary>
        /// <param name="args"></param>
        /// 
        public int StartMultiAlign(AnalysisConfig config, IAnalysisReportGenerator reporter)
        {
            m_reportCreator = reporter;
            m_config        = config;

            // Builds the list of algorithm providers.
            AlgorithmBuilder builder = new AlgorithmBuilder();

            // Use this to signal when the analysis is done.              
            config.triggerEvent     = new ManualResetEvent(false);
            config.errorEvent       = new ManualResetEvent(false);
            m_config.stopEvent      = new ManualResetEvent(false);
            config.errorException   = null;

            /// /////////////////////////////////////////////////////////////
            /// Print Help
            /// /////////////////////////////////////////////////////////////
            // See if the user wants help
            if (config.showHelp)
            {
                PrintHelp();
                config.errorEvent.Dispose();
                config.triggerEvent.Dispose();
                return 0;
            }

            /// /////////////////////////////////////////////////////////////
            /// Validate the command line
            /// /////////////////////////////////////////////////////////////            
            AnalysisType validated = AnalysisValidator.ValidateSetup(m_config);
            if (validated == AnalysisType.InvalidParameters)
            {

                PrintHelp();
                return 0;
            }

            /// /////////////////////////////////////////////////////////////
            /// Setup log path, analysis path, and print version to log file.            
            /// /////////////////////////////////////////////////////////////                        
            SetupAnalysisEssentials();

            /// /////////////////////////////////////////////////////////////
            /// Determine if we have specified a valid database to extract
            /// data from or to re-start an analysis.
            /// /////////////////////////////////////////////////////////////    
            string databasePath = Path.Combine(config.AnalysisPath, config.AnalysisName);
            bool databaseExists = File.Exists(databasePath);
            bool createDatabase = true;

            createDatabase = ShouldCreateDatabase(validated, databaseExists);

            // make sure that we were not told to skip to a new part of the analysis.
            if (config.InitialStep >= AnalysisStep.Alignment)
            {
                createDatabase = false;
            }

            int result = 0;
            switch (validated)
            {
                case AnalysisType.FactorImporting:
                    result = ImportFactors(config,  databaseExists);
                    break;
                case AnalysisType.Full:
                    result = PerformAnalysis(config, builder, validated, createDatabase);
                    break;
                case AnalysisType.ExportDataOnly:
                    result = ExportData(config, builder, databasePath, databaseExists);
                    break;
                case AnalysisType.ExportSICs:
                    result = PerformAnalysis(config, builder, validated, createDatabase);
                    break;
            }
            return result;
        }

        /// <summary>
        /// Determines if the database should be created or not.
        /// </summary>
        /// <param name="validated"></param>
        /// <param name="databaseExists"></param>
        /// <returns></returns>
        private static bool ShouldCreateDatabase(AnalysisType validated, bool databaseExists)
        {
            bool createDatabase = true;

            if (validated != AnalysisType.Full && validated != AnalysisType.InvalidParameters)
            {
                if (databaseExists)
                {
                    createDatabase = false;
                }
            }

            return createDatabase;
        }

        /// <summary>
        /// Cancels the analysis.
        /// </summary>
        public void CancelAnalysis()
        {
            if (m_worker != null)
            {
                try
                {
                    // Let's kill the processing threads...
                    m_config.stopEvent.Set();

                    Logger.PrintMessage("Cancelling Analysis.");

                    WaitHandle[] handles = new WaitHandle[] { m_workerManager.SynchEvent };
                    bool waited = System.Threading.WaitHandle.WaitAll(handles, 1000);
                                    
                    // Then the background worker threads.  
                    // Processing thread does all of the post-analysis report generation and is based on the Console
                    // Application way of running and waiting for the analysis to complete.  It's a bit of overhead
                    // threadwise to do it this way from teh GUI perspective but remains in tact to not change
                    // the way processing happens between GUI and Console.

                    // Here we just let the background worker go
                    m_worker = null;
                }
                catch(Exception ex)
                {
                    int xx = 0;
                    xx++;

                }
            }

            if (this.AnalysisCancelled != null)
            {
                AnalysisCancelled(this, null);
            }
        }

        WorkerObject m_workerManager;
        
        #region Processing 
        /// <summary>
        /// Performs the analysis.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="builder"></param>
        /// <param name="providers"></param>
        /// <param name="processor"></param>
        /// <param name="validated"></param>
        /// <param name="analysisSetupInformation"></param>
        /// <param name="createDatabase"></param>
        /// <returns></returns>
        private void PerformAnalysisGUI(AnalysisConfig config, AlgorithmBuilder builder, AnalysisType validated, bool createDatabase, WorkerObject worker)
        {                        
            FeatureDataAccessProviders providers        = null;
            MultiAlignAnalysisProcessor processor       = null;
            
            Logger.PrintMessage("Performing analysis.");
         
            /// /////////////////////////////////////////////////////////////            
            /// Creates or connects to the underlying analysis database.
            /// ///////////////////////////////////////////////////////////// 
            providers = SetupDataProviders(createDatabase);

            /// /////////////////////////////////////////////////////////////
            /// Create the clustering, analysis, and plotting paths.
            /// /////////////////////////////////////////////////////////////                                    
            builder.BuildClusterer(config.Analysis.Options.ClusterOptions.ClusteringAlgorithm);
            config.Analysis.DataProviders   = providers;
            config.Analysis.AnalysisType    = validated;
            ConstructPlotPath();

            ExportParameterFile();
            Logger.PrintSpacer();
            PrintParameters(config.Analysis, createDatabase);
            Logger.PrintSpacer();

            /// /////////////////////////////////////////////////////////////
            /// Setup the processor.
            /// /////////////////////////////////////////////////////////////            
            processor = ConstructAnalysisProcessor(builder, providers);

            // Tell the processor whether to load data or not.
            processor.ShouldLoadData = createDatabase;


            // Construct the dataset information for export.// Create dataset information.
            Logger.PrintMessage("Storing dataset information into the database.");

            List<DatasetInformation> information = new List<DatasetInformation>();
            foreach (DatasetInformation info in config.Analysis.MetaData.Datasets)
            {
                information.Add(info);
            }                        
            m_config.Analysis.DataProviders.DatasetCache.AddAll(information);
            

            // Give the processor somewhere to put the SIC images.
            if (validated == AnalysisType.ExportSICs)
            {
                processor.AnalysisPath = Path.Combine(config.AnalysisPath, "SICs");
                if (!Directory.Exists(processor.AnalysisPath))
                {
                    Directory.CreateDirectory(processor.AnalysisPath);
                }
            }

            Logger.PrintMessage("Creating exporter options.");
            if (config.ExporterNames.CrossTabPath == null)
            {
                config.ExporterNames.CrossTabPath = config.AnalysisName.Replace(".db3", "");
            }
            if (config.ExporterNames.CrossTabAbundance == null)
            {
                config.ExporterNames.CrossTabAbundance = config.AnalysisName.Replace(".db3", "");
            }
            ConstructExporting();

            Logger.PrintMessage("Cleaning up old analysis branches.");
            CleanupOldAnalysisBranches(config);

            Logger.PrintMessage("Analysis Started.");
            processor.StartAnalysis(config);

            int handleID = WaitHandle.WaitAny(new WaitHandle[] { config.triggerEvent, config.errorEvent, config.stopEvent });

            if (handleID == 1)
            {
                Logger.PrintMessageWorker("There was an error during processing.", 1, false);
                config.triggerEvent.Dispose();
                config.errorEvent.Dispose();
                processor.Dispose();

                if (AnalysisError != null)
                {
                    AnalysisError(this, null);
                }
                return;
            }
            else if (handleID == 2)
            {
                Logger.PrintMessageWorker("Stopping the analysis.", 1, false);
                processor.StopAnalysis();

                try
                {
                    // We use this guy to tell the GUI thread that killed us that it's ok to finish cleaning up.
                    worker.SynchEvent.Set();
                    System.Threading.Thread.Sleep(50);
                }
                catch
                {
                }

                config.triggerEvent.Dispose();
                config.errorEvent.Dispose();
                processor.Dispose();

                if (AnalysisCancelled != null)
                {
                    AnalysisCancelled(this, null);
                }
                return;
            }
            else
            {

                try
                {
                    m_reportCreator.CreatePlotReport();
                }
                catch (Exception ex)
                {
                    Logger.PrintMessage("There was an error when trying to create the final analysis plots, however, the data analysis is complete.");
                    Logger.PrintMessage(ex.Message);
                    Logger.PrintMessage(ex.StackTrace);                    
                }
            }
            
            
            config.triggerEvent.Dispose();
            config.errorEvent.Dispose();
            processor.Dispose();
            CleanupDataProviders();

            Logger.PrintMessage("Indexing Database Clusters for Faster Retrieval");
            string databasePath = Path.Combine(m_config.AnalysisPath, m_config.AnalysisName);
            DatabaseIndexer.IndexClusters(databasePath);
            Logger.PrintMessage("Indexing Database Features");
            DatabaseIndexer.IndexFeatures(databasePath);

            Logger.PrintMessage("Analysis Complete");
                                    
            if (AnalysisComplete != null)
            {
                AnalysisComplete(this, null);
            }
        }
        /// <summary>
        /// Performs the analysis.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="builder"></param>
        /// <param name="providers"></param>
        /// <param name="processor"></param>
        /// <param name="validated"></param>
        /// <param name="analysisSetupInformation"></param>
        /// <param name="createDatabase"></param>
        /// <returns></returns>
        private int PerformAnalysis(AnalysisConfig config, AlgorithmBuilder builder, AnalysisType validated, bool createDatabase)
        {
            InputAnalysisInfo analysisSetupInformation = null;
            FeatureDataAccessProviders  providers = null;
            MultiAlignAnalysisProcessor processor = null;

            Logger.PrintMessage("Performing analysis.");

            /// /////////////////////////////////////////////////////////////            
            /// Read the input files.
            /// /////////////////////////////////////////////////////////////                                    
            bool useMTDB = false;
            bool isInputFileOk = ReadInputDefinitionFile(out analysisSetupInformation, out useMTDB);
            if (!isInputFileOk)
                return 1;

            /// /////////////////////////////////////////////////////////////            
            /// Figure out if the factors are defined.
            /// /////////////////////////////////////////////////////////////                                    
            if (config.options.ContainsKey("-factors"))
            {
                Logger.PrintMessage("Factor file specified.");
                string factorFile = config.options["-factors"][0];
                analysisSetupInformation.FactorFile = factorFile;
            }

            /// /////////////////////////////////////////////////////////////            
            /// Creates or connects to the underlying analysis database.
            /// ///////////////////////////////////////////////////////////// 
            providers = SetupDataProviders(createDatabase);

            /// /////////////////////////////////////////////////////////////
            /// Create the clustering, analysis, and plotting paths.
            /// /////////////////////////////////////////////////////////////                                    
            ConstructClustering(builder);

            config.Analysis                 = ConstructAnalysisObject(analysisSetupInformation);
            config.Analysis.DataProviders   = providers;
            config.Analysis.AnalysisType    = validated;
            ConstructPlotPath();

            /// /////////////////////////////////////////////////////////////
            /// Read the parameter files.
            /// /////////////////////////////////////////////////////////////        
            ReadParameterFile();

            /// /////////////////////////////////////////////////////////////
            /// Construct Dataset information
            /// /////////////////////////////////////////////////////////////            
            // Construct the dataset information for export.
            ConstructDatasetInformation(analysisSetupInformation, config.Analysis, createDatabase);

            if (config.ShouldUseFactors)
            {
                ConstructFactorInformation(analysisSetupInformation, config.Analysis.MetaData.Datasets, config.Analysis.DataProviders);
            }

            bool isBaselineSpecified = ConstructBaselines(analysisSetupInformation, config.Analysis.MetaData, useMTDB);
            if (!isBaselineSpecified)
            {
                return 1;
            }

            ExportParameterFile();
            Logger.PrintSpacer();
            PrintParameters(config.Analysis, createDatabase);
            Logger.PrintSpacer();

            /// /////////////////////////////////////////////////////////////
            /// Setup the processor.
            /// /////////////////////////////////////////////////////////////            
            processor = ConstructAnalysisProcessor(builder, providers);

            // Tell the processor whether to load data or not.
            processor.ShouldLoadData = createDatabase;


            // Give the processor somewhere to put the SIC images.
            if (validated == AnalysisType.ExportSICs)
            {
                processor.AnalysisPath = Path.Combine(config.AnalysisPath, "SICs");
                if (!Directory.Exists(processor.AnalysisPath))
                {
                    Directory.CreateDirectory(processor.AnalysisPath);
                }
            }

            Logger.PrintMessage("Creating exporter options.");
            if (config.ExporterNames.CrossTabPath == null)
            {
                config.ExporterNames.CrossTabPath = config.AnalysisName.Replace(".db3", "");
            }
            if (config.ExporterNames.CrossTabAbundance == null)
            {
                config.ExporterNames.CrossTabAbundance = config.AnalysisName.Replace(".db3", "");
            }
            ConstructExporting();

            Logger.PrintMessage("Cleaning up old analysis branches.");
            CleanupOldAnalysisBranches(config);


            /// /////////////////////////////////////////////////////////////
            /// Start the analysis
            /// /////////////////////////////////////////////////////////////            
            Logger.PrintMessage("Analysis Started.");
            processor.StartAnalysis(config);
            int handleID = WaitHandle.WaitAny(new WaitHandle[] { config.triggerEvent, config.errorEvent });
            
            bool wasError = false;

            if (handleID == 1)
            {
                
                Logger.PrintMessage("There was an error during processing.");
                return 1;
            }

            /// /////////////////////////////////////////////////////////////
            /// Finalize the analysis plots etc.
            /// /////////////////////////////////////////////////////////////
            try
            {
                //m_reportCreator.CreateFinalAnalysisPlots(providers.FeatureCache, providers.ClusterCache);
                m_reportCreator.CreatePlotReport();
            }
            catch (Exception ex)
            {
                Logger.PrintMessage("There was an error when trying to create the final analysis plots, however, the data analysis is complete.");
                Logger.PrintMessage(ex.Message);
                Logger.PrintMessage(ex.StackTrace);
            }

            config.Analysis.Dispose();
            config.triggerEvent.Dispose();
            config.errorEvent.Dispose();
            processor.Dispose();
            CleanupDataProviders();

            if (!wasError)
            {
                Logger.PrintMessage("Indexing Database Features");
                DatabaseIndexer.IndexFeatures(config.AnalysisPath);
                DatabaseIndexer.IndexClusters(config.AnalysisPath);
            }

            Logger.PrintMessage("Analysis Complete.");
            return 0;
        }
        private int ExportData(AnalysisConfig config, AlgorithmBuilder builder, string databasePath, bool databaseExists)
        {
            InputAnalysisInfo analysisSetupInformation  = null;
            FeatureDataAccessProviders providers        = null;
            MultiAlignAnalysisProcessor processor       = null;

            Logger.PrintMessage("Exporting data only selected.");
            if (!databaseExists)
            {
                Logger.PrintMessage("The database you specified to extract data from does not exist.");
                return 1;
            }

            // Create access to data.
            providers = SetupDataProviders(false);
            if (providers == null)
            {
                Logger.PrintMessage("Could not create connection to database.");
                return 1;
            }

            // Find all the datasets 
            List<DatasetInformation> datasets = providers.DatasetCache.FindAll();
            if (datasets == null || datasets.Count == 0)
            {
                Logger.PrintMessage("There are no datasets present in the current database.");
                CleanupDataProviders();
                return 1;
            }

            ConstructExporting();
            ExportData(providers, databasePath, datasets);
            CleanupDataProviders();
            return 0;
        }
        private int ImportFactors(AnalysisConfig config,  bool databaseExists)
        {

            FeatureDataAccessProviders providers = null;
            MultiAlignAnalysisProcessor processor = null;

            Logger.PrintMessage("Updating factors");
            if (!databaseExists)
            {
                Logger.PrintMessage("The database you specified to extract data from does not exist.");
                return 1;
            }

            // Create access to data.
            providers = SetupDataProviders(false);
            if (providers == null)
            {
                Logger.PrintMessage("Could not create connection to database.");
                return 1;
            }

            // Find all the datasets 
            List<DatasetInformation> datasetsFactors = providers.DatasetCache.FindAll();
            if (datasetsFactors == null || datasetsFactors.Count == 0)
            {
                Logger.PrintMessage("There are no datasets present in the current database.");
                CleanupDataProviders();
                return 1;
            }

            InputAnalysisInfo info = new InputAnalysisInfo();

            if (config.options.ContainsKey("-factors"))
            {
                Logger.PrintMessage("Factor file specified.");
                string factorFile = config.options["-factors"][0];
                info.FactorFile = factorFile;
            }
            ConstructFactorInformation(info, datasetsFactors.ToObservableCollection(), providers);
            CleanupDataProviders();
            return 0;
        }
        #endregion
    }

    /// <summary>
    /// Class that combines the worker object with a manual reset event used for snychronization.  This 
    /// happens because some processing elements take a long time to perform.  We want to let them finish but dont watn the user interface to freeze.
    /// So instead we create an object that allows us to let the objects go into the deep so that when the processing elements complete, they will manage themselves.
    /// </summary>
    public class WorkerObject
    {
        public WorkerObject(BackgroundWorker worker)
        {
            Worker = worker;
            SynchEvent = new ManualResetEvent(false);
        }
        public BackgroundWorker Worker
        {
            get;
            set;
        }

        public ManualResetEvent SynchEvent
        {
            get;
            set;
        }
    }
}
