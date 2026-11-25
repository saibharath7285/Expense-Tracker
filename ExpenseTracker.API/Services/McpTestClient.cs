using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ExpenseTracker.API.Services;

public sealed class McpTestClient : IMcpTestClient
{
    private readonly Uri _serverUri;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private McpClient? _client;

    public McpTestClient(string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new ArgumentException("MCP Server URL cannot be null or empty.", nameof(serverUrl));
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Invalid MCP Server URL: '{serverUrl}'. Must be an absolute HTTP or HTTPS URL.", nameof(serverUrl));
        }

        _serverUri = uri;
    }

    public async Task<McpToolResponse> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?>? arguments, CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken);

        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);

        var output = string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(content => content.Text));

        if (string.IsNullOrWhiteSpace(output) && result.StructuredContent is not null)
        {
            output = result.StructuredContent.Value.ToString();
        }

        return new McpToolResponse(result.IsError != true, output);
    }

    public async Task<object[]> GetToolDeclarationsAsync(CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        return tools.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.JsonSchema
            }
        }).ToArray();
    }

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _initializationLock.WaitAsync(cancellationToken);

        try
        {
            if (_client is not null)
            {
                return _client;
            }

            var httpTransport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = _serverUri,
                TransportMode = HttpTransportMode.AutoDetect
            });

            _client = await McpClient.CreateAsync(
                httpTransport,
                cancellationToken: cancellationToken);

            return _client;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}