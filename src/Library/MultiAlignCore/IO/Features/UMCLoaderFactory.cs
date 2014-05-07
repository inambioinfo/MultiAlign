using System;
using System.Collections.Generic;
using System.IO;
using PNNLOmics.Data.Features;
using PNNLOmicsIO.IO;

namespace MultiAlignCore.IO.Features
{
    /// <summary>
    /// Loads UMC's from the given sources.
    /// </summary>
    public static class UmcLoaderFactory
    {
        /// <summary>
        /// Status event fired when umc's are loaded.
        /// </summary>
        public static event EventHandler<UmcLoadingEventArgs> Status;
        /// <summary>
        /// Helper function to tell listeners about data.
        /// </summary>
        /// <param name="message"></param>
        private static void UpdateStatus(string message)
        {
            if (Status != null)
            {
                Status(null, new UmcLoadingEventArgs(message));
            }
        }

        /// <summary>
        /// Loads feature data from the files provided.
        /// </summary>
        /// <returns></returns>
        public static IList<UMCLight> LoadUmcFeatureData(string path,
                                                         int  datasetId,
                                                         IUmcDAO featureCache)
        {
            var features  = new List<UMCLight>();
            var extension = Path.GetExtension(path);
            if (extension == null) return features;

            extension = extension.ToUpper();            
            switch (extension)
            {
                case ".TXT":
                    var umcReader = new LCMSFeatureFileReader(path);
                    features = umcReader.GetUmcList();
                    break;
                case ".DB3":
                    features = featureCache.FindByDatasetId(datasetId);
                    break;
            }
            return features;
        }

        /// <summary>
        /// Determines if the features came from the database or a feature file.
        /// </summary>
        /// <returns></returns>
        public static bool AreExistingFeatures(string path)
        {
            var areExisting = false;
            var extension = Path.GetExtension(path);
            if (extension == null) return false;

            extension = extension.ToUpper();
            switch (extension)
            {
                case ".DB3":
                    areExisting = true;
                    break;
            }
            return areExisting;
        }

        /// <summary>
        /// Loads MS Features from a CSV file or existing database.
        /// </summary>
        /// <returns></returns>
        public static List<MSFeatureLight> LoadMsFeatureData(string path)
        {
            var msFeatures      = new List<MSFeatureLight>();
            var extension = Path.GetExtension(path);
            if (extension == null) return msFeatures;

            extension    = extension.ToUpper();
            switch (extension)
            {
                default:
                    var reader = new MsFeatureLightFileReader { Delimeter = "," };
                    var newMsFeatures = reader.ReadFile(path);
                    msFeatures.AddRange(newMsFeatures);                    
                    UpdateStatus("Loaded features from the CSV files.");
                    break;                    
            }

            return msFeatures;
        }
    }
}
