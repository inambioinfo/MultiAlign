﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InformedProteomics.Backend.Data.Spectrometry;
using MultiAlignCore.IO.RawData;
using NHibernate.Mapping;
using PNNLOmics.Algorithms;
using PNNLOmics.Algorithms.Chromatograms;
using PNNLOmics.Algorithms.SpectralProcessing;
using PNNLOmics.Data;
using PNNLOmics.Data.Features;
using PNNLOmics.Extensions;

namespace MultiAlignCore.Algorithms.Chromatograms
{
    public class XicCreator
    {
        private const int CONST_POLYNOMIAL_ORDER = 3;

        public XicCreator()
        {
            ScanWindowSize = 100;
            FragmentationSizeWindow = .5;
            NumberOfPoints = 5;
        }

        public event EventHandler<ProgressNotifierArgs> Progress;

        private void OnProgress(string message)
        {
            if (Progress != null)
            {
                Progress(this, new ProgressNotifierArgs(message));
            }
        }

        public IEnumerable<UMCLight> CreateXic(IList<UMCLight> features,
                              double massError,
                              InformedProteomicsReader provider)
        {
            // this algorithm works as follows
            // 
            //  PART A - Build the XIC target list 
            //  For each UMC Light , find the XIC representation
            //      for each charge in a feature          
            //          from start scan to end scan
            //              1. Compute a lower / upper m/z bound
            //              2. build an XIC chomatogram object
            //              3. reference the original UMC Feature -- this allows us to easily add 
            //                  chromatograms to the corresponding feature
            //              4. store the chomatogram (with unique ID across all features)
            //
            //  PART B - Read Data From File
            //  Sort the list of XIC's by scan
            //  for each scan s = start scan to end scan 
            //      1. find all xic's that start before and end after s - 
            //          a. cache these xics in a dictionary based on unique id
            //          b. NOTE: this is why we sort so we can do an O(N) search for 
            //             all XIC's that need data from this scan s
            //      2.  Then for each XIC that needs data
            //          a. Pull intensity data from lower / upper m/z bound 
            //          b. create an MS Feature
            //          c. store in original UMC Feature
            //          d. Test to see if the XIC is done building (Intensity < 1 or s > scan end)
            //      3. Remove features that are done building from cache
            // 
            //  CONCLUSIONS
            //  Building UMC's then takes linear time  (well O(N Lg N) time if you consider sort)
            //      and theoretically is only bounded by the time it takes to read an entire raw file
            // 
            if (features.Count <= 0)
                throw new Exception("No features were available to create XIC's from");

            var minScan = Math.Max(1, features.Min(x => x.Scan - ScanWindowSize));
            var maxScan = features.Max(x => x.Scan + ScanWindowSize);

            OnProgress("Sorting features for optimized scan partitioning");
            // PART A 
            // Map the feature ID to the xic based features
            var xicFeatures = new SortedSet<XicFeature>();
            var allFeatures = CreateXicTargets(features, massError);

            // PART B 
            // sort the features...
            var featureCount = allFeatures.Count;
            //allFeatures = allFeatures.OrderBy(x => x.StartScan).ToList();

            // This map tracks all possible features to keep            

            var msFeatureId = 0;

            // This list stores a temporary amount of parent MS features
            // so that we can link MS/MS spectra to MS Features
            var parentMsList = new List<MSFeatureLight>();

            // Creates a comparison function for building a BST from a spectrum.
            var msmsFeatureId = 0;

            var totalScans = provider.GetTotalScans(0);
            OnProgress(string.Format("Analyzing {0} scans", totalScans));

            /**/

            var ipr = provider.GetReaderForGroup(0);

            var ms1ScanNums = ipr.GetScanNumbers(1);
            var precursorScans = new Dictionary<int, List<MSFeatureLight>>(ms1ScanNums.Count + 1);
            foreach (var scanNum in ms1ScanNums)
            {
                precursorScans.Add(scanNum, new List<MSFeatureLight>());
            }

            // 
            // Notes: 
            // MsFeatures is what needs to be populated
            // xic.Feature.AddChildFeature(msFeature) adds to xic.Feature.MsFeatures
            // 
            foreach (var feature in allFeatures)
            //Parallel.ForEach(allFeatures, feature =>
            {
                var target = feature.StartScan + (feature.EndScan - feature.StartScan) / 2;
                var xic = ipr.GetPrecursorExtractedIonChromatogram(feature.LowMz, feature.HighMz, target);
                //var xic = ipr.GetPrecursorExtractedIonChromatogram(feature.LowMz, feature.HighMz);
                //var validXic = xic.Where(x => feature.StartScan <= x.ScanNum).ToList();
                //validXic.Sort();

                //var scans = validXic.Select(x => x.ScanNum).Where(x => x >= feature.StartScan).ToList();
                //foreach (var scan in scans)
                foreach (var xicp in xic)
                {
                    //var sxic = validXic.Where(x => x.ScanNum == scan).ToList();
                    //var summedIntensity = sxic.Sum(x => x.Intensity); // InformedProteomics gives us base peak intensity per scan for the xic

                    if (xicp.ScanNum < feature.StartScan)
                    {
                        continue;
                    }

                    // See if we need to remove this feature
                    // We only do so if the intensity has dropped off and we are past the end of the feature.
                    //if (summedIntensity < 1 && scan > feature.EndScan)
                    if (xicp.Intensity < 1 && xicp.ScanNum > feature.EndScan)
                    {
                        break;
                    }

                    var umc = feature.Feature;

                    // otherwise create a new feature here...
                    var msFeature = new MSFeatureLight
                    {
                        ChargeState = feature.ChargeState,
                        Mz = feature.Mz,
                        MassMonoisotopic = umc.MassMonoisotopic,
                        //Scan = scan,
                        Scan = xicp.ScanNum,
                        //Abundance = Convert.ToInt64(summedIntensity),
                        Abundance = Convert.ToInt64(xicp.Intensity),
                        //Id = msFeatureId++,
                        Id = Interlocked.Increment(ref msFeatureId) - 1,
                        DriftTime = umc.DriftTime,
                        //Net = ipr.GetElutionTime(scan),
                        Net = ipr.GetElutionTime(xicp.ScanNum),
                        GroupId = umc.GroupId
                    };
                    //parentMsList.Add(msFeature);
                    //precursorScans[scan].Add(msFeature);
                    //precursorScans[xicp.ScanNum].Add(msFeature);
                    //feature.Feature.AddChildFeature(msFeature); // Adds to MsFeatures
                    lock (precursorScans[xicp.ScanNum])
                    {
                        precursorScans[xicp.ScanNum].Add(msFeature);
                    }
                    lock (feature.Feature)
                    {
                        feature.Feature.AddChildFeature(msFeature); // Adds to MsFeatures
                    }
                }
                //Parallel.ForEach(xic.Where(xicp => xicp.ScanNum >= feature.StartScan).TakeWhile(xicp => xicp.Intensity < 1 && xicp.ScanNum > feature.EndScan), xicp =>
                //xic.TakeWhile(xicp => xicp.Intensity < 1 && xicp.ScanNum > feature.EndScan).AsParallel().Where(xicp => xicp.ScanNum >= feature.StartScan).ForAll(xicp =>
                //{
                //    //var sxic = validXic.Where(x => x.ScanNum == scan).ToList();
                //    //var summedIntensity = sxic.Sum(x => x.Intensity); // InformedProteomics gives us base peak intensity per scan for the xic
                //
                //    //if (xicp.ScanNum < feature.StartScan)
                //    //{
                //    //    return;
                //    //}
                //
                //    // See if we need to remove this feature
                //    // We only do so if the intensity has dropped off and we are past the end of the feature.
                //    //if (summedIntensity < 1 && scan > feature.EndScan)
                //    //if (xicp.Intensity < 1 && xicp.ScanNum > feature.EndScan)
                //    //{
                //    //    return;
                //    //}
                //
                //    var umc = feature.Feature;
                //
                //    // otherwise create a new feature here...
                //    var msFeature = new MSFeatureLight
                //    {
                //        ChargeState = feature.ChargeState,
                //        Mz = feature.Mz,
                //        MassMonoisotopic = umc.MassMonoisotopic,
                //        //Scan = scan,
                //        Scan = xicp.ScanNum,
                //        //Abundance = Convert.ToInt64(summedIntensity),
                //        Abundance = Convert.ToInt64(xicp.Intensity),
                //        //Id = msFeatureId++,
                //        Id = Interlocked.Increment(ref msFeatureId) - 1,
                //        DriftTime = umc.DriftTime,
                //        //Net = ipr.GetElutionTime(scan),
                //        Net = ipr.GetElutionTime(xicp.ScanNum),
                //        GroupId = umc.GroupId
                //    };
                //    //parentMsList.Add(msFeature);
                //    //precursorScans[scan].Add(msFeature);
                //    lock (precursorScans[xicp.ScanNum])
                //    {
                //        precursorScans[xicp.ScanNum].Add(msFeature);
                //    }
                //    lock (feature.Feature)
                //    {
                //        feature.Feature.AddChildFeature(msFeature); // Adds to MsFeatures
                //    }
                //});
            }//);

            //int lastMs1ScanNum = -1;
            var ms1Scans = ms1ScanNums.GetEnumerator();
            int lastMs1ScanNum = -1;
            if (ms1Scans.MoveNext())
            {
                lastMs1ScanNum = ms1Scans.Current;
                int nextMs1ScanNum = lastMs1ScanNum;

                //Spectrum lastMs1Spectrum = null;
                //for (int i = ipr.MinLcScan; i <= ipr.MaxLcScan; i++)
                foreach (var scan in ipr.GetScanNumbers(2))
                {
                    if (lastMs1ScanNum == nextMs1ScanNum && ms1Scans.MoveNext())
                    {
                        nextMs1ScanNum = ms1Scans.Current;
                    }
                    if (scan > lastMs1ScanNum && ms1Scans.MoveNext())
                    {
                        lastMs1ScanNum = ms1Scans.Current;
                    }
                    //var spectrum = ipr.GetSpectrum(i, false);
                    //var spectrum = ipr.GetSpectrum(scan, false);
                    //if (spectrum is ProductSpectrum)
                    //{
                    //var summary = provider.GetScanSummary(i, 0);
                    var summary = provider.GetScanSummary(scan, 0);
                    // If it is an MS 2 spectra... then let's link it to the parent MS
                    // Feature
                    //var matching = precursorScans[lastMs1ScanNum].FindAll(
                    //    x => Math.Abs(x.Mz - summary.PrecursorMz) <= FragmentationSizeWindow
                    //    );

                    //foreach (var match in matching)
                    //foreach (var match in precursorScans[lastMs1ScanNum].Where(x => Math.Abs(x.Mz - summary.PrecursorMz) <= FragmentationSizeWindow))
                    //{
                    //    // We create multiple spectra because this guy is matched to multiple ms
                    //    // features
                    //    var spectraData = new MSSpectra
                    //    {
                    //        Id = msmsFeatureId,
                    //        ScanMetaData = summary,
                    //        CollisionType = summary.CollisionType,
                    //        //Scan = spectrum.ScanNum,
                    //        Scan = scan,
                    //        MsLevel = summary.MsLevel,
                    //        PrecursorMz = summary.PrecursorMz,
                    //        TotalIonCurrent = summary.TotalIonCurrent
                    //    };
                    //
                    //    spectraData.ParentFeature = match;
                    //    match.MSnSpectra.Add(spectraData);
                    //
                    //    msmsFeatureId++;
                    //}
                    //Parallel.ForEach(precursorScans[lastMs1ScanNum]
                    //    .Where(x => Math.Abs(x.Mz - summary.PrecursorMz) <= FragmentationSizeWindow), match =>
                    //Parallel.ForEach(precursorScans[lastMs1ScanNum], match =>
                    precursorScans[lastMs1ScanNum].AsParallel().Where(x => Math.Abs(x.Mz - summary.PrecursorMz) <= FragmentationSizeWindow).ForAll(match =>
                        {
                            //if (Math.Abs(match.Mz - summary.PrecursorMz) > FragmentationSizeWindow)
                            //{
                            //    return;
                            //}
                            int featureId = Interlocked.Increment(ref msmsFeatureId) - 1;
                            // We create multiple spectra because this guy is matched to multiple ms
                            // features
                            var spectraData = new MSSpectra
                            {
                                //Id = msmsFeatureId,
                                Id = featureId,
                                ScanMetaData = summary,
                                CollisionType = summary.CollisionType,
                                //Scan = spectrum.ScanNum,
                                Scan = scan,
                                MsLevel = summary.MsLevel,
                                PrecursorMz = summary.PrecursorMz,
                                TotalIonCurrent = summary.TotalIonCurrent
                            };

                            spectraData.ParentFeature = match;
                            match.MSnSpectra.Add(spectraData);

                            //msmsFeatureId++;
                        });
                    //}
                    //else
                    //{
                    //    lastMs1ScanNum = spectrum.ScanNum;
                    //    //lastMs1Spectrum = spectrum;
                    //}
                }
            }

            /*/
            
            allFeatures = allFeatures.OrderBy(x => x.StartScan).ToList();
            
            // Iterate over all the scans...
            for (var currentScan = minScan; currentScan < maxScan && currentScan <= totalScans; currentScan++)
            {
                // Find any features that need data from this scan 
                var featureIndex = 0;
                while (featureIndex < featureCount)
                {
                    var xicFeature = allFeatures[featureIndex];
                    // This means that no new features were eluting with this scan....
                    if (xicFeature.StartScan > currentScan)
                        break;

                    // This means that there is a new feature...
                    if (currentScan <= xicFeature.EndScan)
                    {
                        if (!xicFeatures.Contains(xicFeature))
                        {
                            xicFeatures.Add(xicFeature);
                        }
                        
                    }
                    featureIndex++;
                }

                // Skip pulling the data from the file if there is nothing to pull from.
                if (xicFeatures.Count < 1)
                    continue;

                // Here We link the MSMS Spectra to the UMC Features
                ScanSummary summary;
                var spectrum = provider.GetRawSpectra(currentScan, 0, 1, out summary);

               
                if (summary.MsLevel > 1)
                {
                    // If it is an MS 2 spectra... then let's link it to the parent MS
                    // Feature
                    var matching = parentMsList.FindAll(
                        x => Math.Abs(x.Mz - summary.PrecursorMz) <= FragmentationSizeWindow
                        );

                    foreach (var match in matching)
                    {
                        // We create multiple spectra because this guy is matched to multiple ms
                        // features
                        var spectraData = new MSSpectra
                        {
                            Id = msmsFeatureId,
                            ScanMetaData = summary,
                            CollisionType = summary.CollisionType,
                            Scan = currentScan,
                            MsLevel = summary.MsLevel,
                            PrecursorMz = summary.PrecursorMz,
                            TotalIonCurrent = summary.TotalIonCurrent
                        };

                        match.MSnSpectra.Add(spectraData);
                        spectraData.ParentFeature = match;
                    }

                    if (spectrum != null)
                        spectrum.Clear();
                    msmsFeatureId++;

                    continue;
                }


                var mzList = new double[spectrum.Count];
                var intensityList = new double[spectrum.Count];                
                XYData.XYDataListToArrays(spectrum, mzList, intensityList);
                Array.Sort(mzList, intensityList);

                // Tracks which spectra need to be removed from the cache
                var toRemove = new List<XicFeature>();

                // Tracks which features we need to link to MSMS spectra with
                parentMsList.Clear();

                // now we iterate through all features that need data from this scan
                
                foreach (var xic in xicFeatures)
                {
                    var lower = xic.LowMz;
                    var higher = xic.HighMz;

                    var startIndex = Array.BinarySearch(mzList, lower);
                    // A bitwise complement of the index, so use the bitwise complement
                    if (startIndex < 0)
                        startIndex = ~startIndex;

                    double summedIntensity = 0;

                    if (startIndex < mzList.Count() && mzList[startIndex] < lower)
                    {
                        // All data in the list is lighter than lower; nothing to sum
                    }
                    else
                    {
                        while (startIndex < mzList.Count() && mzList[startIndex] <= higher)
                        {
                            summedIntensity += intensityList[startIndex];
                            startIndex++;
                        }    
                    }

                    // See if we need to remove this feature
                    // We only do so if the intensity has dropped off and we are past the end of the feature.
                    if (summedIntensity < 1 && currentScan > xic.EndScan)
                    {
                        toRemove.Add(xic);
                        continue;
                    }

                    var umc = xic.Feature;

                    // otherwise create a new feature here...
                    var msFeature = new MSFeatureLight
                    {
                        ChargeState = xic.ChargeState,
                        Mz = xic.Mz,
                        MassMonoisotopic = umc.MassMonoisotopic,
                        Scan = currentScan,
                        Abundance = Convert.ToInt64(summedIntensity),
                        Id = msFeatureId++,
                        DriftTime = umc.DriftTime,
                        Net = currentScan,
                        GroupId = umc.GroupId
                    };
                    parentMsList.Add(msFeature);
                    xic.Feature.AddChildFeature(msFeature); // Adds to MsFeatures
                }

                // Remove features that end their elution prior to the current scan
                toRemove.ForEach(x => xicFeatures.Remove(x));
            }
            /**/
            OnProgress("Filtering bad features with no data.");
            features = features.Where(x => x.MsFeatures.Count > 0).ToList();

            OnProgress("Refining XIC features.");
            return RefineFeatureXics(features);

        }

