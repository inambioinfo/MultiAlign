﻿using MultiAlign.Commands;
using MultiAlign.Properties;
using MultiAlign.ViewModels.Instruments;
using MultiAlign.Windows.Wizard;
using MultiAlignCore.Algorithms.Alignment;
using MultiAlignCore.Algorithms.Clustering;
using MultiAlignCore.Algorithms.FeatureFinding;
using MultiAlignCore.Algorithms.Options;
using MultiAlignCore.IO.Generic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PNNLOmics.Algorithms.FeatureClustering;

namespace MultiAlign.ViewModels.Wizard
{
    public class AnalysisOptionsViewModel: ViewModelBase
    {
        private MultiAlignAnalysisOptions m_options;
        private InstrumentPresetViewModel m_instrumentPreset;

        /// <summary>
        /// Open file dialog for opening an existing parameter file.
        /// </summary>
        private readonly System.Windows.Forms.OpenFileDialog m_dialog;
        private readonly System.Windows.Forms.SaveFileDialog m_saveDialog;
        private ExperimentPresetViewModel m_selectedExperimentPreset;

        public AnalysisOptionsViewModel(MultiAlignAnalysisOptions options)
        {
            m_options            = options;
            ClusteringAlgorithms = new ObservableCollection<LcmsFeatureClusteringAlgorithmType>();
            AlignmentAlgorithms  = new ObservableCollection<FeatureAlignmentType>();
            FeatureFindingAlgorithms = new ObservableCollection<FeatureFinderType>();

            UpdateOptions();

            InstrumentPresets = new ObservableCollection<InstrumentPresetViewModel>();
            ExperimentPresets = new ObservableCollection<ExperimentPresetViewModel>();

            var presets = new Dictionary<string, bool>();
            foreach (var preset in ExperimentPresetFactory.Create())
            {
                ExperimentPresets.Add(preset);

                if (!presets.ContainsKey(preset.InstrumentPreset.Name))
                {
                    presets.Add(preset.InstrumentPreset.Name, false);
                    InstrumentPresets.Add(preset.InstrumentPreset);
                }
            }

            foreach (var preset in InstrumentPresetFactory.Create())
            {
                if (!presets.ContainsKey(preset.Name))
                {
                    InstrumentPresets.Add(preset);
                }
            }

            
            SelectedPreset           = InstrumentPresets[0];
            SelectedExperimentPreset = ExperimentPresets[0];

            m_saveDialog                = new System.Windows.Forms.SaveFileDialog();
            m_dialog                    = new System.Windows.Forms.OpenFileDialog
            {
                Filter = Resources.MultiAlignParameterFileFilter                        
            };
            m_saveDialog.Filter         = Resources.MultiAlignParameterFileFilter;

            ShowAdvancedWindowCommand   = new BaseCommandBridge(ShowAdvancedWindow);
            SaveOptionsCommand          = new BaseCommandBridge(SaveCurrentParameters);
            LoadExistingCommand         = new BaseCommandBridge(LoadExistingParameters);

            Enum.GetValues(typeof(LcmsFeatureClusteringAlgorithmType)).Cast<LcmsFeatureClusteringAlgorithmType>().ToList().ForEach(x => ClusteringAlgorithms.Add(x));
            Enum.GetValues(typeof(FeatureAlignmentType)).Cast<FeatureAlignmentType>().ToList().ForEach(x => AlignmentAlgorithms.Add(x));
            Enum.GetValues(typeof(FeatureFinderType)).Cast<FeatureFinderType>().ToList().ForEach(x => FeatureFindingAlgorithms.Add(x));
        }

        private void ShowAdvancedWindow(object parameter)
        {
            var viewModel       = new AdvancedOptionsViewModel(m_options);            
            var view            = new AnalysisOptionsView
            {
                DataContext = viewModel,
                MinWidth = 800,
                MinHeight = 600,
                MaxWidth = 1200,
                MaxHeight = 1024,
                Width = 800,
                Height = 600,
                ShowInTaskbar = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            view.ShowDialog();

            UpdateOptions();
        }
        private void LoadExistingParameters(object parameter)
        {
            try
            {
                //TODO: Replace with OOKI dialogs
                System.Windows.Forms.DialogResult result = m_dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    var reader = new JsonReader<MultiAlignAnalysisOptions>();
                    m_options = reader.Read(m_dialog.FileName);
                    UpdateOptions();
                }
            }
                //TODO: Replace with appropriate exception handling
            catch (Exception ex)
            {
                //TODO: Add message?
            }
        }
        private void SaveCurrentParameters(object parameter)
        {
            try
            {
                System.Windows.Forms.DialogResult result = m_saveDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    var writer = new JsonWriter<MultiAlignAnalysisOptions>();
                    writer.Write(m_saveDialog.FileName, m_options);
                }
            }
            //TODO: Replace with appropriate exception handling
            catch (Exception ex)
            {
            }
        }


