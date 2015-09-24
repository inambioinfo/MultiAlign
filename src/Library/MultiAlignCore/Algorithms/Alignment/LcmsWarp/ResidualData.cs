using System.Collections.Generic;

namespace MultiAlignCore.Algorithms.Alignment.LcmsWarp
{
    /// <summary>
    /// Class to hold the residual data from the alignment process
    /// </summary>
    public sealed class ResidualData
    {
        #region Public Properties
        /// <summary>
        /// AutoProperty to hold all the scans from the alignment data
        /// </summary>
        public List<double> Scan { get; set; }

        /// <summary>
        /// AutoProperty to hold all the M/Z from the alignment data
        /// </summary>
        public List<double> Mz { get; set; }

        /// <summary>
        /// AutoProperty to hold all the Linear NETs from the alignment data
        /// </summary>
        public List<double> LinearNet { get; set; }

        /// <summary>
        /// AutoProperty to hold all the NET Errors from the alignment data
        /// </summary>
        public List<double> CustomNet { get; set; }

        /// <summary>
        /// Autoproperty, holds the Aligned net minus the predicted linear net the data
        /// </summary>
        public List<double> LinearCustomNet { get; set; }

        /// <summary>
        /// Autoproperty to hold all the Mass Errors from the alignment data
        /// </summary>
        public List<double> MassError { get; set; }

        /// <summary>
        /// AutoProperty to hold all the corrected mass error (mass error - ppmShift) for the alignment
        /// </summary>
        public List<double> MassErrorCorrected { get; set; }

        /// <summary>
        /// Autoproperty to hold all the Mass Errors from the alignment data
        /// Same as MassError
        /// </summary>
        public List<double> MzMassError { get; set; }

        /// <summary>
        /// AutoProperty to hold all the corrected mass error (mass error - ppmShift) for the alignment
        /// Same as MassErrorCorrected
        /// </summary>
        public List<double> MzMassErrorCorrected { get; set; }

        #endregion
    }
}