        //public IEnumerable<UMCLight> CreateXic(IList<UMCLight> features,
        //              double massError,
        //              ISpectraProvider provider)
        //{
        //    // this algorithm works as follows
        //    // 
        //    //  PART A - Build the XIC target list 
        //    //  For each UMC Light , find the XIC representation
        //    //      for each charge in a feature          
        //    //          from start scan to end scan
        //    //              1. Compute a lower / upper m/z bound
        //    //              2. build an XIC chomatogram object
        //    //              3. reference the original UMC Feature -- this allows us to easily add 
        //    //                  chromatograms to the corresponding feature
        //    //              4. store the chomatogram (with unique ID across all features)
        //    //
        //    //  PART B - Read Data From File
        //    //  Sort the list of XIC's by scan
        //    //  for each scan s = start scan to end scan 
        //    //      1. find all xic's that start before and end after s - 
        //    //          a. cache these xics in a dictionary based on unique id
        //    //          b. NOTE: this is why we sort so we can do an O(N) search for 
        //    //             all XIC's that need data from this scan s
        //    //      2.  Then for each XIC that needs data
        //    //          a. Pull intensity data from lower / upper m/z bound 
        //    //          b. create an MS Feature
        //    //          c. store in original UMC Feature
        //    //          d. Test to see if the XIC is done building (Intensity < 1 or s > scan end)
        //    //      3. Remove features that are done building from cache
        //    // 
        //    //  CONCLUSIONS
        //    //  Building UMC's then takes linear time  (well O(N Lg N) time if you consider sort)
        //    //      and theoretically is only bounded by the time it takes to read an entire raw file
        //    // 
        //    if (features.Count <= 0)
        //        throw new Exception("No features were available to create XIC's from");

