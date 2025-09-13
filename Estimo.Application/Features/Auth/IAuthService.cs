using Estimo.Domain;

namespace Estimo.Application.Features.Auth;

public interface IAuthService
{
    Task<Guid> RegisterAsync(string email, string password, CancellationToken ct);
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    bool VerifyPassword(User user, string password);
}
