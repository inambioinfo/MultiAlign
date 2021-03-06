﻿using System.ComponentModel;
using System.Linq;

namespace MultiAlignCore.Algorithms.Alignment.LcmsWarp
{
    using System.Collections.Generic;

    using MultiAlignCore.Algorithms.Alignment.LcmsWarp.MassCalibration;
    using MultiAlignCore.Data.Features;

    /// <summary>
    /// Object to hold the options for LcmsWarp Alignment.
    /// </summary>
    public class LcmsWarpAlignmentOptions
    {
        #region Auto Properties

        /// <summary>
        /// Number of Time Sections
        /// </summary>
        [Description("Percentage of top Abundance features to use for alignment. ")]
        public int NumTimeSections { get; set; }

        /// <summary>
        /// Number of sections to divide the baseline dataset NET values into.
        /// Calculated as NumTimSections * ContractionFactor
        /// </summary>
        public int NumBaselineSections { get; private set; }

        /// <summary>
        /// Contraction factor for the alignment
        /// </summary>
        public int ContractionFactor { get; set; }

        /// <summary>
        /// Maximum width that an alignee dataset section can be expanded when warped.
        /// </summary>
        public int MaxExpansionWidth { get; private set; }

        /// <summary>
        /// Max time distortion at which to filter afterwards
        /// </summary>
        public int MaxTimeDistortion { get; set; }

        /// <summary>
        /// Max number of promiscuous points to use for alignment
        /// </summary>
        public int MaxPromiscuity { get; set; }

        /// <summary>
        /// Flag for whether to even use promiscuous points or not
        /// </summary>
        /// <remarks>
        /// This should be true when aligning to AMT tag databases
        /// It should be false when aligning MS data to MS data
        /// Auto-set to True if AlignToMassTagDatabase is set to true
        /// </remarks>
        public bool UsePromiscuousPoints { get; set; }

        /// <summary>
        /// Flag for whether to use LSQ during mass calibration
        /// </summary>
        public bool MassCalibUseLsq { get; set; }

        /// <summary>
        /// Window for the Mass calibration alignment (in ppm)
        /// </summary>
        public double MassCalibrationWindow { get; set; }

        /// <summary>
        /// Number of Mass slices for the mass calibration
        /// </summary>
        public int MassCalibNumXSlices { get; set; }

        /// <summary>
        /// Number of NET slices for the mass calibration
        /// </summary>
        public int MassCalibNumYSlices { get; set; }

        /// <summary>
        /// Number of jumps for the alignment function
        /// </summary>
        public int MassCalibMaxJump { get; set; }

        /// <summary>
        /// Z Tolerance for the calibration that Central Regression would use
        /// </summary>
        public double MassCalibMaxZScore { get; set; }

        /// <summary>
        /// Z Tolerance for the calibration that LSQ Regression would use
        /// </summary>
        public double MassCalibLsqMaxZScore { get; set; }

        /// <summary>
        /// Number of Knots that LSQ Regression would use
        /// </summary>
        public int MassCalibLsqNumKnots { get; set; }

        /// <summary>
        /// Mass tolerance (in ppm) for the Alignment and warping
        /// </summary>
        public double MassTolerance { get; set; }

        /// <summary>
        /// NET tolerance for the Alignment and warping
        /// </summary>
        public double NetTolerance { get; set; }

        /// <summary>
        /// The type of alignment which will be performed; either Net warping or Net-Mass warping
        /// </summary>
        public LcmsWarpAlignmentType AlignType { get; set; }

        /// <summary>
        /// The type of calibration which will be performed; Either MZ, Scan or hybrid
        /// </summary>
        public LcmsWarpCalibrationType CalibrationType { get; set; }

        /// <summary>
        /// When using AMT tags from a database, minimum NET filter
        /// </summary>
        /// <remarks>AMTTagFilterNETMin is ignored if AMTTagFilterNETMax is less than or equal to AMTTagFilterNETMin</remarks>
        public double AMTTagFilterNETMin { get; set; }

        /// <summary>
        /// When using AMT tags from a database, maximum NET filter
        /// </summary>
        /// <remarks>AMTTagFilterNETMin is ignored if AMTTagFilterNETMax is less than or equal to AMTTagFilterNETMin</remarks>
        public double AMTTagFilterNETMax { get; set; }

        /// <summary>
        /// When using AMT tags from a database, minimum monoisotopic mass filter
        /// </summary>
        /// <remarks>AMTTagFilterMassMin is ignored if AMTTagFilterMassMax is less than or equal to AMTTagFilterMassMin</remarks>
        public double AMTTagFilterMassMin { get; set; }

        /// <summary>
        /// When using AMT tags from a database, maximum monoisotopic mass filter
        /// </summary>
        /// <remarks>AMTTagFilterMassMin is ignored if AMTTagFilterMassMax is less than or equal to AMTTagFilterMassMin</remarks>
        public double AMTTagFilterMassMax { get; set; }

