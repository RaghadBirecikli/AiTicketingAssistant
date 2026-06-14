namespace AiTicketing.Application.Users;

public interface IUserLookupService
{
    Task<IReadOnlyList<AgentLookupResponse>> GetAgentsAsync(CancellationToken cancellationToken = default);

    Task<AgentLookupResponse?> GetActiveAgentByIdAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
