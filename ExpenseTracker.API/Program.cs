using ExpenseTracker.API.AppStart;
using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Allow Angular frontend to call this API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:7150", "http://localhost:5150")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.RegisterDataContext(builder.Configuration);
builder.Services.RegisterRepositories();
builder.Services.RegisterServices();

var defaultMcpServerPath = Path.GetFullPath(
    Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "ExpenseTracker.McpServer",
        "bin",
        "Debug",
        "net10.0",
        "ExpenseTracker.McpServer.dll"));

var mcpServerPath =
    builder.Configuration["Mcp:ServerDllPath"]
    ?? defaultMcpServerPath;

builder.Services.AddSingleton<IMcpTestClient>(
    new McpTestClient(mcpServerPath));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Tracker API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowAngular");

app.UseAuthorization();

app.MapControllers();

app.Run();
