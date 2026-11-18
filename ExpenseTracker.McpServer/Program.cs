using ExpenseTracker.API.AppStart;
using ExpenseTracker.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// 1. Wire database, repositories, and services (reusing our ExpenseTracker.API AppStart methods)
builder.Services.RegisterDataContext(builder.Configuration);
builder.Services.RegisterRepositories();
builder.Services.RegisterServices();

// 2. Register MCP Server with stdio transport and tools
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ExpenseTrackerTools>();

var host = builder.Build();
await host.RunAsync();