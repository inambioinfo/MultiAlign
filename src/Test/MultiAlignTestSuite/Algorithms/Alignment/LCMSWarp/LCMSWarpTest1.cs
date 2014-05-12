﻿#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MultiAlignCore.Algorithms.Alignment;
using MultiAlignCore.Algorithms.FeatureMatcher;
using MultiAlignEngine.Alignment;
using MultiAlignEngine.Features;
using NUnit.Framework;
using PNNLOmics.Algorithms.Alignment.LcmsWarp;
using PNNLOmics.Data.Features;
using PNNLOmics.Data.MassTags;

#endregion

namespace MultiAlignTestSuite.Algorithms.Alignment.LCMSWarp
{
    [TestFixture]
    public class LcmsWarpTest1
    {
        [Test(
            Description = "This tests the new LCMSWarp port between two database search results converted to UmcLights")
        ]
        [TestCase(
            @"C:\UnitTestFolder\dataset1.txt",
            @"C:\UnitTestFolder\dataset2.txt"
            )]
        public void TestLcmsWarpPort(string baselinePath, string aligneePath)
        {
            Console.WriteLine(@"I'm Testing!");

            var aligner = new LcmsWarpAdapter();

            var baseline = new List<UMCLight>();

            var rawBaselineData = File.ReadAllLines(baselinePath);
            var rawFeaturesData = File.ReadAllLines(aligneePath);

            foreach (var line in rawBaselineData)
            {
                if (line != "")
                {
                    var parsed = line.Split(',');
                    var data = new UMCLight
                    {
                        Net = Convert.ToDouble(parsed[0]),
                        ChargeState = Convert.ToInt32(parsed[1]),
                        Mz = Convert.ToDouble(parsed[2]),
                        Scan = Convert.ToInt32(parsed[3]),
                        MassMonoisotopic = Convert.ToDouble(parsed[4]),
                        MassMonoisotopicAligned = Convert.ToDouble(parsed[5]),
                        Id = Convert.ToInt32(parsed[6])
                    };
                    baseline.Add(data);
                }
            }

            var features = (from line in rawFeaturesData
                where line != ""
                select line.Split(',')
                into parsed
                select new UMCLight
                {
                    Net = Convert.ToDouble(parsed[0]),
                    ChargeState = Convert.ToInt32(parsed[1]),
                    Mz = Convert.ToDouble(parsed[2]),
                    Scan = Convert.ToInt32(parsed[3]),
                    MassMonoisotopic = Convert.ToDouble(parsed[4]),
                    MassMonoisotopicAligned = Convert.ToDouble(parsed[5]),
                    Id = Convert.ToInt32(parsed[6])
                }).ToList();


            var outputData = aligner.Align(baseline, features);

            Console.WriteLine(@"Done testing");
        }

        [Test(Description = "Tests the old C++ version, to make sure it sets the reference features correctly.")]
        [TestCase(
            @"C:\UnitTestFolder\dataset1.txt"
            )]
        public void TestCppSetReferenceFeatures(string baselinePath)
        {
            Console.WriteLine(@"I'm Testing!");

            var rawBaselineData = File.ReadAllLines(baselinePath);

            var baseline = new List<UMCLight>();

            foreach (var line in rawBaselineData)
            {
                if (line != "")
                {
                    var parsed = line.Split(',');
                    var data = new UMCLight
                    {
                        Net = Convert.ToDouble(parsed[0]),
                        ChargeState = Convert.ToInt32(parsed[1]),
                        Mz = Convert.ToDouble(parsed[2]),
                        Scan = Convert.ToInt32(parsed[3]),
                        MassMonoisotopic = Convert.ToDouble(parsed[4]),
                        MassMonoisotopicAligned = Convert.ToDouble(parsed[5]),
                        Id = Convert.ToInt32(parsed[6])
                    };
                    baseline.Add(data);
                }
            }

            var oldStyle = new clsAlignmentProcessor();
            var oldBaseline = baseline.Select(baseData => new clsUMC
            {
                Net = baseData.Net,
                MZForCharge = baseData.Mz,
                Scan = baseData.Scan,
                Mass = baseData.MassMonoisotopic,
                MassCalibrated = baseData.MassMonoisotopicAligned,
                Id = baseData.Id
            }).ToList();

            oldStyle.SetReferenceDatasetFeatures(oldBaseline);
            Console.WriteLine(@"Done testing");
        }

        [Test(Description = "Tests the old C++ version, to make sure it sets the alignee features correctly.")]
        [TestCase(
            @"C:\UnitTestFolder\dataset2.txt"
            )]
        public void TestSetAligneeFeatures(string aligneePath)
        {
            Console.WriteLine(@"I'm Testing!");
            var rawFeaturesData = File.ReadAllLines(aligneePath);

            var features = (from line in rawFeaturesData
                where line != ""
                select line.Split(',')
                into parsed
                select new UMCLight
                {
                    Net = Convert.ToDouble(parsed[0]),
                    ChargeState = Convert.ToInt32(parsed[1]),
                    Mz = Convert.ToDouble(parsed[2]),
                    Scan = Convert.ToInt32(parsed[3]),
                    MassMonoisotopic = Convert.ToDouble(parsed[4]),
                    MassMonoisotopicAligned = Convert.ToDouble(parsed[5]),
                    Id = Convert.ToInt32(parsed[6])
                }).ToList();

            var oldStyle = new clsAlignmentProcessor();
            var oldFeatures = features.Select(baseData => new clsUMC
            {
                Net = baseData.Net,
                MZForCharge = baseData.Mz,
                Scan = baseData.Scan,
                Mass = baseData.MassMonoisotopic,
                MassCalibrated = baseData.MassMonoisotopicAligned,
                Id = baseData.Id
            }).ToList();

            var oldMzBound = new classAlignmentMZBoundary(0, 9999999.0);
            oldStyle.SetAligneeDatasetFeatures(oldFeatures, oldMzBound);

            Console.WriteLine(@"Done testing");
        }