        /// <summary>
        /// When using AMT tags from a database, only use those AMT tags with an observation count of this value or larger
        /// </summary>
        /// <remarks>
        /// If all of the AMT tags have an observation count of 0, then they will all be used
        /// (i.e. this filter will be effectively ignored)</remarks>
        public int MinimumAMTTagObsCount { get; set; }

        /// <summary>
        /// How wide the Mass histogram bins are (in ppm)
        /// </summary>
        public double MassBinSize { get; set; }

        /// <summary>
        /// How wide the NET histogram bins are
        /// </summary>
        public double NetBinSize { get; set; }

        /// <summary>
        /// How wide the Drift time histogram bins are
        /// </summary>
        public double DriftTimeBinSize { get; set; }

        /// <summary>
        /// Abundance percentage under which to filter alignment.
        /// Set to 0 means all features are matched, set to 100 means no features are matched,
        /// set to 33 the top 67% of features sorted by abundance are matched
        /// </summary>
        public int TopFeatureAbundancePercent { get; set; }

        /// <summary>
        /// Flag for whether to store the alignment function from one alignment to another
        /// </summary>
        public bool StoreAlignmentFunction { get; set; }

        /// <summary>
        /// The type of aligner the processor uses.
        /// </summary>
        public FeatureAlignmentType AlignmentAlgorithmType { get; set; }

        /// <summary>
        /// When true, standardizes the match scores of each subsection in the heatmap scores
        /// </summary>
        public bool StandardizeHeatScores { get; set; }

        #endregion

        private bool _AlignToMassTagDatabase;

        /// <summary>
        /// Flag for if the warper is aligning to a database of mass tags
        /// </summary>
        public bool AlignToMassTagDatabase
        {
            get
            {
                return _AlignToMassTagDatabase;
            }
            set
            {
                _AlignToMassTagDatabase = value;
                if (_AlignToMassTagDatabase)
                {
                    UsePromiscuousPoints = true;
                }
            }
        }

        /// <summary>
        /// The LCMSWarp regression type to use; based on MassCalibUseLsq
        /// </summary>
        [MultiAlignCore.IO.Options.IgnoreOptionProperty]
        public LcmsWarpRegressionType RegressionType
        {
            get
            {
                if (MassCalibUseLsq)
                {
                    return LcmsWarpRegressionType.Hybrid;
                }
                return LcmsWarpRegressionType.Central;
            }
        }

        [MultiAlignCore.IO.Options.IgnoreOptionProperty]
        public List<FeatureLight.SeparationTypes> SeparationTypes { get; set; }

        public FeatureLight.SeparationTypes[] SeparationTypesArray
        {
            get { return SeparationTypes.ToArray(); }
            set { SeparationTypes = value.ToList(); }
        }

        /// <summary>
        /// Default constructor, initializes every value to commonly used values and flags
        /// </summary>
        public LcmsWarpAlignmentOptions()
        {
            NumTimeSections = 100;              // 100 in VIPER
            NumBaselineSections = this.NumTimeSections * this.ContractionFactor;
            TopFeatureAbundancePercent = 0;
            ContractionFactor = 3;              //   3 in VIPER
            MaxExpansionWidth = this.ContractionFactor * this.ContractionFactor;
            MaxTimeDistortion = 10;             //  10 in VIPER
            MaxPromiscuity = 3;                 //   2 in VIPER
            UsePromiscuousPoints = false;       // false for Dataset to Dataset alignment; true for Dataset to AMT tag alignment
            MassCalibUseLsq = false;
            MassCalibrationWindow = 40.0;       //  50 ppm in VIPER
            MassCalibNumXSlices = 12;           //  20 in VIPER
            MassCalibNumYSlices = 50;           // 100 in VIPER
            MassCalibMaxJump = 20;              //  50 in VIPER
            MassCalibMaxZScore = 3;             //   3 in VIPER
            MassCalibLsqMaxZScore = 2.5;        //   3 in VIPER
            MassCalibLsqNumKnots = 12;          //  12 in VIPER
            MassTolerance = 10;                 //  10 in VIPER
            NetTolerance = 0.02;                //   0.02 in VIPER

            AlignType = LcmsWarpAlignmentType.NET_MASS_WARP;
            CalibrationType = LcmsWarpCalibrationType.Both;

            AlignToMassTagDatabase = false;

            AMTTagFilterNETMin = 0;
            AMTTagFilterNETMax = 0;

            AMTTagFilterMassMin = 0;
            AMTTagFilterMassMax = 0;

            MinimumAMTTagObsCount = 5;          // 5 in VIPER

            MassBinSize = 0.2;                  // 0.2 in VIPER
            NetBinSize = 0.001;                 // 0.001 in VIPER
            DriftTimeBinSize = 0.03;
            StoreAlignmentFunction = false;
            AlignmentAlgorithmType = FeatureAlignmentType.LCMS_WARP;
            SeparationTypes = new List<FeatureLight.SeparationTypes>();

            StandardizeHeatScores = true;
        }
    }
}