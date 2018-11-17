using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RestToolkit.Base
{
    public class ToolkitDbContext : IdentityDbContext<ToolkitUser, IdentityRole<int>, int>
    {
        public ToolkitDbContext(DbContextOptions options) : base(options) { }
    }
}
