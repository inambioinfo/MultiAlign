﻿using System.Collections.Generic;
using PNNLOmics.Data.Features;
using MultiAlignCore.Algorithms.FeatureFinding;

namespace MultiAlignCore.Algorithms.Features
{
    /// <summary>
    /// Consolidates similar features from the same dataset into one representative LCMS Feature 
    /// based on max abundance.
    /// </summary>
    public class UMCAbundanceConsolidator: LCMSFeatureConsolidator
    {
        public UMCAbundanceConsolidator()
        {
            AbundanceType = AbundanceReportingType.Max;
        }
        /// <summary>
        /// Organizes a list of UMC's into a dataset represented group.
        /// </summary>
        /// <param name="features"></param>
        /// <returns></returns>
        public override Dictionary<int, UMCLight> ConsolidateUMCs(List<UMCLight> features)
        {
            // Map the UMC's to datasets so we can choose only one.
            Dictionary<int, UMCLight> umcs = new Dictionary<int, UMCLight>();
            foreach (UMCLight umc in features)
            {
                int group = umc.GroupID;
                if (!umcs.ContainsKey(group))
                {
                    umcs.Add(group, umc);
                }
                else
                {
                    switch(AbundanceType)
                    {
                        case AbundanceReportingType.Max:
                            if (umc.Abundance > umcs[group].Abundance)
                            {
                                umcs[group] = umc;
                            }
                            break;
                        case AbundanceReportingType.Sum:
                            if (umc.AbundanceSum > umcs[group].AbundanceSum)
                            {
                                umcs[group] = umc;
                            }
                            break;
                    }
                }
            }
            return umcs;
        }

        public AbundanceReportingType AbundanceType
        {
            get;
            set;
        }
    }
}