        //    var minScan = Math.Max(1, features.Min(x => x.Scan - ScanWindowSize));
        //    var maxScan = features.Max(x => x.Scan + ScanWindowSize);

        //    OnProgress("Sorting features for optimized scan partitioning");
        //    // PART A 
        //    // Map the feature ID to the xic based features
        //    var xicFeatures = new SortedSet<XicFeature>();
        //    var allFeatures = CreateXicTargets(features, massError);

        //    // PART B 
        //    // sort the features...
        //    var featureCount = allFeatures.Count;
        //    allFeatures = allFeatures.OrderBy(x => x.StartScan).ToList();

        //    // This map tracks all possible features to keep            

        //    var msFeatureId = 0;

        //    // This list stores a temporary amount of parent MS features
        //    // so that we can link MS/MS spectra to MS Features
        //    var parentMsList = new List<MSFeatureLight>();

        //    // Creates a comparison function for building a BST from a spectrum.
        //    var msmsFeatureId = 0;

        //    var totalScans = provider.GetTotalScans(0);
        //    OnProgress(string.Format("Analyzing {0} scans", totalScans));


        //    // Iterate over all the scans...
        //    for (var currentScan = minScan; currentScan < maxScan && currentScan <= totalScans; currentScan++)
        //    {
        //        // Find any features that need data from this scan 
        //        var featureIndex = 0;
        //        while (featureIndex < featureCount)
        //        {
        //            var xicFeature = allFeatures[featureIndex];
        //            // This means that no new features were eluting with this scan....
        //            if (xicFeature.StartScan > currentScan)
        //                break;

