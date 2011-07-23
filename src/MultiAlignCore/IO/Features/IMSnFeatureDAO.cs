using System;
using System.Collections.Generic;
using MultiAlignEngine.Features;
using PNNLOmics.Data;

namespace MultiAlignCore.IO.Features
{
    public interface IMSnFeatureDAO : IGenericDAO<MSSpectra>
	{
        /// <summary>
        /// Finds MSMS Spectra stored in the database.
        /// </summary>
        /// <param name="datasetId"></param>
        /// <returns></returns>
        List<MSSpectra> FindByDatasetId(int datasetId);
	}
}