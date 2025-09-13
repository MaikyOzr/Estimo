namespace Estimo.Application.Features.Quote.Response;

public sealed record GetQuoteByIdResponse(string name, decimal amount, decimal vatPercent, DateTime createdAt);
