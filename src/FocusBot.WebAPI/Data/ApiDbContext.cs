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
            entity.HasIndex(s => s.UserId)
                .HasFilter("\"EndedAtUtc\" IS NULL")
                .IsUnique();
            entity.Property(s => s.TaskText).HasMaxLength(500);
            entity.Property(s => s.Source).HasMaxLength(20);
        });
    }
}
