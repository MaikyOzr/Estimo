namespace Estimo.Domain;

public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid ClientId { get; set; }

    public required string Name { get; set; }

    public required decimal Amount { get; set; }
    
    public required decimal VatPercent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal Total => Amount + (Amount * VatPercent / 100m);
}
