using AiTicketing.Application.Auth;
using AiTicketing.Application.Auth.Validation;
using AiTicketing.Application.Tickets;
using AiTicketing.Application.Tickets.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AiTicketing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateTicketRequest>, CreateTicketRequestValidator>();
        services.AddScoped<IValidator<AddTicketMessageRequest>, AddTicketMessageRequestValidator>();
        services.AddScoped<IValidator<AddInternalNoteRequest>, AddInternalNoteRequestValidator>();
        services.AddScoped<IValidator<SuggestReplyRequest>, SuggestReplyRequestValidator>();
        services.AddScoped<IValidator<SuggestTriageRequest>, SuggestTriageRequestValidator>();
        services.AddScoped<IValidator<ChangeTicketStatusRequest>, ChangeTicketStatusRequestValidator>();
        services.AddScoped<IValidator<AssignTicketRequest>, AssignTicketRequestValidator>();
        services.AddScoped<IValidator<DeleteTicketRequest>, DeleteTicketRequestValidator>();
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

        return services;
    }
}
