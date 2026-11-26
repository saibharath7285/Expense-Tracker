using ExpenseTracker.McpServer.Entities;
using ExpenseTracker.McpServer.Knowledge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.McpServer.AppStart;

public static class KnowledgeRegistration
{
    public static IServiceCollection RegisterKnowledgeServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<KnowledgeOptions>(configuration.GetSection(KnowledgeOptions.SectionName));

        var connectionString = configuration.GetConnectionString("AdventureWorksConnection")
            ?? throw new InvalidOperationException("Connection string 'AdventureWorksConnection' is missing.");

        services.AddHttpClient<IEmbeddingGenerator, OllamaEmbeddingGenerator>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
            if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpointUri) ||
                (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException($"Invalid Ollama endpoint: '{options.Endpoint}'.");
            }

            httpClient.BaseAddress = new Uri(endpointUri, "/");
            httpClient.Timeout = TimeSpan.FromSeconds(options.EmbeddingTimeoutSeconds);
        });

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<KnowledgeOptions>>().Value;
            var knowledgeDirectory = string.IsNullOrWhiteSpace(options.Path)
                ? Path.Combine(AppContext.BaseDirectory, "Knowledge")
                : options.Path;

            return new MarkdownKnowledgeLoader(
                knowledgeDirectory,
                serviceProvider.GetRequiredService<ILogger<MarkdownKnowledgeLoader>>());
        });

        services.AddSingleton<IKnowledgeSearchService>(serviceProvider =>
            new SqlKnowledgeSearchService(
                connectionString,
                serviceProvider.GetRequiredService<IEmbeddingGenerator>(),
                serviceProvider.GetRequiredService<MarkdownKnowledgeLoader>(),
                serviceProvider.GetRequiredService<IOptions<KnowledgeOptions>>(),
                serviceProvider.GetRequiredService<ILogger<SqlKnowledgeSearchService>>()));

        return services;
    }
}
