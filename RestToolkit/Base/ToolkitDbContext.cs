using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RestToolkit.Base
{
    public abstract class ToolkitDbContext<TUser> : IdentityDbContext<TUser, IdentityRole<int>, int>
        where TUser : ToolkitUser
    {
        public ToolkitDbContext(DbContextOptions options) : base(options) { }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            SeedUsers(modelBuilder);
        }

        protected virtual void SeedUsers(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TUser>().HasData(
                new { Id = 0, UserName = "dev", Email = "biarity@outlook.com" },
                new { Id = 1, UserName = "dev2", Email = "example@example.com" },
                new { Id = 2, UserName = "dev3", Email = "example@example.com" });
        }
    }
}
