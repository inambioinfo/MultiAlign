using System.Collections.Generic;
using MultiAlignEngine.Features;
using NHibernate.Criterion;
using PNNLOmics.Data.Features;
using System.Data.SQLite;
using System;

namespace MultiAlignCore.IO.Features.Hibernate
{

    public class UmcDAOHibernate : GenericDAOHibernate<UMCLight>, IUmcDAO
    {
		
        /// <summary>
        /// Searches for and returns a List of Umc Objects in the Database that have the exact Mass given.
        /// </summary>
        /// <param name="mass">Mass value to be searched for</param>
        /// <returns>List of Umc Objects</returns>
        public List<UMCLight> FindByMass(double mass)
        {
            ICriterion criterion = Expression.Eq("Mass", mass);
            List<ICriterion> criterionList = new List<ICriterion>();
            criterionList.Add(criterion);
            
            return FindByCriteria(criterionList);
        }

        /// <summary>
        /// Searches for and returns a List of Umc Objects in the Database that have a Mass inside the given range.
        /// If the Lower Mass Value given is greater than the Upper Mass Value given, they are switched.
        /// </summary>
        /// <param name="mass1">Lower mass value</param>
        /// <param name="mass2">Upper mass value</param>
        /// <returns>List of Umc Objects</returns>
        public List<UMCLight> FindByMassRange(double mass1, double mass2)
        {
            ICriterion criterion;

            if (mass1 <= mass2)
            {
                criterion = Expression.Between("Mass", mass1, mass2);
            } else
            {
                criterion = Expression.Between("Mass", mass2, mass1);
            }

            List<ICriterion> criterionList = new List<ICriterion>();
            criterionList.Add(criterion);

            return FindByCriteria(criterionList);
        }
        /// <summary>
        /// Finds a feature based on a feature id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public UMCLight FindByFeatureID(int id)
        {
            ICriterion criterion = Expression.Eq("Id", id);
            List<ICriterion> criterionList = new List<ICriterion>();
            criterionList.Add(criterion);
            List<UMCLight> umcs = FindByCriteria(criterionList);
            if (umcs.Count < 1)
                return null;

            return umcs[0];
        }
        /// <summary>
        /// Finds a feature based on a charge state.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public List<UMCLight> FindByCharge(int charge)
        {
            ICriterion criterion = Expression.Eq("ChargeState", charge);
            List<ICriterion> criterionList = new List<ICriterion>();
            criterionList.Add(criterion);
            return FindByCriteria(criterionList);
        }
        /// <summary>
        /// Finds a feature based on a charge state.
        /// </summary>
        /// <param name="charge">Charge state interested in</param>
        /// <param name="dataset">Dataset interested in</param>
        /// <returns></returns>
        public List<UMCLight> FindByChargeDataset(int charge, int dataset)
        {
            ICriterion criterion = Expression.Eq("ChargeState", charge);
            List<ICriterion> criterionList = new List<ICriterion>();
            criterionList.Add(criterion);
            return FindByCriteria(criterionList);
        }
        /// <summary>
        /// Finds a feature based on a feature id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public List<UMCLight> FindByFeatureID(List<int> id)
        {
            ICriterion criterion = Expression.In("Id", id);
            List<ICriterion> criterionList = new List<ICriterion>();
            criterionList.Add(criterion);
            List<UMCLight> umcs = FindByCriteria(criterionList);
            if (umcs.Count < 1)
                return null;

            return umcs;
        }
        public int FindMaxCharge()
        {
            object data = GetSession().CreateCriteria(typeof(UMCLight))
                            .SetProjection(Projections.Max("ChargeState"))
                                .UniqueResult();
            return Convert.ToInt32(data);
        }
        /// <summary>
        /// Finds a feature based on a cluster id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public List<UMCLight> FindByClusterID(int id)
        {
            ICriterion criterion = Expression.Eq("ClusterID", id);
            List<ICriterion> criterionList = new List<ICriterion>();
            criterionList.Add(criterion);
            List<UMCLight> umcs = FindByCriteria(criterionList);
            if (umcs.Count < 1)
                return null;

            return umcs;
        }
		/// <summary>
		/// Finds a feature based on a List of cluster IDs.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
        public List<UMCLight> FindByClusterID(List<int> idList)
		{
			ICriterion criterion = Expression.In("ClusterID", idList);
			List<ICriterion> criterionList = new List<ICriterion>();
			criterionList.Add(criterion);
            List<UMCLight> umcs = FindByCriteria(criterionList);
			if (umcs.Count < 1)
			{
				return null;
			}

			return umcs;
		}
		/// <summary>
		/// Searches for and returns a List of Umc Objects in the Database that have the exact Dataset Id given.
		/// </summary>
		/// <param name="mass">Dataset value to be searched for</param>
		/// <returns>List of Umc Objects</returns>
        public List<UMCLight> FindByDatasetId(int datasetId)
		{
			ICriterion criterion = Expression.Eq("GroupID", datasetId);
			List<ICriterion> criterionList = new List<ICriterion>();
			criterionList.Add(criterion);
			return FindByCriteria(criterionList);
		}

        /// <summary>
        /// Finds all features that are clustered.
        /// </summary>
        /// <returns></returns>

        /// <summary>
        /// Searches for and returns a List of Umc Objects in the Database that have the exact Dataset Id given.
        /// </summary>
        /// <param name="mass">Dataset value to be searched for</param>
        /// <returns>List of Umc Objects</returns>
        public List<UMCLight> FindAllClustered()
        {
            ICriterion criterion = Expression.Gt("ClusterID", -1);
            List<ICriterion> criterionList = new List<ICriterion>();
            criterionList.Add(criterion);
            return FindByCriteria(criterionList);
        }        
        public void ClearAlignmentData()
        {
            using (SQLiteConnection connection = new SQLiteConnection("Data Source=" + NHibernateUtil.Path))
            {
                connection.Open();
                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = "UPDATE T_LCMS_Features  SET Mass_Calibrated = -1, NET = -1, Scan_Aligned = -1";
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
        }

        public IEnumerable<int> RetrieveChargeStates()
        {
            var charges = new List<int>();
            using (var connection = new SQLiteConnection("Data Source=" + NHibernateUtil.Path))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = "SELECT Charge FROM T_LCMS_Features";
                    var reader = command.ExecuteReader();

                    if (reader != null)
                    {
                        while (reader.Read())
                        {
                            var charge = reader.GetInt32(0);
                            charges.Add(charge);
                        }
                    }
                }
                connection.Close();
            }
            return charges;
        }
    }
}