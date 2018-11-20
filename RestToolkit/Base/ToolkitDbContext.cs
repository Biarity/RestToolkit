using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RestToolkit.Base
{
    public abstract class ToolkitDbContext<TUser> : IdentityDbContext<TUser, IdentityRole<int>, int>
        where TUser : ToolkitUser
    {
        public ToolkitDbContext(DbContextOptions options) : base(options) { }
    }
}
