using FocusBot.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserTask> UserTasks => Set<UserTask>();
    public DbSet<WindowContext> WindowContexts => Set<WindowContext>();
    public DbSet<AlignmentCacheEntry> AlignmentCacheEntries => Set<AlignmentCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<UserTask>(entity =>
        {
            entity.HasKey(e => e.TaskId);
            entity.Property(e => e.TaskId).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Context).HasMaxLength(1024);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<WindowContext>(entity =>
        {
            entity.HasKey(e => e.ContextHash);
            entity.Property(e => e.ContextHash).HasMaxLength(64);
            entity.Property(e => e.ProcessName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.WindowTitle).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<AlignmentCacheEntry>(entity =>
        {
            entity.HasKey(e => new { e.ContextHash, e.TaskContentHash });
            entity.Property(e => e.ContextHash).HasMaxLength(64);
            entity.Property(e => e.TaskContentHash).HasMaxLength(64);
            entity.Property(e => e.Reason).HasMaxLength(1024).IsRequired();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne<WindowContext>()
                .WithMany()
                .HasForeignKey(e => e.ContextHash)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
