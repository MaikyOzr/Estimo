using Estimo.Application.Features.Auth;
using Estimo.Application.Features.Client.Response;
using Estimo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Estimo.Application.Features.Client.Query;

public sealed class GetClientByIIdQuery(EstimoDbContext _context)
{
    public async Task<GetClientByIdResponse> Handle(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Clients.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new GetClientByIdResponse(x.Name, x.Address, x.VatNumber, x.OwnerId))
            .FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("User not found!");
    }
}
