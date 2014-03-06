﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiAlignCore.Data.SequenceData
{
    /// <summary>
    /// Maps the Database Search Sequence to an MSn feature
    /// </summary>
    public class SequenceToMsnFeature
    {
        
        public int Id { get; set; }
        public int DatasetId { get; set; }
        public int SequenceId { get; set; }
        public int MsnFeatureId { get; set; }
        public int UmcFeatureId { get; set; }
        
        public override bool Equals(object obj)
        {
            SequenceToMsnFeature other = (SequenceToMsnFeature)obj;

            if (other.SequenceId != SequenceId)
                return false;
            if (other.MsnFeatureId != MsnFeatureId)
                return false;
            if (other.DatasetId != DatasetId)
                return false;
            if (other.UmcFeatureId != UmcFeatureId)
                return false; 

            return true;
        }

        /// <summary>
        /// Calculates a hash code for the seuqence
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            int hash = 17;                                                            
            hash     = hash * 23 + this.SequenceId.GetHashCode();
            hash     = hash * 23 + this.MsnFeatureId.GetHashCode();
            hash     = hash * 23 + this.DatasetId.GetHashCode();            
            return hash;
        }
    }
}