        #region Updating 
        private void UpdateOptions()
        {                        
            OnPropertyChanged("ShouldUseMzFilter");
            OnPropertyChanged("ShouldUseDeisotopingFilter");
            OnPropertyChanged("ShouldUseMzFilter");
            
            OnPropertyChanged("MinimumDeisotopingFitScore");
            OnPropertyChanged("MinimumIntensity");
            OnPropertyChanged("MinimumMz");
            OnPropertyChanged("MaximumMz");
            OnPropertyChanged("MinimumFeatureLength");
            OnPropertyChanged("MaximumFeatureLength");     
       
            OnPropertyChanged("MassResolution");
            OnPropertyChanged("FragmentationTolerance");
            OnPropertyChanged("NETTolerance");
            OnPropertyChanged("DriftTimeTolerance");

            OnPropertyChanged("IsIonMobility");
            OnPropertyChanged("SelectedAlignmentAlgorithm");
            OnPropertyChanged("SelectedClusteringAlgorithm");                  
        }
        #endregion

        #region Instrument Presets
        public ObservableCollection<InstrumentPresetViewModel> InstrumentPresets { get; private set; }
        public ObservableCollection<ExperimentPresetViewModel> ExperimentPresets { get; private set; }
        public InstrumentPresetViewModel SelectedPreset 
        {
            get
            {
                return m_instrumentPreset;
            }
            set
            {
                if (m_instrumentPreset != value)
                {
                    m_instrumentPreset = value;
                    UpdatePreset(value);
                    OnPropertyChanged("SelectedPreset");
                    UpdateOptions();
                }
            }
        }
        public ExperimentPresetViewModel SelectedExperimentPreset 
        {
            get
            {
                return m_selectedExperimentPreset;
            }
            set
            {
                if (m_selectedExperimentPreset != value)
                {
                    m_selectedExperimentPreset = value;
                    
                    m_options.MsFilteringOptions.MzRange.Minimum = value.MassRangeLow;
                    m_options.MsFilteringOptions.MzRange.Maximum = value.MassRangeHigh;

                    UpdateOptions();
                }
            }
        }


        private void UpdatePreset(InstrumentPresetViewModel preset)
        {
            MassResolution         = preset.Mass;
            FragmentationTolerance = preset.FragmentWindowSize;
            NetTolerance           = preset.NetTolerance;
            DriftTimeTolerance     = preset.DriftTimeTolerance;
        }
        #endregion

        #region Experiment Type
        public bool IsIonMobility
        {
            get
            {
                return m_options.UsedIonMobility;
            }
            set
            {
                if (m_options.UsedIonMobility == value) return;

                m_options.UsedIonMobility                = value;
                m_options.InstrumentTolerances.DriftTime = value ? 3 : 50;                                
                m_options.LcmsClusteringOptions.ShouldSeparateCharge = value;
                OnPropertyChanged("IsIonMobility");
                OnPropertyChanged("DriftTimeTolerance");
                
            }
        }
        public bool HasMsMsFragmentation
        {
            get
            {
                return m_options.HasMsMs;
            }
            set
            {
                if (m_options.HasMsMs == value) return;
                m_options.HasMsMs = value;                    
                OnPropertyChanged("HasMsMsFragmentation");
            }
        }
        #endregion

        #region Instrument Tolerances
        public double MassResolution 
        {
            get
            {
                return m_options.InstrumentTolerances.Mass;
            }
            set
            {                                
                m_options.AlignmentOptions.MassTolerance = value;
                m_options.InstrumentTolerances.Mass      = value;
                OnPropertyChanged("MassResolution");
            }
        }
        public double NetTolerance
        {
            get
            {
                return m_options.InstrumentTolerances.RetentionTime;
            }
            set
            {
                
                m_options.AlignmentOptions.NETTolerance         = value;
                m_options.InstrumentTolerances.RetentionTime    = value;
                OnPropertyChanged("NETTolerance");
            }
        }
        public double DriftTimeTolerance
        {
            get { return m_options.InstrumentTolerances.DriftTime; }
            set
            {
                    m_options.InstrumentTolerances.DriftTime = value;
                    OnPropertyChanged("DriftTimeTolerance");
                
            }
        }
        public double FragmentationTolerance
        {
            get
            {
                return m_options.InstrumentTolerances.FragmentationWindowSize;
            }
            set
            {
                m_options.InstrumentTolerances.FragmentationWindowSize = value;                    
                OnPropertyChanged("FragmentationTolerance");
            }
        }
        public double MaximumMz
        {
            get
            {
                return m_options.MsFilteringOptions.MzRange.Maximum;
            }
            set
            {
                    m_options.MsFilteringOptions.MzRange.Maximum = value;
                    OnPropertyChanged("MaximumMz");                
            }
        }
        public double MinimumMz
        {
            get
            {
                return m_options.MsFilteringOptions.MzRange.Minimum;
            }
            set
            {                
                m_options.MsFilteringOptions.MzRange.Minimum = value;
                OnPropertyChanged("MinimumMz");                
            }
        }