        //            // This means that there is a new feature...
        //            if (currentScan <= xicFeature.EndScan)
        //            {
        //                if (!xicFeatures.Contains(xicFeature))
        //                {
        //                    xicFeatures.Add(xicFeature);
        //                }

        //            }
        //            featureIndex++;
        //        }

        //        // Skip pulling the data from the file if there is nothing to pull from.
        //        if (xicFeatures.Count < 1)
        //            continue;

        //        // Here We link the MSMS Spectra to the UMC Features
        //        ScanSummary summary;
        //        var spectrum = provider.GetRawSpectra(currentScan, 0, 1, out summary);


        //        if (summary.MsLevel > 1)
        //        {
        //            // If it is an MS 2 spectra... then let's link it to the parent MS
        //            // Feature
        //            var matching = parentMsList.FindAll(
        //                x => Math.Abs(x.Mz - summary.PrecursorMz) <= FragmentationSizeWindow
        //                );

        //            foreach (var match in matching)
        //            {
        //                // We create multiple spectra because this guy is matched to multiple ms
        //                // features
        //                var spectraData = new MSSpectra
        //                {
        //                    Id = msmsFeatureId,
        //                    ScanMetaData = summary,
        //                    CollisionType = summary.CollisionType,
        //                    Scan = currentScan,
        //                    MsLevel = summary.MsLevel,
        //                    PrecursorMz = summary.PrecursorMz,
        //                    TotalIonCurrent = summary.TotalIonCurrent
        //                };

