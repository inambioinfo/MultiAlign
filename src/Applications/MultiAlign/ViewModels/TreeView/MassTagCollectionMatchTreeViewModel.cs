﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using MultiAlignCore.Data;
using PNNLOmics.Annotations;

namespace MultiAlign.ViewModels.TreeView
{
    public sealed class MassTagCollectionMatchTreeViewModel : TreeItemViewModel
    {
        private readonly ObservableCollection<MassTagMatchTreeViewModel> m_massTags =
            new ObservableCollection<MassTagMatchTreeViewModel>();

        public MassTagCollectionMatchTreeViewModel(List<ClusterToMassTagMap> matches) :
            this(matches, null)
        {
        }

        public MassTagCollectionMatchTreeViewModel(List<ClusterToMassTagMap> matches, TreeItemViewModel parent)
        {
            m_parent = parent;
            m_massTags = new ObservableCollection<MassTagMatchTreeViewModel>();
        }
        [UsedImplicitly]
        public ObservableCollection<MassTagMatchTreeViewModel> MassTags
        {
            get { return m_massTags; }
        }

        public override void LoadChildren()
        {
        }
    }
}