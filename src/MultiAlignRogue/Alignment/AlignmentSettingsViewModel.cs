﻿using System.Windows.Navigation;
using InformedProteomics.Backend.MassFeature;
using InformedProteomics.Backend.Utils;
using MultiAlign.ViewModels.Databases;
using MultiAlign.Windows.Viewers.Databases;
using MultiAlignCore.Algorithms.Alignment.LcmsWarp;
using MultiAlignCore.Data.Features;
using MultiAlignCore.Data.MassTags;
using MultiAlignCore.Data.MetaData;
using MultiAlignCore.IO.Features;
using MultiAlignCore.IO.MTDB;
using MultiAlignRogue.ViewModels;

namespace MultiAlignRogue.Alignment
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;

    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;

    using MultiAlign.Data;

    using MultiAlignCore.Algorithms;
    using MultiAlignCore.Data;
    using MultiAlignCore.Data.Alignment;
    using MultiAlignCore.IO;
    using MultiAlignCore.IO.Hibernate;

    public class AlignmentSettingsViewModel : ViewModelBase
    {
        private readonly MultiAlignAnalysis analysis;

        private readonly FeatureLoader featureCache;

        private readonly IAlignmentWindowFactory alignmentWindowFactory;

        private readonly IProgress<int> progress;

        private readonly AlgorithmBuilder builder;

        private readonly LCMSFeatureAligner aligner;

        private AlgorithmProvider algorithms;

        private DatasetInformationViewModel selectedBaseline;

        private List<AlignmentData> alignmentInformation;

        public AlignmentSettingsViewModel(MultiAlignAnalysis analysis,
                                          FeatureLoader featureCache,
                                          ObservableCollection<DatasetInformationViewModel> datasets,
                                          IAlignmentWindowFactory alignmentWindowFactory = null,
                                          IProgress<int> progressReporter = null)
        {
            this.analysis = analysis;
            this.featureCache = featureCache;
            this.Datasets = datasets;
            this.alignmentWindowFactory = alignmentWindowFactory ?? new AlignmentViewFactory();
            this.progress = progressReporter ?? new Progress<int>();
            this.aligner = new LCMSFeatureAligner();
            this.builder = new AlgorithmBuilder();
            this.CalibrationOptions = new ObservableCollection<AlignmentType>(Enum.GetValues(typeof(AlignmentType)).Cast<AlignmentType>());
            this.AlignmentAlgorithms = new ObservableCollection<FeatureAlignmentType>(
                                           Enum.GetValues(typeof(FeatureAlignmentType)).Cast<FeatureAlignmentType>());
            this.alignmentInformation = new List<AlignmentData>();

            this.MessengerInstance.Register<PropertyChangedMessage<bool>>(this, args =>
            {
                if (args.Sender is DatasetInformationViewModel && args.PropertyName == "IsSelected")
                {
                    ThreadSafeDispatcher.Invoke(() => this.AlignCommand.RaiseCanExecuteChanged());
                    ThreadSafeDispatcher.Invoke(() => this.DisplayAlignmentCommand.RaiseCanExecuteChanged());
                }
            });

            // When dataset state changes, update can executes.
            this.MessengerInstance.Register<PropertyChangedMessage<DatasetInformationViewModel.DatasetStates>>(this, args =>
            {
                if (args.Sender is DatasetInformationViewModel && args.PropertyName == "DatasetState")
                {
                    ThreadSafeDispatcher.Invoke(() => this.AlignCommand.RaiseCanExecuteChanged());
                    ThreadSafeDispatcher.Invoke(() => this.DisplayAlignmentCommand.RaiseCanExecuteChanged());
                }
            });

            this.AlignCommand = new RelayCommand(this.AsyncAlign, this.CanAlign);
            this.DisplayAlignmentCommand = new RelayCommand(
                                                            this.DisplayAlignment,
                                                            () => this.Datasets.Any(file => file.IsAligned && file.IsSelected &&
                                                                  this.alignmentInformation.Any(data => data.DatasetID == file.DatasetId)));
            this.SelectAMTCommand = new RelayCommand(this.AsyncSelectAMT, () => this.ShouldAlignToAMT);
            this.ShouldAlignToBaseline = true;
            this.ShouldAlignToAMT = false;
        }

        public RelayCommand AlignCommand { get; private set; }

        public RelayCommand DisplayAlignmentCommand { get; private set; }

        public RelayCommand SelectAMTCommand { get; private set; }

        public ObservableCollection<FeatureAlignmentType> AlignmentAlgorithms { get; private set; }

        public ObservableCollection<AlignmentType> CalibrationOptions { get; private set; }

        public ObservableCollection<DatasetInformationViewModel> Datasets { get; private set; }

        private bool shouldAlignToBaseline;
        public bool ShouldAlignToBaseline
        {
            get
            {
                return shouldAlignToBaseline;
            }
            set
            {
                shouldAlignToBaseline = value;
                this.AlignCommand.RaiseCanExecuteChanged();
                this.RaisePropertyChanged();
            }
        }

        private bool shouldAlignToAMT;
        public bool ShouldAlignToAMT
        {
            get { return shouldAlignToAMT; }
            set
            {
                shouldAlignToAMT = value; 
                SelectAMTCommand.RaiseCanExecuteChanged();
                this.AlignCommand.RaiseCanExecuteChanged();
                this.RaisePropertyChanged();
            }
        }

        public DatasetInformationViewModel SelectedBaseline
        {
            get { return this.selectedBaseline; }
            set
            {
                if (this.selectedBaseline != value)
                {
                    if (value == null)
                    {
                        this.analysis.MetaData.BaselineDataset = null;
                    }
                    else
                    {
                        this.selectedBaseline = value;
                        this.analysis.MetaData.BaselineDataset = value.Dataset;
                    }

                    this.AlignCommand.RaiseCanExecuteChanged();
                    this.RaisePropertyChanged();
                }
            }
        }

        public double MassTolerance
        {
            get { return this.analysis.Options.LcmsClusteringOptions.InstrumentTolerances.Mass; }
            set
            {
                this.analysis.Options.LcmsClusteringOptions.InstrumentTolerances.Mass = value;
                this.RaisePropertyChanged();
            }
        }

        public double NetTolerance
        {
            get { return this.analysis.Options.LcmsClusteringOptions.InstrumentTolerances.Net; }
            set
            {
                this.analysis.Options.LcmsClusteringOptions.InstrumentTolerances.Net = value;
                this.RaisePropertyChanged();
            }
        }


        private double massTagLoadProgress;
        public double MassTagLoadProgress
        {
            get { return this.massTagLoadProgress; }
            set
            {
                if (this.massTagLoadProgress != value)
                {
                    this.massTagLoadProgress = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool showMassTagProgress;

        public bool ShowMassTagProgress
        {
            get { return this.showMassTagProgress; }
            set
            {
                if (this.showMassTagProgress != value)
                {
                    this.showMassTagProgress = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public FeatureAlignmentType SelectedAlignmentAlgorithm
        {
            get { return this.analysis.Options.AlignmentOptions.AlignmentAlgorithm; }
            set
            {
                if (this.analysis.Options.AlignmentOptions.AlignmentAlgorithm != value)
                {
                    this.analysis.Options.AlignmentOptions.AlignmentAlgorithm = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public AlignmentType SelectedCalibrationType
        {
            get { return this.analysis.Options.AlignmentOptions.AlignmentType; }
            set
            {
                if (this.analysis.Options.AlignmentOptions.AlignmentType != value)
                {
                    this.analysis.Options.AlignmentOptions.AlignmentType = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public async void AsyncAlign()
        {
            if (ShouldAlignToBaseline == true && this.SelectedBaseline != null)
            {
                await Task.Run(() => this.AlignToBaseline());
            }
            else if (ShouldAlignToAMT == true)
            {
                await Task.Run(() => this.AlignToBaseline());
            }
        }


        private void AlignToBaseline()
        {
            //Update algorithms and providers
            this.featureCache.Providers = this.analysis.DataProviders;
            this.algorithms = this.builder.GetAlgorithmProvider(this.analysis.Options);
            this.aligner.m_algorithms = this.algorithms;
            List<UMCLight> baselineFeatures = new List<UMCLight>();
            if (ShouldAlignToBaseline)
            {
                baselineFeatures = this.featureCache.Providers.FeatureCache.FindByDatasetId(this.selectedBaseline.DatasetId);
                this.SelectedBaseline.DatasetState = DatasetInformationViewModel.DatasetStates.Aligning;
                var priorAlignment = from x in this.alignmentInformation where x.DatasetID == this.selectedBaseline.DatasetId select x;
                if (priorAlignment.Any())
                {
                    this.alignmentInformation.Remove(priorAlignment.Single());
                }
            }
            var alignmentData = new AlignmentDAOHibernate();
            alignmentData.ClearAll();
         
            var selectedFiles = this.Datasets.Where(file => file.IsSelected && !file.DoingWork).ToList();
            foreach (var file in selectedFiles)
            {
                file.DatasetState = DatasetInformationViewModel.DatasetStates.Aligning;
            }

            foreach (var file in selectedFiles)
            {
                ThreadSafeDispatcher.Invoke(() => this.AlignCommand.RaiseCanExecuteChanged());
                ThreadSafeDispatcher.Invoke(() => this.DisplayAlignmentCommand.RaiseCanExecuteChanged());
                if ((file.Dataset.IsBaseline || !file.FeaturesFound) && ShouldAlignToBaseline)
                {
                    file.DatasetState = DatasetInformationViewModel.DatasetStates.Aligned;
                    continue;
                }

                IList<UMCLight> features = this.featureCache.Providers.FeatureCache.FindByDatasetId(file.DatasetId);
                AlignmentData alignment = new AlignmentData();
                if (ShouldAlignToBaseline)
                {
                    alignment = this.aligner.AlignToDataset(ref features, file.Dataset, baselineFeatures);
                }
                else
                {
                    alignment = this.aligner.AlignToDatabase(ref features, file.Dataset, this.analysis.MassTagDatabase);
                }
                //Check if there is information from a previous alignment for this dataset. If so, replace it. If not, just add the new one.
                var priorAlignment = this.alignmentInformation.Where(x => x.DatasetID == alignment.DatasetID);
                if (priorAlignment.Any())
                {
                    this.alignmentInformation.Remove(priorAlignment.Single());
                    this.alignmentInformation.Add(alignment);
                }
                else
                {
                    this.alignmentInformation.Add(alignment);
                }

                this.featureCache.CacheFeatures(features);
                file.DatasetState = DatasetInformationViewModel.DatasetStates.Aligned;
                ThreadSafeDispatcher.Invoke(() => this.AlignCommand.RaiseCanExecuteChanged());
                ThreadSafeDispatcher.Invoke(() => this.DisplayAlignmentCommand.RaiseCanExecuteChanged());
            }
            if (ShouldAlignToBaseline)
            {
                this.SelectedBaseline.DatasetState = DatasetInformationViewModel.DatasetStates.Aligned;
            }

        }

        public DmsDatabaseServerViewModel SelectedDatabaseServer { get; set; }

        private void AsyncSelectAMT()
        {
            ThreadSafeDispatcher.Invoke(SelectAMT);
            if (this.SelectedDatabaseServer != null)
            {
                ThreadSafeDispatcher.Invoke(AsyncAddMassTags);
            }
        }

        private void SelectAMT()
        {
            var dmsWindow = new DatabaseSearchToolWindow();
            var databaseView = new DatabasesViewModel();
            dmsWindow.DataContext = databaseView;
            dmsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var loader = MassTagDatabaseLoaderFactory.Create(MtdbDatabaseServerType.Dms);
            var databases = loader.LoadDatabases();

            foreach (var database in databases)
            {
                databaseView.AddDatabase(database);
            }

            if (this.SelectedDatabaseServer != null)
                databaseView.SelectedDatabase = this.SelectedDatabaseServer;

            var result = dmsWindow.ShowDialog();
            if (result == true)
            {
                if (databaseView.SelectedDatabase != null)
                {
                    this.SelectedDatabaseServer = databaseView.SelectedDatabase;
                }
            }

            this.AlignCommand.RaiseCanExecuteChanged();
        }

        private async void AsyncAddMassTags()
        {
            await Task.Run(() => AddMassTags());
        }

        private void AddMassTags()
        {
            var loadProgress = new Progress<ProgressData>(pd => this.MassTagLoadProgress = pd.Percent);
            this.RaisePropertyChanged("SelectedDatabaseServer");
            var database = this.SelectedDatabaseServer.Database;
            database.DatabaseName = database.DatabaseName;
            database.DatabaseServer = database.DatabaseServer;
            analysis.MetaData.Database = database;
            analysis.MetaData.Database.DatabaseFormat = MassTagDatabaseFormat.MassTagSystemSql;
            analysis.MassTagDatabase = MtdbLoaderFactory.LoadMassTagDatabase(this.analysis.MetaData.Database,
                                        this.analysis.Options.MassTagDatabaseOptions);
            analysis.DataProviders.MassTags.DeleteAll();
            this.ShowMassTagProgress = true;
            analysis.DataProviders.MassTags.AddAllStateless(analysis.MassTagDatabase.MassTags, loadProgress);
            this.ShowMassTagProgress = false;
        }

        private void DisplayAlignment()
        {
            foreach (var file in (this.Datasets.Where(x => x.IsSelected && x.IsAligned))) //x => x.IsAligned && !x.Dataset.IsBaseline
            {
                var alignment = this.alignmentInformation.Find(x => x.DatasetID == file.DatasetId);
                if (alignment != null)
                {
                    this.alignmentWindowFactory.CreateNewWindow(alignment);
                }
                else
                {
                    MessageBox.Show(String.Format("No alignment to show for {0}", file.Dataset.DatasetName));
                }
            }
        }

        private bool CanAlign()
        {
            var selectedDatasets = this.Datasets.Where(ds => ds.IsSelected).ToList();
            var validDatasetsSelected = selectedDatasets.Any(ds => !ds.DoingWork && ds.FeaturesFound);
            var validBaselineSelected = this.ShouldAlignToBaseline && this.SelectedBaseline != null &&
                                        // something other than baseline should be selected
                                        selectedDatasets.Any(ds => ds != this.SelectedBaseline);
            var validAmtSelected = this.ShouldAlignToAMT && analysis.MassTagDatabase != null;
            return validDatasetsSelected && (validBaselineSelected || validAmtSelected);
        }
    }
}
