using Estimo.Application.Features.Quote.Response;
using MediatR;

namespace Estimo.Application.Features.Quote.Request;

public sealed record CreateQuoteRequest(
    string name,
    Guid clientId,
    decimal amount,
    decimal vatPercent
    ) : IRequest<CreateQuoteResponse>;
