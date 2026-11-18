using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Services;

namespace ExpenseTracker.API.AppStart
{
    public static class ServiceRegistration
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            services.AddScoped<IExpenseTrackerService, ExpenseTrackerService>();
            services.AddHttpClient<ICopilotService, GeminiCopilotService>();
            return services;
        }
    }
}
