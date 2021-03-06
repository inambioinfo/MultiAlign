﻿using System;
using System.Collections.ObjectModel;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MultiAlignCore.Data.MetaData;
using MultiAlignRogue.Utils;

namespace MultiAlignRogue.ViewModels
{
    using System.Windows.Input;
    using System.Windows.Media;

    public class DatasetInformationViewModel : ViewModelBase
    {
        public enum DatasetStates
        {
            Waiting,
            LoadingRawData,
            Loaded,
            FindingFeatures,
            PersistingFeatures,
            FeaturesFound,
            Aligning,
            Baseline,
            PersistingAlignment,
            Aligned,
            Clustering,
            PersistingClusters,
            Clustered,
            Matching,
            PersistingMatches,
            Matched,
        };

        private readonly DatasetInformation m_information;
        private bool m_expand;
        private bool m_isSelected;

        public DatasetInformationViewModel(DatasetInformation information)
        {
            m_information = information;
            var data = information.PlotData;

            RequestRemovalCommand = new RelayCommand(
                () =>
                {
                    if (RemovalRequested != null)
                    {
                        RemovalRequested(this, EventArgs.Empty);
                    }
                }, () => !this.DoingWork);

            this.SetDatasetState();
        }

        public event EventHandler RemovalRequested;

        public event EventHandler StateChanged;

        public RelayCommand RequestRemovalCommand { get; private set; }

        private DatasetStates datasetState;
        public DatasetStates DatasetState
        {
            get { return this.datasetState; }
            set
            {
                if (this.datasetState != value)
                {
                    var prevValue = this.datasetState;
                    this.datasetState = value;

                    this.IsFindingFeatures = value == DatasetInformationViewModel.DatasetStates.FindingFeatures ||
                                             value == DatasetInformationViewModel.DatasetStates.PersistingFeatures;

                    this.IsAligning = value == DatasetInformationViewModel.DatasetStates.Aligning ||
                                      value == DatasetInformationViewModel.DatasetStates.PersistingAlignment;

                    this.IsClustering = value == DatasetInformationViewModel.DatasetStates.Clustering ||
                                        value == DatasetInformationViewModel.DatasetStates.PersistingClusters;
                    this.FeaturesFound = value >= DatasetInformationViewModel.DatasetStates.FeaturesFound;
                    this.IsAligned = value >= DatasetInformationViewModel.DatasetStates.Aligned;
                    this.IsClustered = value >= DatasetInformationViewModel.DatasetStates.Clustered;

                    this.IsLoadingRawData = value == DatasetStates.LoadingRawData;

                    this.ShouldShowProgress = (value == DatasetStates.LoadingRawData) ||
                          (value == DatasetStates.FindingFeatures) ||
                          (value == DatasetStates.PersistingFeatures) ||
                          ((value == DatasetStates.Aligning) ||
                          (value == DatasetStates.PersistingAlignment));

                    var isFinishedState = (value == DatasetStates.FeaturesFound || value == DatasetStates.Aligned ||
                                           value == DatasetStates.Clustered     || value == DatasetStates.Matched);
                    if (isFinishedState && this.StateChanged != null)
                    {
                        this.StateChanged(this, EventArgs.Empty);
                    }

                    this.RaisePropertyChanged("FindingFeatureLabelColor");
                    this.RaisePropertyChanged("AligningLabelColor");
                    this.RaisePropertyChanged("ClusterLabelColor");
                    this.RaisePropertyChanged("DatasetState", prevValue, value, true);
                }
            }
        }

