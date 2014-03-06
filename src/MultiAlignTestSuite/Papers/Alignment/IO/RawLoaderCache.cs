﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PNNLOmics.Data;
using MultiAlignCore.IO.Features;

namespace MultiAlignTestSuite.Papers.Alignment.IO
{
    public class RawLoaderCache: ISpectraProvider   
    {
        Dictionary<int, Dictionary<int, ScanSummary>> m_summaryMap;        
        ISpectraProvider m_provider;


        public RawLoaderCache(ISpectraProvider provider)
        {
            m_summaryMap = new Dictionary<int, Dictionary<int, ScanSummary>>();
            m_provider = provider;
        }

        public  Dictionary<int, ScanSummary> GetScanData(int group)
        {
            if (!m_summaryMap.ContainsKey(group))
            {
                m_summaryMap[group] = m_provider.GetScanData(group);
            }

            return m_summaryMap[group];
        }
    
        #region ISpectraProvider Members
        
        public void AddDataFile(string path, int groupID)
        {
            m_provider.AddDataFile(path, groupID);
        }

        public void AddCache(int groupID, Dictionary<int, ScanSummary> cache)
        {
            if (m_summaryMap.ContainsKey(groupID))
                throw new Exception("The scan summary already exists for this group.");

            m_summaryMap.Add(groupID, cache);            
        }

        public List<MSSpectra>  GetMSMSSpectra(int group, Dictionary<int,int> excludeMap, bool loadPeaks)
        {
            return m_provider.GetMSMSSpectra(group, excludeMap, loadPeaks);
        }

        public List<MSSpectra>  GetMSMSSpectra(int group, Dictionary<int,int> excludeMap)
        {
            return m_provider.GetMSMSSpectra(group, excludeMap);
        }

        public List<MSSpectra>  GetMSMSSpectra(int group)
        {
            return m_provider.GetMSMSSpectra(group);
        }


        public List<MSSpectra>  GetRawSpectra(int group)
        {
            return m_provider.GetRawSpectra(group);
        }


        #endregion

        #region IDisposable Members

        public void  Dispose()
        {
            m_provider.Dispose();
        }

        #endregion


        public List<XYData> GetRawSpectra(int scan, int group, int scanLevel, out ScanSummary summary)
        {
            return m_provider.GetRawSpectra(scan, group, scanLevel, out summary);
        }


        public List<XYData> GetRawSpectra(int scan, int group, out ScanSummary summary)
        {
            return m_provider.GetRawSpectra(scan, group, -1, out summary);
        }
    }
}