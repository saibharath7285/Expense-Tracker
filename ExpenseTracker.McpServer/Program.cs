using ExpenseTracker.API.AppStart;
using ExpenseTracker.McpServer.Tools;
using Microsoft.AspNetCore.Builder;
using ModelContextProtocol.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

if (string.IsNullOrEmpty(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://localhost:5005");
}

// 1. Wire database, repositories, and services (reusing our ExpenseTracker.API AppStart methods)
builder.Services.RegisterDataContext(builder.Configuration);
builder.Services.RegisterRepositories();
builder.Services.RegisterServices();

// 2. Register MCP Server with stateless Streamable HTTP transport and tools
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<ExpenseTrackerTools>();

var app = builder.Build();

app.MapMcp("/mcp");

await app.RunAsync();