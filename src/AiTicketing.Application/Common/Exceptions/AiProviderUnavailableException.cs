namespace AiTicketing.Application.Common.Exceptions;

public sealed class AiProviderUnavailableException : Exception
{
    public AiProviderUnavailableException()
        : base("AI provider is unavailable.")
    {
    }
}
