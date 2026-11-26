using ExpenseTracker.API.AppStart;
using ExpenseTracker.McpServer.AppStart;
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

// 1. Wire database, repositories, services, and RAG knowledge services
builder.Services.RegisterDataContext(builder.Configuration);
builder.Services.RegisterRepositories();
builder.Services.RegisterServices();
builder.Services.RegisterKnowledgeServices(builder.Configuration);

// 2. Register MCP Server with stateless Streamable HTTP transport and tools
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<ExpenseTrackerTools>()
    .WithTools<KnowledgeTools>();

var app = builder.Build();

app.MapMcp("/mcp");

await app.RunAsync();