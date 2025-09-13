using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace Estimo.Application.Features.Auth;

public sealed class CurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    public Guid Id
    {
        get
        {
            var ctx = http.HttpContext ?? throw new UnauthorizedAccessException("No HttpContext");
            var raw =
                ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                ctx.User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(raw))
                throw new UnauthorizedAccessException("User ID claim is missing.");
            if (!Guid.TryParse(raw, out var id))
                throw new UnauthorizedAccessException($"User ID claim is invalid: {raw}");

            return id;
        }
    }
}