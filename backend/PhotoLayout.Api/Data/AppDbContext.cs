using Microsoft.EntityFrameworkCore;
using PhotoLayout.Api.Models;

namespace PhotoLayout.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UploadedImage> UploadedImages => Set<UploadedImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UploadedImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128);
            e.Property(x => x.OriginalRelativePath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.WhiteBackgroundRelativePath).HasMaxLength(1024);
        });
    }
}
