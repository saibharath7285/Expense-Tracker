using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.API.Entities;

namespace ExpenseTracker.API.Contracts
{
    public interface ICopilotService
    {
        Task<CopilotChatResponse> ChatAsync(CopilotChatRequest request, CancellationToken cancellationToken = default);
    }
}
