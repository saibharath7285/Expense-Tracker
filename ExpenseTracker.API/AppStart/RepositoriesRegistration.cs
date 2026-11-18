using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Data;
using ExpenseTracker.API.Entities;

namespace ExpenseTracker.API.AppStart
{
    public static class RepositoriesRegistration
    {
        public static IServiceCollection RegisterRepositories(this IServiceCollection services)
        {
            services.AddScoped<IRepositoryAsync<Expense_Result>, Repository<Expense_Result>>();
            services.AddScoped<IRepositoryAsync<GetCategories_Result>, Repository<GetCategories_Result>>();
            services.AddScoped<IRepositoryAsync<GetSpendingSummary_Result>, Repository<GetSpendingSummary_Result>>();
            services.AddScoped<IRepositoryAsync<AddExpense_Result>, Repository<AddExpense_Result>>();
            services.AddScoped<IRepositoryAsync<NoOutputResult>, Repository<NoOutputResult>>();

            return services;
        }
    }
}
