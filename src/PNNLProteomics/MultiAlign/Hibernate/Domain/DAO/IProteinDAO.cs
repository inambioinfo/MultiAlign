/// <file>IProteinDAO.cs</copyright>
/// <copyright>Pacific Northwest National Laboratory</copyright>
/// <author email="Kevin.Crowell@pnl.gov">Kevin Crowell</author>

using System;
using System.Collections.Generic;
using System.Text;
using MultiAlignEngine.MassTags;

namespace PNNLProteomics.MultiAlign.Hibernate.Domain.DAO
{
	public interface IProteinDAO : IGenericDAO<clsProtein>
    {
		ICollection<clsProtein> FindByProteinString(string proteinString);
    }
}
