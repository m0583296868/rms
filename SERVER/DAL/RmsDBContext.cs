using Microsoft.EntityFrameworkCore;
using RMS.Models;

namespace RMS.DAL
{
    public class RmsDBContext : DbContext
    {
        public RmsDBContext(DbContextOptions<RmsDBContext> options) : base(options)
        {
        }

        public DbSet<Requests> requests { get; set; }
        public DbSet<StatusHistories> statusHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Requests>(entity =>
            {
                entity.Property(r => r.Title).HasMaxLength(200).IsRequired();
                entity.Property(r => r.OrganizationName).HasMaxLength(150).IsRequired();
                entity.Property(r => r.AssignedTo).HasMaxLength(100).IsRequired();
                entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(r => r.Priority).HasConversion<string>().HasMaxLength(20);
                entity.Property(r => r.Version).IsConcurrencyToken();

                // הסינון המרכזי במסך: סטטוס + עדיפות + מיון לפי תאריך
                entity.HasIndex(r => new { r.Status, r.Priority, r.CreatedAt, r.Id })
                    .IsDescending(false, false, true, false)
                    .HasDatabaseName("IX_Requests_Status_Priority_CreatedAt");
                entity.HasIndex(r => r.AssignedTo).HasDatabaseName("IX_Requests_AssignedTo");
                entity.HasIndex(r => r.OrganizationName).HasDatabaseName("IX_Requests_OrganizationName");
                entity.HasIndex(r => r.CreatedAt).HasDatabaseName("IX_Requests_CreatedAt");
            });

            modelBuilder.Entity<StatusHistories>(entity =>
            {
                entity.Property(h => h.ChangedBy).HasMaxLength(100).IsRequired();
                entity.Property(h => h.PreviousStatus).HasConversion<string>().HasMaxLength(20);
                entity.Property(h => h.NewStatus).HasConversion<string>().HasMaxLength(20);
                entity.HasIndex(h => h.RequestId).HasDatabaseName("IX_StatusHistories_RequestId");
                entity.HasOne(h => h.Request)
                    .WithMany(r => r.History)
                    .HasForeignKey(h => h.RequestId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
