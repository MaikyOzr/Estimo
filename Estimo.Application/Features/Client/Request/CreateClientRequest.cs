using Estimo.Application.Features.Client.Response;
using MediatR;

namespace Estimo.Application.Features.Client.Request;

public sealed record CreateClientRequest(string Name, string? VatNumber, string? Address): IRequest<CreateClientResponse>;
