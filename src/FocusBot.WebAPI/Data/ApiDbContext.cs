using FocusBot.WebAPI.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Data;

/// <summary>
/// EF Core DbContext for the FocusBot Web API backed by PostgreSQL.
/// </summary>
public class ApiDbContext(DbContextOptions<ApiDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

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
    }
}
