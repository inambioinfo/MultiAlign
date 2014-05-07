﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PNNLOmics.Data.Features;
using System.Data.SQLite;

namespace MultiAlignCore.IO.Features
{
    /// <summary>
    /// Class that extracts features from a sqlite database using ADO
    /// </summary>
    public class UmcAdoDAO: IUmcDAO 
    {
        public string DatabasePath { get; set; }

        #region IUmcDAO Members

        public List<UMCLight> FindByMass(double mass)
        {
            throw new NotImplementedException();
        }

        public List<UMCLight> FindByMassRange(double mass1, double mass2)
        {
            throw new NotImplementedException();            
        }

        public UMCLight FindByFeatureID(int id)
        {
            throw new NotImplementedException();   
        }

        public List<UMCLight> FindByClusterID(List<int> idList)
        {
            throw new NotImplementedException();
        }

        public List<UMCLight> FindByClusterID(int id)
        {
            throw new NotImplementedException();
        }

        public List<PNNLOmics.Data.Features.UMCLight> FindByDatasetId(int datasetId)
        {
            throw new NotImplementedException();
        }

        public List<UMCLight> FindByCharge(int charge)
        {
            List<UMCLight> features = new List<UMCLight>();


            int featurecount = 0;
            int cuont = 0;
            using (SQLiteConnection connection = new SQLiteConnection(string.Format("Data Source = {0}", DatabasePath))) 
            {
                connection.Open();
                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = string.Format("SELECT * FROM T_LCMS_FEATURES WHERE CHARGE = {0}", charge);
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            object[] values = new object[20];
                            reader.GetValues(values);

                            UMCLight feature    = new UMCLight();
                            feature.ChargeState = Convert.ToInt32(values[11]);
                            feature.MassMonoisotopicAligned = Convert.ToDouble(values[5]);
                            feature.RetentionTime           = Convert.ToDouble(values[6]);
                            feature.DriftTime               = Convert.ToDouble(values[15]);
                            feature.AbundanceSum            = Convert.ToInt64(values[14]);
                            feature.Abundance               = Convert.ToInt64(values[13]);
                            feature.GroupID                 = Convert.ToInt32(values[1]);
                            feature.ID                      = Convert.ToInt32(values[0]);
                            features.Add(feature);

                            featurecount++;
                            if (featurecount > 100000)
                            {
                                cuont ++;
                                Logger.PrintMessage(string.Format("\tLoaded {0}00000 features", cuont));
                                featurecount = 0;
                            }
                        }
                    }
                }
                connection.Close();
            }

            return features;
        }

        public List<PNNLOmics.Data.Features.UMCLight> FindByChargeDataset(int charge, int dataset)
        {
            throw new NotImplementedException();
        }

        public List<PNNLOmics.Data.Features.UMCLight> FindAllClustered()
        {
            throw new NotImplementedException();
        }

        public int FindMaxCharge()
        {
            throw new NotImplementedException();
        }

        public void ClearAlignmentData()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IGenericDAO<UMCLight> Members

        public void Add(PNNLOmics.Data.Features.UMCLight t)
        {
            throw new NotImplementedException();
        }

        public void AddAll(ICollection<PNNLOmics.Data.Features.UMCLight> tList)
        {
            throw new NotImplementedException();
        }

        public void Update(PNNLOmics.Data.Features.UMCLight t)
        {
            throw new NotImplementedException();
        }

        public void UpdateAll(ICollection<PNNLOmics.Data.Features.UMCLight> tList)
        {
            throw new NotImplementedException();
        }

        public void Delete(PNNLOmics.Data.Features.UMCLight t)
        {
            throw new NotImplementedException();
        }

        public void DeleteAll(ICollection<PNNLOmics.Data.Features.UMCLight> tList)
        {
            throw new NotImplementedException();
        }

        public PNNLOmics.Data.Features.UMCLight FindById(int id)
        {
            throw new NotImplementedException();
        }

        public List<PNNLOmics.Data.Features.UMCLight> FindAll()
        {
            throw new NotImplementedException();
        }

        #endregion


        public IEnumerable<int> RetrieveChargeStates()
        {
            return new List<int>();
        }
    }
}