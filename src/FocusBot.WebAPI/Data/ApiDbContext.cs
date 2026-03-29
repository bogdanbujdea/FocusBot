using FocusBot.WebAPI.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Data;

/// <summary>
/// EF Core DbContext for the FocusBot Web API backed by PostgreSQL.
/// </summary>
public class ApiDbContext(DbContextOptions<ApiDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<ClassificationCache> ClassificationCaches => Set<ClassificationCache>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).ValueGeneratedNever();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(320);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedNever();
            entity.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);
            entity.HasOne<Client>().WithMany().HasForeignKey(s => s.ClientId).IsRequired(false);
            entity.HasIndex(s => s.UserId).HasFilter("\"EndedAtUtc\" IS NULL").IsUnique();
            entity.Property(s => s.SessionTitle).HasMaxLength(200);
            entity.Property(s => s.Context).HasMaxLength(500);
            entity.Property(s => s.Source).HasMaxLength(20);
        });

        modelBuilder.Entity<ClassificationCache>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
            entity.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId);
            entity.HasIndex(c => new
            {
                c.UserId,
                c.ContextHash,
                c.TaskContentHash,
            });
            entity.Property(c => c.ContextHash).HasMaxLength(64);
            entity.Property(c => c.TaskContentHash).HasMaxLength(64);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedNever();
            entity.HasOne(s => s.User).WithOne().HasForeignKey<Subscription>(s => s.UserId);
            entity.HasIndex(s => s.UserId).IsUnique();
            entity.Property(s => s.PaddleSubscriptionId).HasMaxLength(100);
            entity.Property(s => s.PaddleCustomerId).HasMaxLength(100);
            entity.Property(s => s.Status).HasMaxLength(32);
            entity.Property(s => s.PaddlePriceId).HasMaxLength(100);
            entity.Property(s => s.PaddleProductId).HasMaxLength(100);
            entity.Property(s => s.PaddleTransactionId).HasMaxLength(100);
            entity.Property(s => s.CustomerEmail).HasMaxLength(320);
            entity.Property(s => s.CurrencyCode).HasMaxLength(10);
            entity.Property(s => s.BillingInterval).HasMaxLength(20);
            entity.Property(s => s.CancellationReason).HasMaxLength(500);
            entity.Property(s => s.PaymentMethodType).HasMaxLength(50);
            entity.Property(s => s.CardLastFour).HasMaxLength(10);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
            entity.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId);
            entity.HasIndex(c => new { c.UserId, c.Fingerprint }).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(100);
            entity.Property(c => c.Fingerprint).HasMaxLength(100);
            entity.Property(c => c.AppVersion).HasMaxLength(50);
            entity.Property(c => c.Platform).HasMaxLength(500);
            entity.Property(c => c.IpAddress).HasMaxLength(45);
        });
    }
}
