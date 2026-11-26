using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.API.Entities;

namespace ExpenseTracker.API.Contracts
{
    public interface ICopilotService
    {
        Task<CopilotChatResponse> ChatAsync(CopilotChatRequest request, CancellationToken cancellationToken = default);

        async IAsyncEnumerable<string> ChatStreamAsync(
            CopilotChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await ChatAsync(request, cancellationToken);
            if (!response.Success)
            {
                throw new InvalidOperationException(response.Error ?? response.Reply);
            }

            yield return response.Reply;
        }
    }
}