        //                match.MSnSpectra.Add(spectraData);
        //                spectraData.ParentFeature = match;
        //            }

        //            if (spectrum != null)
        //                spectrum.Clear();
        //            msmsFeatureId++;

        //            continue;
        //        }


        //        var mzList = new double[spectrum.Count];
        //        var intensityList = new double[spectrum.Count];
        //        XYData.XYDataListToArrays(spectrum, mzList, intensityList);
        //        System.Array.Sort(mzList, intensityList);

        //        // Tracks which spectra need to be removed from the cache
        //        var toRemove = new List<XicFeature>();

        //        // Tracks which features we need to link to MSMS spectra with
        //        parentMsList.Clear();

        //        // now we iterate through all features that need data from this scan

        //        foreach (var xic in xicFeatures)
        //        {
        //            var lower = xic.LowMz;
        //            var higher = xic.HighMz;

        //            var startIndex = System.Array.BinarySearch(mzList, lower);
        //            // A bitwise complement of the index, so use the bitwise complement
        //            if (startIndex < 0)
        //                startIndex = ~startIndex;

        //            double summedIntensity = 0;

        //            if (startIndex < mzList.Count() && mzList[startIndex] < lower)
        //            {
        //                // All data in the list is lighter than lower; nothing to sum
        //            }
        //            else
        //            {
        //                while (startIndex < mzList.Count() && mzList[startIndex] <= higher)
        //                {
        //                    summedIntensity += intensityList[startIndex];
        //                    startIndex++;
        //                }
        //            }

        //            // See if we need to remove this feature
        //            // We only do so if the intensity has dropped off and we are past the end of the feature.
        //            if (summedIntensity < 1 && currentScan > xic.EndScan)
        //            {
        //                toRemove.Add(xic);
        //                continue;
        //            }

        //            var umc = xic.Feature;

        //            // otherwise create a new feature here...
        //            var msFeature = new MSFeatureLight
        //            {
        //                ChargeState = xic.ChargeState,
        //                Mz = xic.Mz,
        //                MassMonoisotopic = umc.MassMonoisotopic,
        //                Scan = currentScan,
        //                Abundance = Convert.ToInt64(summedIntensity),
        //                Id = msFeatureId++,
        //                DriftTime = umc.DriftTime,
        //                Net = currentScan,
        //                GroupId = umc.GroupId
        //            };
        //            parentMsList.Add(msFeature);
        //            xic.Feature.AddChildFeature(msFeature);
        //        }

        //        // Remove features that end their elution prior to the current scan
        //        toRemove.ForEach(x => xicFeatures.Remove(x));
        //    }

        //    OnProgress("Filtering bad features with no data.");
        //    features = features.Where(x => x.MsFeatures.Count > 0).ToList();

        //    OnProgress("Refining XIC features.");
        //    return RefineFeatureXics(features);

        //}

