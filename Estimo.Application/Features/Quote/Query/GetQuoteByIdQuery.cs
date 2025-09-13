using Estimo.Application.Features.Auth;
using Estimo.Application.Features.Quote.Response;
using Estimo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Estimo.Application.Features.Quote.Query;

public sealed class GetQuoteByIdQuery(EstimoDbContext _context, ICurrentUser currentUser)
{
    public async Task<GetQuoteByIdResponse> Handle(Guid id, CancellationToken cancellationToken)
    { 
        return await _context.Quotes
            .Where(x => x.Id == id)
            .Join(_context.Clients, q => q.ClientId, c => c.Id, (q, c) => new { q, c })
            .Where(x => x.c.OwnerId == currentUser.Id)
            .Select(x => new GetQuoteByIdResponse(x.q.Name, x.q.Amount, x.q.VatPercent, x.q.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("Quote not found!");
    }
}
