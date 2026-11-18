using ExpenseTracker.API.Entities;

namespace ExpenseTracker.API.Contracts
{
    public interface IRepositoryAsync<TEntity> where TEntity : class
    {
        Task<List<TEntity>> ExecuteStoredProcedureAsync(string spName, StoredProcedureParams? spParams = null);
    }
}