        public IEnumerable<UMCLight> CreateXicNew(List<UMCLight> features,
            double massError,
            InformedProteomicsReader provider)
        {
            int id = 0;
            int msmsFeatureId = 0;
            var resultFeatures = new List<UMCLight> { Capacity = features.Count };
            // Iterate over XIC targets.
            foreach (var xicTarget in CreateXicTargetsYield(features, massError))
            {
                // Read XIC
                var ipr = provider.GetReaderForGroup(0);
                var target = xicTarget.StartScan + ((xicTarget.EndScan - xicTarget.StartScan) / 2);
                var xic = ipr.GetPrecursorExtractedIonChromatogram(xicTarget.LowMz, xicTarget.HighMz, target);
                if (xic.Count < 3)
                {
                    continue;
                }
                
                var refinedXic = RefineXic(xic); // Smooth XIC and snip tails
                
                // Add xic points as MSFeatures.
                xicTarget.Feature.MsFeatures.Clear();
                foreach (var point in refinedXic)
                {
                    xicTarget.Feature.AddChildFeature(new MSFeatureLight
                    {
                        ChargeState = xicTarget.ChargeState,
                        Mz = xicTarget.Mz,
                        MassMonoisotopic = xicTarget.Feature.MassMonoisotopic,
                        Scan = point.ScanNum,
                        Abundance = Convert.ToInt64(point.Intensity),
                        Id = id++,
                        DriftTime = xicTarget.Feature.DriftTime,
                        Net = ipr.GetElutionTime(point.ScanNum),
                        GroupId = xicTarget.Feature.GroupId
                    });
                }

                // Associate MS/MS information.
                var ms2Scans = ipr.GetFragmentationSpectraScanNums(xicTarget.Feature.Mz).ToArray();
                int j = 0;
                for (int i = 0; i < xicTarget.Feature.MsFeatures.Count; i++)
                {
                    for (; j < ms2Scans.Length; j++)
                    {
                        // Scan below UMC feature scan range.
                        if (ms2Scans[j] < xicTarget.Feature.MsFeatures[i].Scan)
                        {
                            break;
                        }

                        // Haven't reached the last ms2 scan and ms2 scan is larger than next feature, could be associated with next feature
                        if (i < xicTarget.Feature.MsFeatures.Count - 1 && ms2Scans[j] > xicTarget.Feature.MsFeatures[i + 1].Scan)
                        {
                            break;
                        }

                        // We're on the last MSFeature - is the MS/MS scan actually for this feature?
                        if (i == xicTarget.Feature.MsFeatures.Count - 1 && 
                            ipr.GetPrevScanNum(ms2Scans[j], 1) != xicTarget.Feature.MsFeatures[i].Scan)
                        {
                            continue;
                        }

                        // Otherwise this is a MS/MS we want to add!
                        var spectraData = new MSSpectra
                        {
                            Id = msmsFeatureId++,
                            ScanMetaData = new ScanSummary
                            {
                                MsLevel = 2,
                                Scan = ms2Scans[j],
                                PrecursorMz = xicTarget.Feature.MsFeatures[i].Mz,
                            },
                            CollisionType = CollisionType.None,
                            Scan = ms2Scans[j],
                            PrecursorMz = xicTarget.Feature.MsFeatures[i].Mz
                        };
                        xicTarget.Feature.MsFeatures[i].MSnSpectra.Add(spectraData);
                    }
                }

                resultFeatures.Add(xicTarget.Feature);
            }

            return resultFeatures;
        }

        private IEnumerable<XicPoint> RefineXic(Xic xic)
        {
            // Here we smooth the points...and remove any features with from and trailing zero points
            var numberOfPoints = NumberOfPoints;
            var smoother = new SavitzkyGolaySmoother(numberOfPoints, CONST_POLYNOMIAL_ORDER, false);

            if (xic.Count == 0)
                return xic;

            var xyXic = new List<XYData>();
            xic.ForEach(x => xyXic.Add(new XYData(x.ScanNum, x.Intensity)));
            var points = smoother.Smooth(xyXic);

            // Find the biggest peak...
            var maxScanIndex = 0;
            double maxAbundance = 0;
            for (var i = 0; i < xic.Count; i++)
            {
                if (maxAbundance < xic[i].Intensity)
                {
                    maxScanIndex = i;
                    maxAbundance = xic[i].Intensity;
                }
            }

            // Then find when the feature goes to zero
            // Start from max to left                        
            var startIndex = maxScanIndex;

            // If we hit zero, then keep
            for (; startIndex > 0; startIndex--)
            {
                if (xic[startIndex].Intensity < 1)
                    break;
            }

            // Start from max to right
            var stopIndex = maxScanIndex;
            for (; stopIndex < xic.Count - 1; stopIndex++)
            {
                if (xic[stopIndex].Intensity < 1)
                    break;
            }

            // Add the features back
            for (var i = startIndex; i <= stopIndex; i++)
            {
                points[i].Y = Convert.ToInt64(points[i].Y);
                xic[i] = new XicPoint(Convert.ToInt32(points[i].X), xic[i].Mz, points[i].Y);
            }

            return xic;
        }

        private IEnumerable<XicFeature> CreateXicTargetsYield(IEnumerable<UMCLight> features, double massError)
        {
            int id = 0;
            foreach (var feature in features)
            {
                int minScan = Int32.MaxValue;
                int maxScan = 0;
                foreach (var msFeature in feature.MsFeatures)
                {
                    minScan = Math.Min(minScan, msFeature.Scan);
                    maxScan = Math.Max(maxScan, msFeature.Scan);
                }

                yield return new XicFeature
                {
                    HighMz = FeatureLight.ComputeDaDifferenceFromPPM(feature.Mz, -massError),
                    LowMz = FeatureLight.ComputeDaDifferenceFromPPM(feature.Mz, massError),
                    Mz = feature.Mz,
                    Feature = feature,
                    Id = id++,
                    EndScan = minScan + ScanWindowSize,
                    StartScan = maxScan - ScanWindowSize,
                    ChargeState = feature.ChargeState
                };
            }
        }

