﻿using System;
using System.Windows;
using MultiAlign.IO;
using MultiAlign.ViewModels.Plotting;
using MultiAlign.Windows.Plots;
using MultiAlignCore.Data.MetaData;

namespace MultiAlign.Commands.Plotting
{
    public class FeatureDisplayCommand: BaseCommand
    {
        private Window m_window;
        private readonly DatasetInformation m_information;
        private readonly string m_name;

        public FeatureDisplayCommand(DatasetInformation information)
            : base(null, AlwaysPass)
        {
            m_information   = information;
            m_window        = null;
            m_name          = "Features " + information.DatasetName;
        }

        public override void Execute(object parameter)
        {
            
            if (m_information != null)
            {
                if (m_window == null)
                {
                    var features = SingletonDataProviders.Providers.FeatureCache.FindByDatasetId(m_information.DatasetId);
                    if (features == null || features.Count < 1)
                        return;

                    var window         = new LargeFeatureView();
                    var viewModel     = new FeaturesViewModel(features, m_name);
                    window.DataContext              = viewModel;
                    window.WindowStartupLocation    = WindowStartupLocation.CenterScreen;
                    window.Show();
                    
                    m_window         = window;
                    m_window.Closed += m_window_Closed;
                }

                m_window.BringIntoView();
            }
        }


        void m_window_Closed(object sender, EventArgs e)
        {
            m_window = null;
        }
    }
}
