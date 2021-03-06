#region

using System.Collections.Generic;
using MultiAlignCore.Data.MassTags;

#endregion

namespace MultiAlignCore.IO.Proteins
{
    public interface IProteinDAO : IGenericDAO<Protein>
    {
        ICollection<Protein> FindByProteinString(string proteinString);
    }
}