        private IEnumerable<UMCLight> RefineFeatureXics(IList<UMCLight> features)
        {
            // Here we smooth the points...and remove any features with from and trailing zero points
            var numberOfPoints = NumberOfPoints;
            var smoother = new SavitzkyGolaySmoother(numberOfPoints, CONST_POLYNOMIAL_ORDER, false);

            foreach (var feature in features)
            {
                var map = feature.CreateChargeMap();

                // Clear the MS Feature List 
                // Because we're going to refine each charge state then fix the length of the feature
                // from it's known max abundance value.                
                feature.MsFeatures.Clear();


                // Work on a single charge state since XIC's have different m/z values
                foreach (var chargeFeatures in map.Values)
                {
                    var xic = new List<XYData>();
                    var msFeatures = chargeFeatures.Where(x => x.Abundance > 0).OrderBy(x => x.Scan).ToList();
                    msFeatures.ForEach(x => xic.Add(new XYData(x.Scan, x.Abundance)));

                    var points = smoother.Smooth(xic);
                    if (msFeatures.Count <= 0)
                        continue;

                    // Find the biggest peak...
                    var maxScanIndex = 0;
                    double maxAbundance = 0;
                    for (var i = 0; i < msFeatures.Count; i++)
                    {
                        msFeatures[i].Abundance = Convert.ToInt64(points[i].Y);

                        if (maxAbundance < msFeatures[i].Abundance)
                        {
                            maxScanIndex = i;
                            maxAbundance = msFeatures[i].Abundance;
                        }
                    }

                    // Then find when the feature goes to zero
                    // Start from max to left                        
                    var startIndex = maxScanIndex;

                    // If we hit zero, then keep
                    for (; startIndex > 0; startIndex--)
                    {
                        if (msFeatures[startIndex].Abundance < 1)
                            break;
                    }

                    // Start from max to right
                    var stopIndex = maxScanIndex;
                    for (; stopIndex < msFeatures.Count - 1; stopIndex++)
                    {
                        if (msFeatures[stopIndex].Abundance < 1)
                            break;
                    }

                    // Add the features back
                    for (var i = startIndex; i <= stopIndex; i++)
                    {
                        msFeatures[i].Abundance = Convert.ToInt64(points[i].Y);
                        feature.AddChildFeature(msFeatures[i]);
                    }
                }

                // Clean up 
            }
            return features.Where(x => x.MsFeatures.Count > 0).ToList();
        }

        /// <summary>
        /// Creates XIC Targets from a list of UMC Features
        /// </summary>
        /// <param name="features"></param>
        /// <param name="massError"></param>
        /// <returns></returns>
        private List<XicFeature> CreateXicTargets(IEnumerable<UMCLight> features, double massError)
        {
            var allFeatures = new List<XicFeature>();

            // Create XIC Features
            var id = 0;
            // Then for each feature turn it into a new feature
            foreach (var feature in features)
            {
                // Build XIC features from each
                var x = feature.CreateChargeMap();
                foreach (var charge in x.Keys)
                {
                    double maxIntensity = 0;
                    double mz = 0;
                    var min = double.MaxValue;
                    var max = double.MinValue;

                    var scanStart = int.MaxValue;
                    var scanEnd = 0;

                    foreach (var chargeFeature in x[charge])
                    {
                        min = Math.Min(min, chargeFeature.Mz);
                        max = Math.Max(max, chargeFeature.Mz);
                        scanStart = Math.Min(scanStart, chargeFeature.Scan);
                        scanEnd = Math.Min(scanStart, chargeFeature.Scan);

                        if (chargeFeature.Abundance > maxIntensity)
                        {
                            maxIntensity = chargeFeature.Abundance;
                            mz = chargeFeature.Mz;
                        }
                    }

                    // Clear the ms feature list...because later we will populate it
                    feature.MsFeatures.Clear();

                    var xicFeature = new XicFeature
                    {
                        HighMz = FeatureLight.ComputeDaDifferenceFromPPM(mz, -massError),
                        LowMz = FeatureLight.ComputeDaDifferenceFromPPM(mz, massError),
                        Mz = mz,
                        Feature = feature,
                        Id = id++,
                        EndScan = scanEnd + ScanWindowSize,
                        StartScan = scanStart - ScanWindowSize,
                        ChargeState = charge
                    };

                    allFeatures.Add(xicFeature);
                }
            }

            return allFeatures;
        }

