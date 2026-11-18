using ExpenseTracker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.API.AppStart
{
    public static class DataContextRegistration
    {
        public static IServiceCollection RegisterDataContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<DatabaseContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("AdventureWorksConnection"));
            });
            return services;
        }
    }
}
