using AiTicketing.Application.Tickets;
using AiTicketing.Application.Tickets.Validation;
using AiTicketing.Domain.Enums;

namespace AiTicketing.Tests.Tickets;

public sealed class CreateTicketRequestValidatorTests
{
    private readonly CreateTicketRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsNoErrors()
    {
        var request = new CreateTicketRequest(
            "Cannot access account",
            "The customer cannot sign in after resetting the password.",
            "customer@example.com",
            "Customer Name",
            TicketSource.Web);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenRequiredFieldsAreMissing_ReturnsErrors()
    {
        var request = new CreateTicketRequest(
            "",
            "",
            "not-an-email",
            new string('a', 151),
            (TicketSource)999);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTicketRequest.Title));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTicketRequest.Description));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTicketRequest.CustomerEmail));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTicketRequest.CustomerName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateTicketRequest.Source));
    }
}
