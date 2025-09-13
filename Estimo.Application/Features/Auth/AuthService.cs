using Estimo.Domain;
using Estimo.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Estimo.Application.Features.Auth;

public sealed class AuthService(EstimoDbContext _context, IPasswordHasher<User> hasher) : IAuthService
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var norm = email.Trim().ToUpperInvariant();
        return _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.EmailNormalized == norm, ct);
    }

    public async Task<Guid> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();

        var user = new User
        {
            Email = email.Trim(),
            EmailNormalized = normalizedEmail
        };

        user.PasswordHash = hasher.HashPassword(user, password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);
        return user.Id;
    }

    public bool VerifyPassword(User user, string password)
    {
        return new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
