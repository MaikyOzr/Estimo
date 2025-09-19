namespace Estimo.Domain;

public class UserBilling
{
    public Guid UserId { get; set; }
    public string Plan { get; set; } = "free";
    public DateTime? CurrentPeriodEndUtc { get; set; }
}
