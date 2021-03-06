﻿using System.Collections.ObjectModel;
using MultiAlign.ViewModels.Proteins;
using MultiAlignCore.Data;
using MultiAlignCore.Data.MassTags;

namespace MultiAlign.ViewModels
{
    /// <summary>
    ///     View model for a mass tag that was matched to a cluster
    /// </summary>
    public class MassTagMatchedViewModel : ViewModelBase
    {
        private readonly ClusterToMassTagMap m_match;
        private readonly MassTagToCluster m_tag;

        public MassTagMatchedViewModel(ClusterToMassTagMap match)
        {
            m_match = match;
            m_tag = match.MassTag;

            MatchedProteins = new ObservableCollection<ProteinViewModel>();
            LoadProteinData(m_tag);
        }

        /// <summary>
        ///     Gets or sets the matched proteins.
        /// </summary>
        public ObservableCollection<ProteinViewModel> MatchedProteins { get; set; }

        public MassTagLight MassTag
        {
            get
            {
                if (m_tag == null)
                    return null;

                return m_tag.MassTag;
            }
        }

        public double STACScore
        {
            get { return m_match.StacScore; }
        }

        public double STACUPScore
        {
            get { return m_match.StacUP; }
        }

        private void LoadProteinData(MassTagToCluster tag)
        {
            MatchedProteins.Clear();
            tag.MatchingProteins.ForEach(x => MatchedProteins.Add(new ProteinViewModel(x.Protein)));
        }
    }
}