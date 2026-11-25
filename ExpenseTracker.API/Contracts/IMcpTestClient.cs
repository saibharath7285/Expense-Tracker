using ExpenseTracker.API.Entities;

namespace ExpenseTracker.API.Contracts
{
    public interface IMcpTestClient
    {
        Task<McpToolResponse> CallToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?>? arguments,
            CancellationToken cancellationToken);

        Task<object[]> GetToolDeclarationsAsync(
            CancellationToken cancellationToken);
    }
}
