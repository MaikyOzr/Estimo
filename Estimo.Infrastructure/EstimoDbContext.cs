using Estimo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Estimo.Infrastructure;

public class EstimoDbContext : DbContext
{
    public EstimoDbContext(DbContextOptions<EstimoDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<UserBilling> UserBillings => Set<UserBilling>();
    public DbSet<UserUsage> UserUsages => Set<UserUsage>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Client>().HasIndex(x => x.OwnerId);
        b.Entity<Quote>().HasIndex(x => x.ClientId);
        b.Entity<Quote>().HasIndex(x => x.CreatedAt);
        b.Entity<Quote>()
            .HasOne<Client>()        // навігацію можна не додавати
            .WithMany()
            .HasForeignKey(x => x.ClientId);

        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.Property(x => x.EmailNormalized).IsRequired().HasMaxLength(200);
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.EmailNormalized).IsUnique();
        });
        b.Entity<UserBilling>().HasKey(x => x.UserId);
        b.Entity<UserUsage>().HasKey(x => x.UserId);
    }
}
