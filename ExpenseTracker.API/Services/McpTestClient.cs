using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ExpenseTracker.API.Services;

public sealed class McpTestClient : IMcpTestClient
{
    private readonly string _serverDllPath;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private McpClient? _client;

    public McpTestClient(string serverDllPath)
    {
        _serverDllPath = serverDllPath;
    }

    public async Task<McpToolResponse> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken);

        var result = await client.CallToolAsync(
            toolName,
            arguments,
            cancellationToken: cancellationToken);

        var output = string.Join(
            Environment.NewLine,
            result.Content
                .OfType<TextContentBlock>()
                .Select(content => content.Text));

        if (string.IsNullOrWhiteSpace(output) &&
            result.StructuredContent is not null)
        {
            output = result.StructuredContent.Value.ToString();
        }

        return new McpToolResponse(
            result.IsError != true,
            output);
    }

    private async Task<McpClient> GetClientAsync(
        CancellationToken cancellationToken)
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

            if (!File.Exists(_serverDllPath))
            {
                throw new FileNotFoundException(
                    "The ExpenseTracker MCP server has not been built.",
                    _serverDllPath);
            }

            var workingDirectory =
                Path.GetDirectoryName(_serverDllPath)
                ?? throw new InvalidOperationException(
                    "The MCP server working directory is invalid.");

            var environmentVariables =
                StdioClientTransportOptions.GetDefaultEnvironmentVariables();

            var transport = new StdioClientTransport(
                new StdioClientTransportOptions
                {
                    Name = "ExpenseTracker",
                    Command = "dotnet",
                    Arguments = [_serverDllPath],
                    WorkingDirectory = workingDirectory,
                    InheritEnvironmentVariables = false,
                    EnvironmentVariables = environmentVariables,
                    ShutdownTimeout = TimeSpan.FromSeconds(10),
                });

            _client = await McpClient.CreateAsync(
                transport,
                cancellationToken: cancellationToken);

            return _client;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}