        /// <summary>
        /// Creates an XIC from the given set of target features.
        /// </summary>
        /// <param name="massError">Mass error to use when pulling peaks</param>
        /// <param name="msFeatures">Seed features that provide the targets</param>
        /// <param name="provider"></param>
        /// <returns></returns>
        public IEnumerable<MSFeatureLight> CreateXic(IList<MSFeatureLight> msFeatures,
                                                        double massError,
                                                        InformedProteomicsReader provider)
        {
            var newFeatures = new List<MSFeatureLight>();

            if (msFeatures.Count <= 0) return newFeatures;

            var minScan = msFeatures[0].Scan;
            var maxScan = msFeatures[msFeatures.Count - 1].Scan;
            minScan -= 100;
            maxScan += 100;
            minScan = Math.Max(0, minScan);

            var min = double.MaxValue;
            var max = double.MinValue;
            double maxIntensity = 0;
            var featureMap = new Dictionary<int, MSFeatureLight>();
            double mz = 0;
            foreach (var chargeFeature in msFeatures)
            {
                min = Math.Min(min, chargeFeature.Mz);
                max = Math.Max(max, chargeFeature.Mz);

                if (chargeFeature.Abundance > maxIntensity)
                {
                    maxIntensity = chargeFeature.Abundance;
                    mz = chargeFeature.Mz;
                }

                // Map the feature...
                if (!featureMap.ContainsKey(chargeFeature.Scan))
                {
                    featureMap.Add(chargeFeature.Scan, chargeFeature);
                }
            }

            var features = CreateXic(mz, massError, minScan, maxScan, provider);
            foreach (var msFeature in features)
            {
                var scan = msFeature.Scan;
                if (featureMap.ContainsKey(msFeature.Scan))
                    featureMap[scan].Abundance = msFeature.Abundance;
                newFeatures.Add(msFeature);
            }
            return newFeatures;
        }
        /// <summary>
        /// Creates an XIC from the m/z values provided.
        /// </summary>
        /// <param name="mz"></param>
        /// <param name="massError"></param>
        /// <param name="minScan"></param>
        /// <param name="maxScan"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        public IEnumerable<MSFeatureLight> CreateXic(double mz,
                                                     double massError,
                                                     int minScan,
                                                     int maxScan,
                                                     InformedProteomicsReader provider)
        {

            var newFeatures = new List<MSFeatureLight>();
            var lower = FeatureLight.ComputeDaDifferenceFromPPM(mz, massError);
            var higher = FeatureLight.ComputeDaDifferenceFromPPM(mz, -massError);

            var ipr = provider.GetReaderForGroup(0);
            var xic = ipr.GetPrecursorExtractedIonChromatogram(lower, higher);// InformedProteomics gives us base peak intensity per scan for the xic

            var scans = xic.Select(x => x.ScanNum).Distinct().Where(x => minScan <= x && x <= maxScan);

            //foreach (var scan in scans)
            //{
            //    var newFeature = new MSFeatureLight
            //    {
            //        Scan = scan,
            //        Net = ipr.GetElutionTime(scan),
            //        Abundance = xic.Where(x => x.ScanNum == scan).Sum(x => x.Intensity),
            //    };
            //    newFeatures.Add(newFeature);
            //}

            newFeatures.AddRange(scans.Select(scan => new MSFeatureLight
            {
                Scan = scan,
                Net = ipr.GetElutionTime(scan),
                Abundance = xic.Where(x => x.ScanNum == scan).Sum(x => x.Intensity),
            
            }));

            //for (var i = minScan; i < maxScan; i++)
            //{
            //    List<XYData> spectrum = null;
            //
            //    try
            //    {
            //        var summary = new ScanSummary();
            //        spectrum = provider.GetRawSpectra(i, 0, 1, out summary);
            //    }
            //    catch
            //    {
            //
            //    }
            //
            //    if (spectrum == null)
            //        continue;
            //
            //    var data = (from x in spectrum
            //                where x.X > lower && x.X < higher
            //                select x).ToList();
            //
            //    var summedIntensity = data.Sum(x => x.Y);
            //
            //
            //    var newFeature = new MSFeatureLight
            //    {
            //        Scan = i,
            //        Net = i,
            //        Abundance = Convert.ToInt64(summedIntensity)
            //    };
            //    newFeatures.Add(newFeature);
            //}
            return newFeatures;
        }

        public IDictionary<int, IList<MSFeatureLight>> CreateXic(UMCLight feature, double massError, InformedProteomicsReader provider)
        {
            var features = new Dictionary<int, IList<MSFeatureLight>>();
            var chargeFeatures = feature.CreateChargeMap();

            // For each UMC...
            foreach (var charge in chargeFeatures.Keys)
            {
                // Find the mininmum and maximum features                             
                var msFeatures = CreateXic(chargeFeatures[charge],
                                            massError,
                                            provider);

                features.Add(charge, new List<MSFeatureLight>());

                foreach (var newFeature in msFeatures)
                {
                    // Here we ask if this is a new MS Feature or old...
                    if (!chargeFeatures.ContainsKey(newFeature.Scan))
                    {
                        // Otherwise add the new feature
                        newFeature.MassMonoisotopic = feature.MassMonoisotopic;
                        newFeature.DriftTime = feature.DriftTime;
                        newFeature.GroupId = feature.GroupId;
                    }
                    features[charge].Add(newFeature);
                }
            }
            return features;
        }
        /// <summary>
        /// Gets or sets how many scans to add before and after an initial XIC target
        /// </summary>
        public int ScanWindowSize { get; set; }
        /// <summary>
        /// Gets or sets the size of the m/z window to use when linking MS Features to MS/MS spectra
        /// </summary>
        public double FragmentationSizeWindow { get; set; }


        public int NumberOfPoints { get; set; }
    }

}
