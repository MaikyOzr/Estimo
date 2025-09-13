namespace Estimo.Application.Features.Client.Response;

public sealed record GetClientByIdResponse(string Name,
    string? VatNumber,
    string? Address,
    Guid userId
    );
