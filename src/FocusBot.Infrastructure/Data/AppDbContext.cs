using FocusBot.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AlignmentCacheEntry> AlignmentCacheEntries => Set<AlignmentCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.Property(e => e.SessionId).HasMaxLength(64);
            entity.Property(e => e.SessionTitle).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Context).HasMaxLength(1024);
            entity.Property(e => e.IsCompleted).IsRequired();
            entity.Property(e => e.TopDistractingApps).HasMaxLength(2048);
            entity.HasIndex(e => e.IsCompleted);
        });

        modelBuilder.Entity<AlignmentCacheEntry>(entity =>
        {
            entity.HasKey(e => new { e.ContextHash, e.TaskContentHash });
            entity.Property(e => e.ContextHash).HasMaxLength(64);
            entity.Property(e => e.TaskContentHash).HasMaxLength(64);
            entity.Property(e => e.Reason).HasMaxLength(1024).IsRequired();
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
