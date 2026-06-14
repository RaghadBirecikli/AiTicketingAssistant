namespace AiTicketing.Infrastructure.Ai;

public sealed class OllamaSettings
{
    public string BaseUrl { get; init; } = "http://localhost:11434";

    public string Model { get; init; } = "llama3.2";

    public int TimeoutSeconds { get; init; } = 15;

    public bool FallbackToRuleBased { get; init; } = true;
}