        public double MaximumCharge
        {
            get
            {
                return m_options.MsFilteringOptions.ChargeRange.Maximum;
            }
            set
            {
                m_options.MsFilteringOptions.ChargeRange.Maximum = value;
                OnPropertyChanged("MaximumCharge");
            }
        }
        public double MinimumCharge
        {
            get
            {
                return m_options.MsFilteringOptions.ChargeRange.Minimum;
            }
            set
            {
                m_options.MsFilteringOptions.ChargeRange.Minimum = value;
                OnPropertyChanged("MinimumCharge");
            }
        }
        public bool ShouldUseChargeStateFilter
        {
            get { return m_options.MsFilteringOptions.ShouldUseChargeFilter; }
            set
            {
                m_options.MsFilteringOptions.ShouldUseChargeFilter = value;
                OnPropertyChanged("ShouldUseChargeStateFilter");
            }
        }

        public bool ShouldUseMzFilter
        {
            get { return m_options.MsFilteringOptions.ShouldUseMzFilter; }
            set
            {
                m_options.MsFilteringOptions.ShouldUseMzFilter = value;
                OnPropertyChanged("ShouldUseMzFilter");
            }
        }
        public bool ShouldUseIntensityFilter
        {
            get { return m_options.MsFilteringOptions.ShouldUseIntensityFilter; }
            set
            {
                m_options.MsFilteringOptions.ShouldUseIntensityFilter = value;
                OnPropertyChanged("ShouldUseIntensityFilter");
            }
        }
        public bool ShouldUseDeisotopingFilter
        {
            get { return m_options.MsFilteringOptions.ShouldUseDeisotopingFilter; }
            set
            {
                m_options.MsFilteringOptions.ShouldUseDeisotopingFilter = value;
                OnPropertyChanged("ShouldUseDeisotopingFilter");
            }
        }
        #endregion

        #region Algorithms
        public ObservableCollection<LcmsFeatureClusteringAlgorithmType> ClusteringAlgorithms { get; set; }
        public ObservableCollection<FeatureAlignmentType>    AlignmentAlgorithms { get; set; }
        public ObservableCollection<FeatureFinderType>       FeatureFindingAlgorithms { get; set; }

        public LcmsFeatureClusteringAlgorithmType SelectedLcmsFeatureClusteringAlgorithm
        {
            get
            {
                return m_options.LcmsClusteringOptions.LcmsFeatureClusteringAlgorithm;
            }
            set
            {
                if (m_options.LcmsClusteringOptions.LcmsFeatureClusteringAlgorithm != value)
                {
                    m_options.LcmsClusteringOptions.LcmsFeatureClusteringAlgorithm = value;
                    OnPropertyChanged("SelectedClusteringAlgorithm");
                }
            }
        }
        public FeatureAlignmentType SelectedAlignmentAlgorithm
        {
            get
            {
                return m_options.AlignmentOptions.AlignmentAlgorithm;
            }
            set
            {
                if (m_options.AlignmentOptions.AlignmentAlgorithm != value)
                {
                    m_options.AlignmentOptions.AlignmentAlgorithm = value;
                    OnPropertyChanged("SelectedAlignmentAlgorithm");
                }
            }
        }              
        #endregion

        #region Feature Definition Algorithm Parameters
        public double MinimumFeatureLength
        {
            get
            {
                return m_options.LcmsFilteringOptions.FeatureLengthRange.Minimum;
            }
            set
            {
                
                    m_options.LcmsFilteringOptions.FeatureLengthRange.Minimum = value;
                    m_options.LcmsFilteringOptions.FeatureLengthRange.Minimum = value;
                    OnPropertyChanged("MinimumFeatureLength");
                
            }
        }
        public double MaximumFeatureLength
        {
            get
            {
                return m_options.LcmsFilteringOptions.FeatureLengthRange.Maximum;
            }
            set
            {
                
                m_options.LcmsFilteringOptions.FeatureLengthRange.Maximum = value;
                m_options.LcmsFilteringOptions.FeatureLengthRange.Maximum = value;
                OnPropertyChanged("MaximumFeatureLength");
            }
        }
        public double MinimumDeisotopingScore 
        {
            get
            {
                return m_options.MsFilteringOptions.MinimumDeisotopingScore;
            }
            set
            {
                m_options.MsFilteringOptions.MinimumDeisotopingScore = value;
                OnPropertyChanged("MinimumDeisotopingScore");
            }
        }
        public double MinimumIntensity
        {
            get
            {
                return m_options.MsFilteringOptions.MinimumIntensity;
            }
            set
            {
                m_options.MsFilteringOptions.MinimumIntensity = value;
                OnPropertyChanged("MinimumIntensity");
            }
        }
        #endregion

        #region Commands 
        public ICommand ShowAdvancedWindowCommand 
        { 
            get;
            set; 
        }
        public ICommand LoadExistingCommand
        {
            get;
            set;
        }
        public ICommand SaveOptionsCommand { get; set; }
        #endregion
    }

}