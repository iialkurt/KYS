using KYS.Models;
using Microsoft.EntityFrameworkCore;

namespace KYS.Context;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }
    public DbSet<User01> User01 { get; set; }
    //override protected void OnModelCreating(ModelBuilder modelBuilder)
    //{
    //    modelBuilder.Entity<User01>(entity =>
    //    {
    //        entity.HasOne(p=> p.UserName)
    //        .WithOne()
    //        .HasForeignKey<User01>(p => p.Id)
    //        .OnDelete(DeleteBehavior.NoAction);

    //    });
    //}
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Abstractions.Entity entity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entity.CreatedAt = DateTimeOffset.UtcNow;
                        break;
                    case EntityState.Modified:
                        if (entity.IsDeleted)
                        {
                            entity.DeletedAt = DateTimeOffset.UtcNow;
                        }
                        else
                        {
                            entity.UpdatedAt = DateTimeOffset.UtcNow;
                        }
                        break;
                  
                }
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
     

}
