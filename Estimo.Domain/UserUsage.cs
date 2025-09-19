namespace Estimo.Domain;

public class UserUsage
{
    public Guid UserId { get; set; }
    public DateOnly WindowDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int DailyCount { get; set; } = 0;
    public int TotalCount { get; set; } = 0;
}
