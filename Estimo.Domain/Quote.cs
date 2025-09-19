namespace Estimo.Domain;

public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid ClientId { get; set; }

    public required string Name { get; set; }

    public required decimal Amount { get; set; }
    
    public required decimal VatPercent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? PaymentUrl { get; set; }           // <— нове
    public string? PaymentSessionId { get; set; }     // <— нове (для confirm)
    public string PaymentStatus { get; set; } = "new";// new|pending|paid|canceled
}
