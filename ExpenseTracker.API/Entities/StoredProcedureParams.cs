using Microsoft.Data.SqlClient;

namespace ExpenseTracker.API.Entities
{
    public class StoredProcedureParams
    {
        public SqlParameter[] AsSqlParameters()
        {
            var sqlParameters = new List<SqlParameter>();
            foreach (var p in GetType().GetProperties())
            {
                var value = p.GetValue(this);
                sqlParameters.Add(new SqlParameter("@" + p.Name, value ?? DBNull.Value));
            }
            return sqlParameters.ToArray();
        }
    }
}
