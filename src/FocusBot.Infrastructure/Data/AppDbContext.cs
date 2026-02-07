using FocusBot.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserTask> UserTasks => Set<UserTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<UserTask>(entity =>
        {
            entity.HasKey(e => e.TaskId);
            entity.Property(e => e.TaskId).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => e.Status);
        });
    }
}
