using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.API.Data
{
    public class Repository<TEntity> : IRepositoryAsync<TEntity> where TEntity : class
    {
        private readonly DatabaseContext _context;

        public Repository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<TEntity>> ExecuteStoredProcedureAsync(string spName, StoredProcedureParams? spParams = null)
        {
            if (spParams == null)
            {
                return await _context.Set<TEntity>().FromSqlRaw($"EXEC {spName}").ToListAsync();
            }

            var sqlParams = spParams.AsSqlParameters();
            var paramNames = sqlParams.Select(p => p.ParameterName).ToArray();
            var query = $"EXEC {spName} {string.Join(", ", paramNames)}";

            return await _context.Set<TEntity>().FromSqlRaw(query, sqlParams).ToListAsync();
        }
    }
}