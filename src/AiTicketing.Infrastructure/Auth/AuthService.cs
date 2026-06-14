using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AiTicketing.Application.Auth;
using AiTicketing.Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ValidationFailure = FluentValidation.Results.ValidationFailure;

namespace AiTicketing.Infrastructure.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedRole = AuthRoles.Normalize(request.Role.Trim());
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        ThrowIfIdentityFailed(createResult);

        var roleResult = await userManager.AddToRoleAsync(user, normalizedRole);
        ThrowIfIdentityFailed(roleResult);

        return await CreateAuthResponseAsync(user, normalizedRole);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            throw new ValidationException([new ValidationFailure(nameof(request.Email), "Invalid email or password.")]);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!signInResult.Succeeded)
        {
            throw new ValidationException([new ValidationFailure(nameof(request.Password), "Invalid email or password.")]);
        }

        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? AuthRoles.Customer;

        return await CreateAuthResponseAsync(user, role);
    }

    private Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user, string role)
    {
        var settings = jwtOptions.Value;
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(settings.ExpirationMinutes);
        var signingKey = CreateSigningKey(settings);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.FullName ?? user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role),
            new Claim("name", user.FullName ?? user.Email ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);

        return Task.FromResult(new AuthResponse(
            user.Id,
            user.FullName ?? string.Empty,
            user.Email ?? string.Empty,
            role,
            tokenValue,
            expiresAtUtc));
    }

    private static SymmetricSecurityKey CreateSigningKey(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is not configured.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("JWT SecretKey must be at least 32 bytes.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }

    private static void ThrowIfIdentityFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var failures = result.Errors
            .Select(error => new ValidationFailure(error.Code, error.Description))
            .ToArray();

        throw new ValidationException(failures);
    }
}
