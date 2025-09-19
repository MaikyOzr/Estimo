using Estimo.Application.Features.Auth;
using Estimo.Application.Features.Quote.Request;
using Estimo.Application.Features.Quote.Response;
using Estimo.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Estimo.Application.Features.Quote.Command;

public sealed class CreateQuoteCommand(EstimoDbContext _context, ICurrentUser currentUser) : IRequestHandler<CreateQuoteRequest, CreateQuoteResponse>
{
    public async Task<CreateQuoteResponse> Handle(CreateQuoteRequest request, CancellationToken cancellationToken)
    {
        var client = await _context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(x=> x.Id == request.clientId && x.OwnerId == currentUser.Id) 
            ?? throw new Exception("Client not found!");

        var defaultQuoteName= string.IsNullOrWhiteSpace(request.name)
            ? $"Quote for {client.Name} - {DateTime.UtcNow:yyyyMMddHHmmss}" : request.name;

        var quote = new Domain.Quote
        {
            Name = defaultQuoteName,
            ClientId = request.clientId,
            VatPercent = request.vatPercent,
            Amount = request.amount,
            CreatedAt = DateTime.UtcNow
        };

        _context.Quotes.Add(quote);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateQuoteResponse(quote.Id);
    }
}