        [Test(Description = "Tests the old C++ version, to make sure it sets the reference features correctly.")]
        [TestCase(
            @"C:\UnitTestFolder\dataset1.txt",
            @"C:\UnitTestFolder\dataset2.txt"
            )]
        public void TestPerformNetAlignment(string baselinePath, string aligneePath)
        {
            Console.WriteLine(@"I'm Testing!");
            var baseline = new List<UMCLight>();

            var rawBaselineData = File.ReadAllLines(baselinePath);
            var rawFeaturesData = File.ReadAllLines(aligneePath);

            foreach (var line in rawBaselineData)
            {
                if (line != "")
                {
                    var parsed = line.Split(',');
                    var data = new UMCLight
                    {
                        Net = Convert.ToDouble(parsed[0]),
                        RetentionTime = Convert.ToDouble(parsed[0]),
                        ChargeState = Convert.ToInt32(parsed[1]),
                        Mz = Convert.ToDouble(parsed[2]),
                        ScanStart = Convert.ToInt32(parsed[3]),
                        Scan = Convert.ToInt32(parsed[3]),
                        MassMonoisotopic = Convert.ToDouble(parsed[4]),
                        MassMonoisotopicAligned = Convert.ToDouble(parsed[5]),
                        Id = Convert.ToInt32(parsed[6])
                    };
                    baseline.Add(data);
                }
            }

            var features = (from line in rawFeaturesData
                where line != ""
                select line.Split(',')
                into parsed
                select new UMCLight
                {
                    Net = Convert.ToDouble(parsed[0]),
                    RetentionTime = Convert.ToDouble(parsed[0]),
                    ChargeState = Convert.ToInt32(parsed[1]),
                    Mz = Convert.ToDouble(parsed[2]),
                    ScanStart = Convert.ToInt32(parsed[3]),
                    Scan = Convert.ToInt32(parsed[3]),
                    MassMonoisotopic = Convert.ToDouble(parsed[4]),
                    MassMonoisotopicAligned = Convert.ToDouble(parsed[5]),
                    Id = Convert.ToInt32(parsed[6])
                }).ToList();

            var oldStyle = new LcmsWarpFeatureAligner();

            var oldOutputData = oldStyle.Align(baseline, features);

            Console.WriteLine(@"Done testing");
        }


        [Test(Description = "Testing the grid app being developed (will be moved to appropriate folder)")]
        [TestCase(@"C:\UnitTestFolder\csvFolder\mz.csv")]
        public void TestLamarcheGridApp(string csvPath)
        {
            //Read a csv file, put the data into a new UMCLight for each one
            var csvFileText = File.ReadAllLines(csvPath);

            var csvDataList = new List<UMCLight> {Capacity = csvFileText.Length};

            foreach (var line in csvFileText)
            {
                var parsedLine = line.Split(',');

                var umcDataMember = new UMCLight();
                //put the data from the parsed line into the umcDataMember in the appropriate fashion

                csvDataList.Add(umcDataMember);
            }

            //Create clusters from the data read in from the file

            UMCClusterLight cluster = null;

            var filteredClusters = new List<UMCClusterLight>();

            if (!Filter(cluster))
            {
                //Save the cluster
                filteredClusters.Add(cluster);
            }


            //Read a mtdb file using MACore or sqliteDb
            var databasePath = "C:\\UnitTestFolder\\MSGFPlus\\blah.db";
                //Either read from console, or entered at program execution
            // Console.ReadLine(databasePath) or databasePath = args[2]
            var database = ReadDatabase(databasePath);

            var stacAdapter = new STACAdapter<UMCClusterLight>();

            var matchList = stacAdapter.PerformPeakMatching(filteredClusters, database);
            string writePath = null;
            // As with databasePath, could either be read from console, or entered at program execution
            // Console.ReadLine(writePath) or, if(args.Length >= 4){ writePath = args[3]; }
            // If writePath isn't entered, then it writes to a default file, defined inside the WriteData method
            WriteData(matchList, writePath);
        }

        private static bool Filter(UMCClusterLight cluster) // might be different cluster object
        {
            //Perform filtering as necesary, returns true if the cluster does not pass the filter
            return true;
        }

        private static MassTagDatabase ReadDatabase(string databasePath)
        {
            MassTagDatabase database = null;

            //Perform the reading from the binary file, saving data into database
            var fileBytes = File.ReadAllBytes(databasePath);

            var sb = new StringBuilder();

            foreach (var b in fileBytes)
            {
                sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            }
            //Now it's a string of data
            var data = sb.ToString();

            return database;
        }

        private static void WriteData(List<FeatureMatchLight<UMCClusterLight, MassTagLight>> matchList,
            string writePath)
        {
            if (writePath == null)
            {
                writePath = "C:\\DataGoesHere.csv";
            }

            // Open the file, or create it if it didn't exist, for write access
            using (var writeFile = new StreamWriter(writePath))
            {
                foreach (var match in matchList)
                {
                    //Write the data into the file!
                    writeFile.Write(match.Observed.DriftTime);
                    writeFile.Write(",");
                    writeFile.Write(match.Observed.MassMonoisotopicAligned);
                    writeFile.Write(",");
                    writeFile.Write(match.Observed.NetAligned);
                    writeFile.Write(",");
                    writeFile.Write(match.Observed.DatasetMemberCount);
                    writeFile.Write(",");
                    writeFile.Write(match.Observed.Features.Count);
                    writeFile.Write('\n');
                }
            }
        }
    }
}