        public bool IsBaseline
        {
            get { return this.Dataset.IsBaseline; }
            set
            {
                if (this.Dataset.IsBaseline != value)
                {
                    this.Dataset.IsBaseline = value;
                    this.DatasetState = DatasetStates.Baseline;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool FeaturesFound
        {
            get { return this.Dataset.FeaturesFound; }
            private set
            {
                if (this.Dataset.FeaturesFound != value)
                {
                    this.Dataset.FeaturesFound = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool IsAligned
        {
            get { return this.Dataset.IsAligned; }
            private set
            {
                if (this.Dataset.IsAligned != value)
                {
                    this.Dataset.IsAligned = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool isClustering;

        public bool IsClustering
        {
            get { return this.isClustering; }
            private set
            {
                if (this.isClustering != value)
                {
                    this.isClustering = value;
                    this.RaisePropertyChanged("ClusterLabelColor");
                    this.RaisePropertyChanged("DoingWork");
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool IsClustered
        {
            get { return this.Dataset.IsClustered; }
            private set
            {
                if (this.Dataset.IsClustered != value)
                {
                    this.Dataset.IsClustered = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public Brush ClusterLabelColor
        {
            get
            {
                Brush brush;
                switch (this.DatasetState)
                {
                    case DatasetInformationViewModel.DatasetStates.Clustering:
                        brush = Brushes.Red;
                        break;
                    case DatasetInformationViewModel.DatasetStates.PersistingClusters:
                        brush = Brushes.Yellow;
                        break;
                    default:
                        brush = Brushes.Transparent;
                        break;
                }

                return brush;
            }
        }

        private bool isFindingFeatures;
        public bool IsFindingFeatures
        {
            get { return this.isFindingFeatures; }
            private set
            {
                if (this.isFindingFeatures != value)
                {
                    this.isFindingFeatures = value;
                    ThreadSafeDispatcher.Invoke(() => this.RequestRemovalCommand.RaiseCanExecuteChanged());
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged("FindingFeatureLabelColor");
                    this.RaisePropertyChanged("DoingWork");
                }
            }
        }

        public Brush FindingFeatureLabelColor
        {
            get
            {
                Brush brush;
                switch (this.DatasetState)
                {
                    case DatasetInformationViewModel.DatasetStates.FindingFeatures:
                        brush = Brushes.Red;
                        break;
                    case DatasetInformationViewModel.DatasetStates.PersistingFeatures:
                        brush = Brushes.Yellow;
                        break;
                    default:
                        brush = Brushes.Transparent;
                        break;
                }

                return brush;
            }
        }

        private bool isAligning;
        public bool IsAligning
        {
            get { return this.isAligning; }
            private set
            {
                if (this.isAligning != value)
                {
                    this.isAligning = value;
                    ThreadSafeDispatcher.Invoke(() => this.RequestRemovalCommand.RaiseCanExecuteChanged());
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged("AligningLabelColor");
                    this.RaisePropertyChanged("DoingWork");
                }
            }
        }

        public Brush AligningLabelColor
        {
            get
            {
                Brush brush;
                switch (this.DatasetState)
                {
                    case DatasetInformationViewModel.DatasetStates.Aligning:
                        brush = Brushes.Red;
                        break;
                    case DatasetInformationViewModel.DatasetStates.PersistingAlignment:
                        brush = Brushes.Yellow;
                        break;
                    default:
                        brush = Brushes.Transparent;
                        break;
                }

                return brush;
            }
        }

        private bool isLoadingRawData;
        public bool IsLoadingRawData
        {
            get { return this.isLoadingRawData; }
            private set
            {
                if (this.isLoadingRawData != value)
                {
                    this.isLoadingRawData = value;
                    ThreadSafeDispatcher.Invoke(() => this.RequestRemovalCommand.RaiseCanExecuteChanged());
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged("DoingWork");
                }
            }
        }

        public bool DoingWork
        {
            get { return this.IsLoadingRawData || this.IsAligning || this.IsFindingFeatures || this.IsClustering; }
        }

        private double progress;

        public double Progress
        {
            get { return this.progress; }
            set
            {
                if (this.progress != value)
                {
                    this.progress = value;
                    this.RaisePropertyChanged("Progress");
                }
            }
        }

        private bool shouldShowProgress;
        public bool ShouldShowProgress
        {
            get { return this.shouldShowProgress; }
            set
            {
                if (this.shouldShowProgress != value)
                {
                    this.shouldShowProgress = value;
                    this.Progress = 0; // Reset the progress to zero
                    this.RaisePropertyChanged();
                }
            }
        }

        public DatasetInformation Dataset
        {
            get { return m_information; }
        }

        public int DatasetId
        {
            get { return m_information.DatasetId; }
            set
            {
                if (m_information != null)
                {
                    m_information.DatasetId = value;
                    RaisePropertyChanged("DatasetId");
                }
            }
        }

        public string Name
        {
            get
            {
                var name = "";
                if (m_information != null)
                {
                    name = m_information.DatasetName;
                }
                return name;
            }
        }

        public string DisplayName
        {
            get
            {
                ///stupid WPF content __ http://stackoverflow.com/questions/7861699/can-not-see-underscore-in-wpf-content
                return Name.Replace("_", "__");
            }
        }

        public int Id
        {
            get
            {
                var id = 0;
                if (m_information != null)
                {
                    id = m_information.DatasetId;
                }
                return id;
            }
        }

        public bool ShouldExpand
        {
            get { return m_expand; }
            set
            {
                if (value != m_expand)
                {
                    m_expand = value;
                    RaisePropertyChanged("ShouldExpand");
                }
            }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get { return this.isSelected; }
            set
            {
                if (this.isSelected != value)
                {
                    this.isSelected = value;
                    this.RaisePropertyChanged("IsSelected", !value, value, true);
                }
            }
        }

        public event EventHandler Selected;

        private void SetDatasetState()
        {
            if (this.FeaturesFound && !(this.IsAligned || this.IsClustered))
            {
                this.DatasetState = DatasetStates.FeaturesFound;
            }

            if (this.IsAligned && ! this.IsClustered)
            {
                this.DatasetState = DatasetStates.Aligned;
            }

            if (this.IsClustered)
            {
                this.DatasetState = DatasetStates.Clustered;
            }
        }
    }
}