﻿#region

using System.Collections.Generic;
using MultiAlignCore.Data;

#endregion

namespace MultiAlignCore.IO.MassTags
{
    public interface IMassTagMatchDAO : IGenericDAO<ClusterToMassTagMap>
    {
        void ClearAllMatches();
        List<ClusterToMassTagMap> FindByClusterId(int id);
    }
}