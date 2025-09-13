namespace Estimo.Domain;

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerId { get; set; }

    public required string Name { get; set; } = string.Empty;

    public string? Address { get; set; }
    public string? VatNumber { get; set; }
}
