using AiTicketing.Application.Ai;
using AiTicketing.Tests.Fakes;

namespace AiTicketing.Tests.Fakes;

public sealed class FakeAiTicketAssistantServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsDeterministicFakeResponse()
    {
        var service = new FakeAiTicketAssistantService();

        var result = await service.AnalyzeAsync(new TicketAssistantRequest("Login issue", "Cannot sign in."));

        Assert.Equal("Fake", result.Provider);
        Assert.Equal("Login issue", result.Summary);
    }
}
