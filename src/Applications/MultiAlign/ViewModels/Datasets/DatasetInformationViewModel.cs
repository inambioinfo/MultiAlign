﻿using System;
using System.Collections.ObjectModel;
using MultiAlign.Commands.Datasets;
using MultiAlign.Commands.Plotting;
using MultiAlign.ViewModels.Plotting;
using MultiAlignCore.Data.MetaData;
using NHibernate.Mapping;

namespace MultiAlign.ViewModels.Datasets
{
    using System.Windows.Input;
    using System.Windows.Media;

    using MultiAlign.Commands;
    using MultiAlign.Data;

    public class DatasetInformationViewModel : ViewModelBase
    {
        private readonly DatasetInformation m_information;
        private bool m_expand;
        private bool m_isSelected;

        public DatasetInformationViewModel(DatasetInformation information)
        {
            m_information = information;
            var data = information.PlotData;
            PlotData = new ObservableCollection<PlotViewModel>();

            RequestRemovalCommand = new BaseCommand(
                () =>
                    {
                        if (RemovalRequested != null)
                        {
                            RemovalRequested(this, EventArgs.Empty);
                        }
                    }, s => !this.DoingWork);

            if (data != null)
            {
                PlotData.Add(new PlotViewModel(data.Alignment,
                    "Alignment",
                    new PictureDisplayCommand(data.Alignment, "Alignment" + information.DatasetName)));
                PlotData.Add(new PlotViewModel(data.Features,
                    "Features",
                    new FeatureDisplayCommand(information)));

                PlotData.Add(new PlotViewModel(data.MassErrorHistogram, "Mass Error Histogram"));
                PlotData.Add(new PlotViewModel(data.NetErrorHistogram, "NET Error Histogram"));
                PlotData.Add(new PlotViewModel(data.MassScanResidual, "Mass vs Scan Residuals"));
                PlotData.Add(new PlotViewModel(data.MassMzResidual, "Mass vs m/z Residuals"));
                PlotData.Add(new PlotViewModel(data.NetResiduals, "NET Residuals"));
            }


            ModifyDatasetCommand = new ShowDatasetDetailCommand();
        }

        public event EventHandler RemovalRequested;

        public BaseCommand RequestRemovalCommand { get; private set; }

        public bool IsSelected
        {
            get { return m_isSelected; }
            set
            {
                if (value == m_isSelected)
                    return;

                m_isSelected = value;
                OnPropertyChanged("IsSelected");

                if (Selected != null)
                    Selected(this, null);
            }
        }

        public bool ScansBool
        {
            get { return this.Dataset.ScansBool; }
            set
            {
                if (this.Dataset.ScansBool != value)
                {
                    this.Dataset.ScansBool = value;
                    this.OnPropertyChanged("ScansBool");
                }
            }
        }

        public bool RawBool
        {
            get { return this.Dataset.RawBool; }
            set
            {
                if (this.Dataset.RawBool != value)
                {
                    this.Dataset.RawBool = value;
                    this.OnPropertyChanged("RawBool");
                }
            }
        }

        public DatasetInformation.DatasetStates DatasetState
        {
            get { return this.Dataset.DatasetState; }
            set
            {
                if (this.Dataset.DatasetState != value)
                {
                    this.Dataset.DatasetState = value;

                    this.IsFindingFeatures = value == DatasetInformation.DatasetStates.FindingFeatures ||
                                             value == DatasetInformation.DatasetStates.PersistingFeatures;

                    this.IsAligning = value == DatasetInformation.DatasetStates.Aligning ||
                                      value == DatasetInformation.DatasetStates.PersistingAlignment;

                    this.IsClustering = value == DatasetInformation.DatasetStates.Clustering ||
                                        value == DatasetInformation.DatasetStates.PersistingClusters;
                    this.FeaturesFound = value >= DatasetInformation.DatasetStates.FeaturesFound;
                    this.IsAligned = value >= DatasetInformation.DatasetStates.Aligned;
                    this.IsClustered = value >= DatasetInformation.DatasetStates.Clustered;

                    this.OnPropertyChanged("FindingFeatureLabelColor");
                    this.OnPropertyChanged("AligningLabelColor");
                    this.OnPropertyChanged("ClusterLabelColor");
                    this.OnPropertyChanged();
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
                    this.OnPropertyChanged("FeaturesFound");
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
                    this.OnPropertyChanged("IsAligned");
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
                    this.OnPropertyChanged("ClusterLabelColor");
                    this.OnPropertyChanged("DoingWork");
                    this.OnPropertyChanged("IsClustering");
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
                    this.OnPropertyChanged("IsClustered");
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
                    case DatasetInformation.DatasetStates.Clustering:
                        brush = Brushes.Red;
                        break;
                    case DatasetInformation.DatasetStates.PersistingClusters:
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
                    ThreadSafeDispatcher.Invoke(() => this.RequestRemovalCommand.InvokeCanExecuteChanged());
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("FindingFeatureLabelColor");
                    this.OnPropertyChanged("DoingWork");
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
                    case DatasetInformation.DatasetStates.FindingFeatures:
                        brush = Brushes.Red;
                        break;
                    case DatasetInformation.DatasetStates.PersistingFeatures:
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
                    ThreadSafeDispatcher.Invoke(() => this.RequestRemovalCommand.InvokeCanExecuteChanged());
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("AligningLabelColor");
                    this.OnPropertyChanged("DoingWork");
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
                    case DatasetInformation.DatasetStates.Aligning:
                        brush = Brushes.Red;
                        break;
                    case DatasetInformation.DatasetStates.PersistingAlignment:
                        brush = Brushes.Yellow;
                        break;
                    default:
                        brush = Brushes.Transparent;
                        break;
                }

                return brush;
            }
        }

        public bool DoingWork
        {
            get { return this.IsAligning || this.IsFindingFeatures || this.IsClustering; }
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
                    OnPropertyChanged("DatasetId");
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

        public ObservableCollection<PlotViewModel> PlotData { get; private set; }

        public bool ShouldExpand
        {
            get { return m_expand; }
            set
            {
                if (value != m_expand)
                {
                    m_expand = value;
                    OnPropertyChanged("ShouldExpand");
                }
            }
        }

        public ICommand ModifyDatasetCommand { get; set; }
        public event EventHandler Selected;
    }
}