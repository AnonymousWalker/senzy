using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Senzy.Api.Features.Identity;
using Senzy.Api.Features.Profiles;

namespace Senzy.Api.Infrastructure.Persistence;

public sealed class SenzyDbContext(DbContextOptions<SenzyDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.Entity<Profile>(entity =>
        {
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.DisplayName).HasMaxLength(80).IsRequired();
            entity.Property(profile => profile.Handle).HasMaxLength(20).IsRequired();
            entity.HasIndex(profile => profile.Handle).IsUnique();
            entity.HasOne<ApplicationUser>().WithOne().HasForeignKey<Profile>(profile => profile.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
