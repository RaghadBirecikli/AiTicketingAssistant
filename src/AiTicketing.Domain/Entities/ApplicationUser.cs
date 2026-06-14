using Microsoft.AspNetCore.Identity;

namespace AiTicketing.Domain.Entities;

public sealed class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
}
