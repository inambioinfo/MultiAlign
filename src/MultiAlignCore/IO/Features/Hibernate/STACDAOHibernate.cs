using MultiAlignCore.Data.Alignment;
using System.Data.SQLite;
using MultiAlignCore.IO.Features.Hibernate;
using MultiAlignCore.Data.MassTags;
using PNNLOmics.Algorithms.FeatureMatcher.Data;

namespace MultiAlignCore.IO.Features.Hibernate
{

    public class STACDAOHibernate : GenericDAOHibernate<STACFDR>
    {
        public void ClearAll()
        {
            string path = NHibernateUtil.Connection;
            using (SQLiteConnection connection = new SQLiteConnection("Data Source = " + path + " ;"))
            {
                connection.Open();
                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM T_STAC_FDR";
                    command.ExecuteNonQuery();
                }
            }
        }
    }

}