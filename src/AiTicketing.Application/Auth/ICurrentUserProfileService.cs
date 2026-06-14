namespace AiTicketing.Application.Auth;

public interface ICurrentUserProfileService
{
    Task<CurrentUserResponse?> GetAsync(CancellationToken cancellationToken = default);
}
