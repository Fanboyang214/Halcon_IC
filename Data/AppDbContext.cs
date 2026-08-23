
using Core.Models;
using Data.Entity;
using Microsoft.EntityFrameworkCore;

namespace Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Member> Members { get; set; }
        public DbSet<ProductInspectionRecord> ProductInspectionRecords { get; set; }

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       { 
            modelBuilder.Entity<ProductInspectionRecord>()
                .HasKey(p => p.RecordId);
        }
    }
}
