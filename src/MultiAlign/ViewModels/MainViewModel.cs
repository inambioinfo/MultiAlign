﻿using MultiAlign.Commands;
using MultiAlign.Data;
using MultiAlign.Data.States;
using MultiAlign.ViewModels.Analysis;
using MultiAlign.ViewModels.TreeView;
using MultiAlign.ViewModels.Wizard;
using MultiAlignCore;
using MultiAlignCore.Algorithms;
using MultiAlignCore.Data;
using MultiAlignCore.Data.Features;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MultiAlign.ViewModels
{
    /// <summary>
    /// Does nothing right now...needs to replace the code behind from the main window
    /// </summary>
    public class MainViewModel: ViewModelBase
    {
        private string m_status;
        private string m_title;        
        private AnalysisViewModel           m_currentAnalysis;
        private AnalysisSetupViewModel m_analysisSetupViewModel;
        private AnalysisRunningViewModel m_analysisRunningViewModel;

        public MainViewModel()
        {
            // Create the state moderation (between views)
            BuildStateModerator();

            MainDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            MainDataName      = "analysis";



            // Titles and Status
            var version  = ApplicationUtility.GetEntryAssemblyData();
            Title           = version;
            
            // Command Setup
            ShowAnalysisCommand = new BaseCommandBridge(ShowAnalysis);
            ShowStartCommand    = new BaseCommandBridge(ShowStart);

            // View Models
            
            var workSpacePath    = ApplicationUtility.GetApplicationDataFolderPath("MultiAlign");
            workSpacePath           = Path.Combine(workSpacePath, Properties.Settings.Default.WorkspaceFile);
            GettingStartedViewModel = new GettingStartedViewModel(workSpacePath, StateModerator);

            GettingStartedViewModel.NewAnalysisStarted += GettingStartedViewModel_NewAnalysisStarted;
            GettingStartedViewModel.ExistingAnalysisSelected += GettingStartedViewModel_ExistingAnalysisSelected;

            AnalysisRunningViewModel = new AnalysisRunningViewModel();            
            AnalysisRunningViewModel.AnalysisCancelled += AnalysisRunningViewModel_AnalysisCancelled;
            AnalysisRunningViewModel.AnalysisComplete  += AnalysisRunningViewModel_AnalysisComplete;

            LoadingAnalysisViewModel = new AnalysisLoadingViewModel();
            LoadingAnalysisViewModel.AnalysisLoaded += LoadingAnalysisViewModel_AnalysisLoaded;

            ApplicationStatusMediator.SetStatus("Ready.");
            
        }


        #region Command Delegate Method Handlers
        public void ShowAnalysis(object parameter)
        {
            ShowLoadedAnalysis();
        }
        public void ShowStart(object parameter)
        {
            ShowHomeScreen();
        }
        #endregion

        #region Commands
        /// <summary>
        /// Gets the showing of an analysis 
        /// </summary>
        public ICommand ShowAnalysisCommand { get; private set; }
        /// <summary>
        /// Gets the showing of a new analysis
        /// </summary>
        public ICommand ShowStartCommand { get; private set; }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the current analysis.
        /// </summary>
        public AnalysisViewModel CurrentAnalysis
        {
            get
            {
                return m_currentAnalysis;
            }
            set
            {
                if (m_currentAnalysis != value)
                {
                    m_currentAnalysis = value;
                    OnPropertyChanged("CurrentAnalysis");
                }
            }
        }
        /// <summary>
        /// Gets or sets the title of the window
        /// </summary>
        public string Title
        {
            get
            {
                return m_title;
            }
            set
            {
                if (m_title != value)
                {
                    m_title = value;
                    OnPropertyChanged("Title");
                }
            }
        }
        /// <summary>
        /// Gets or sets the status 
        /// </summary>
        public string Status
        {
            get
            {
                return m_status;
            }
            set
            {
                if (m_status != value)
                {
                    m_status = value;
                    OnPropertyChanged("Status");
                }
            }
        }
        #endregion

        #region View Models
        /// <summary>
        /// Gets or sets the view model for displaying the home screen.
        /// </summary>
        public GettingStartedViewModel GettingStartedViewModel
        {
            get;
            private set;
        }
        /// <summary>
        /// Gets or sets the view model for creating a new analysis.
        /// </summary>
        public AnalysisSetupViewModel AnalysisSetupViewModel
        {
            get
            {
                return m_analysisSetupViewModel;
            }
            set
            {
                if (m_analysisSetupViewModel != value)
                {
                    m_analysisSetupViewModel = value;
                    OnPropertyChanged("AnalysisSetupViewModel");
                }
            }
        }
        /// <summary>
        /// Gets or sets the analysis running view model
        /// </summary>
        public AnalysisRunningViewModel AnalysisRunningViewModel
        {
            get
            {
                return m_analysisRunningViewModel;
            }
            set
            {
                if (m_analysisRunningViewModel != value)
                {
                    m_analysisRunningViewModel = value;
                    OnPropertyChanged("AnalysisRunningViewModel");
                }
            }
        }
        /// <summary>
        /// Gets or sets the state moderator.
        /// </summary>
        public StateModeratorViewModel StateModerator
        {
            get;
            set;
        }
        /// <summary>
        /// Gets or set the view model for loading an analysis.
        /// </summary>
        public AnalysisLoadingViewModel LoadingAnalysisViewModel { get; set; }
        #endregion

        #region Analysis State Commands        
        private bool ConfirmCancel()
        {
            System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(   "Performing this action will cancel the running analysis.  Do you want to cancel?", 
                                                                                       "Cancel Analysis", 
                                                                                       MessageBoxButton.YesNo);
            return (result == System.Windows.MessageBoxResult.Yes);
        }
        #endregion

        #region Loading      

        /// <summary>
        /// Loads a recent analysis
        /// </summary>
        /// <param name="recentAnalysis"></param>
        private void LoadAnalysis(RecentAnalysis recentAnalysis)
        {
            string message = "";
            if (StateModerator.IsAnalysisRunning(ref message))
            {
                Status = "Cannot open a new analysis while one is running.";
                return;
            }

            string filename = Path.Combine(recentAnalysis.Path, recentAnalysis.Name);
            if (!File.Exists(filename))
            {
                StateModerator.CurrentViewState = ViewState.HomeView;
                Status = "The analysis file does not exist";
                return;
            }

            // Show the open view
            StateModerator.CurrentViewState = ViewState.OpenView;
            LoadingAnalysisViewModel.LoadAnalysis(recentAnalysis);

        }
        /// <summary>
        /// Handles when an analysis has been loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LoadingAnalysisViewModel_AnalysisLoaded(object sender, AnalysisStatusArgs e)
        {
            DisplayAnalysis(e.Analysis);
        }
        private void DisplayAnalysis(MultiAlignAnalysis analysis)
        {
            // Change the title
            string version = ApplicationUtility.GetEntryAssemblyData();
            Title = string.Format("{0} - {1}", version, analysis.MetaData.AnalysisName);

            var model                 = new AnalysisViewModel(analysis);
            model.ClusterTree.ClustersFiltered      += ClustersFiltered;
            UpdateAllClustersPlot(model.ClusterTree.FilteredClusters);
            
            CurrentAnalysis                         = model;
            StateModerator.CurrentViewState         = ViewState.AnalysisView;
            var recent                              = new RecentAnalysis(   analysis.MetaData.AnalysisPath,
                                                                            analysis.MetaData.AnalysisName);
            GettingStartedViewModel.CurrentWorkspace.AddAnalysis(recent);
        }
        #endregion

        #region Display
        void ClustersFiltered(object sender, ClustersUpdatedEventArgs e)
        {
            UpdateAllClustersPlot(e.Clusters);
        }

        /// <summary>
        /// Updates the clustered plots...
        /// </summary>
        /// <param name="clusters"></param>
        void UpdateAllClustersPlot(ObservableCollection<UMCClusterTreeViewModel> clusters)
        {
            List<UMCClusterLightMatched> filteredClusters = (from cluster in clusters
                                                                select cluster.Cluster).ToList();
        }
        /// <summary>
        /// Shows the new analysis setup
        /// </summary>
        private void ShowNewAnalysisSetup()
        {
            string message = "";
            bool canStart  = StateModerator.CanPerformNewAnalysis(ref message);
            Status         = message;
            if (!canStart)
            {              
                return ;
            }

            ApplicationStatusMediator.SetStatus("Creating new analysis.");

            StateModerator.CurrentViewState                 = ViewState.SetupAnalysisView;
            StateModerator.CurrentAnalysisState             = AnalysisState.Setup;
            
            AnalysisConfig config                           = new AnalysisConfig();
            config.Analysis                                 = new MultiAlignAnalysis();
            config.AnalysisPath                             = MainDataDirectory;
            config.AnalysisName                             = MainDataName;
            config.Analysis.AnalysisType                    = AnalysisType.Full;
            config.Analysis.Options.AlignmentOptions.IsAlignmentBaselineAMasstagDB = false;

            AnalysisSetupViewModel                = new Analysis.AnalysisSetupViewModel(config);
            AnalysisSetupViewModel.AnalysisQuit  += new EventHandler(AnalysisSetupViewModel_AnalysisQuit);
            AnalysisSetupViewModel.AnalysisStart += new EventHandler(AnalysisSetupViewModel_AnalysisStart);
            AnalysisSetupViewModel.CurrentStep    = AnalysisSetupStep.DatasetSelection;
        }
        /// <summary>
        /// Displays the loaded analysis 
        /// </summary>
        private void ShowLoadedAnalysis()
        {
            string message = "";
            bool isRunning = StateModerator.IsAnalysisRunning(ref message);

            if (isRunning)
            {
                StateModerator.CurrentViewState = ViewState.RunningAnalysisView;
            }
            else
            {                
                StateModerator.CurrentAnalysisState = AnalysisState.Viewing;
                StateModerator.CurrentViewState     = ViewState.AnalysisView;                                    
            }
        }
        /// <summary>
        /// Shows the home screen.
        /// </summary>
        private bool ShowHomeScreen()
        {
            StateModerator.CurrentViewState = ViewState.HomeView;
            return true;
        }
        /// <summary>
        /// Cancels the analysis setup
        /// </summary>
        private bool CancelAnalysisSetup()
        {
            // If we were looking at an analysis before, then go back to it.
            if (StateModerator.PreviousViewState == ViewState.AnalysisView)
            {
                StateModerator.CurrentViewState     = ViewState.AnalysisView;
                StateModerator.CurrentAnalysisState = AnalysisState.Viewing;
            }
            else
            {
                StateModerator.CurrentViewState     = ViewState.HomeView;
                StateModerator.CurrentAnalysisState = AnalysisState.Idle;
            }
            return true;
        }
        #endregion

        #region Settings
        public string MainDataDirectory { get; set; }
        public string MainDataName { get; set; }
        #endregion

        #region Application State
        /// <summary>
        /// Constructs the transitions for the user interface
        /// </summary>
        private void BuildStateModerator()
        {
            StateModerator = new StateModeratorViewModel();
            StateModerator.CurrentAnalysisState     = AnalysisState.Idle;
            StateModerator.CurrentViewState         = ViewState.HomeView;
            StateModerator.PreviousAnalysisState    = AnalysisState.Idle;
            StateModerator.PreviousViewState        = ViewState.HomeView;            
        }
        #endregion
        
        #region Getting Started View Model Event Handlers
        /// <summary>
        /// Loads an existing analysis file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void GettingStartedViewModel_ExistingAnalysisSelected(object sender, OpenAnalysisArgs e)
        {
            LoadAnalysis(e.AnalysisData);
        }
        /// <summary>
        /// Starts a new analysis 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void GettingStartedViewModel_NewAnalysisStarted(object sender, OpenAnalysisArgs e)
        {
            ShowNewAnalysisSetup();
        }
        /// <summary>
        /// Loads a recently selected view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void m_gettingStarted_RecentAnalysisSelected(object sender, OpenAnalysisArgs e)
        {
            LoadAnalysis(e.AnalysisData);         
        }
        #endregion

        #region Analysis Running View Model Events
        /// <summary>
        /// Adds the finished analysis back into the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AnalysisRunningViewModel_AnalysisComplete(object sender, AnalysisStatusArgs e)
        {
            string path                         = e.Configuration.AnalysisPath;
            string name                         = System.IO.Path.GetFileName(e.Configuration.AnalysisName);
            RecentAnalysis recent               = new RecentAnalysis(path, name);
            StateModerator.CurrentViewState     = ViewState.AnalysisView;
            StateModerator.CurrentAnalysisState = AnalysisState.Viewing;

            GettingStartedViewModel.AddAnalysis(recent);            
            DisplayAnalysis(e.Configuration.Analysis);
        }
        void AnalysisRunningViewModel_AnalysisCancelled(object sender, EventArgs e)
        {
            StateModerator.CurrentViewState     = ViewState.HomeView;
            StateModerator.CurrentAnalysisState = AnalysisState.Idle;
        }
        #endregion

        #region Aanlysis Setup View Model Events
        /// <summary>
        /// Starts the analysis
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AnalysisSetupViewModel_AnalysisStart(object sender, EventArgs e)
        {
            StateModerator.CurrentAnalysisState = AnalysisState.Running;
            StateModerator.CurrentViewState     = ViewState.RunningAnalysisView;            
            AnalysisRunningViewModel.Start(AnalysisSetupViewModel.AnalysisConfiguration);
        }
        /// <summary>
        /// Cancels the analysis.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AnalysisSetupViewModel_AnalysisQuit(object sender, EventArgs e)
        {
            CancelAnalysisSetup();
        }
        #endregion        
    }
}