using Estimo.Application.Features.Auth;
using Estimo.Application.Features.Client.Request;
using Estimo.Application.Features.Client.Response;
using Estimo.Infrastructure;
using MediatR;

namespace Estimo.Application.Features.Client.Command;

public sealed class CreateClientCommand(EstimoDbContext _context, ICurrentUser currentUser) : IRequestHandler<CreateClientRequest, CreateClientResponse>
{
    public async Task<CreateClientResponse> Handle(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var client = new Domain.Client
        {
            Name = request.Name,
            VatNumber = request.VatNumber,
            Address = request.Address,
            OwnerId = currentUser.Id,
        };

        _context.Clients.Add(client);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateClientResponse(client.Id);
    }
}
