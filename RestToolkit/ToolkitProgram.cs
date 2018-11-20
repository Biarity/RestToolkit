using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestToolkit.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RestToolkit
{
    public abstract class ToolkitProgram<TStartup, TDbContext, TUser>
        where TStartup : class
        where TDbContext : ToolkitDbContext<TUser>
        where TUser : ToolkitUser, new()
    {
        public static void Main(string[] args)
        {
            var host = CreateWebHostBuilder(args).Build();

            // Don't initialise database if given the IgnoreInitDb argument
            var ignoreInitDb = args.FirstOrDefault(a => a.ToLower().Contains("ignoreinitdb"));
            if (ignoreInitDb == null)
            {
                using (var scope = host.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;

                    try
                    {
                        InitializeAsync(services, CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        var logger = services.GetRequiredService<ILogger>();
                        logger.LogError(ex, "An error occurred seeding the DB.");
                    }
                }
            }

            host.Run();
        }

        private static int ILogger<T>()
        {
            throw new NotImplementedException();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<TStartup>();

        private async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            using (var scope = services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

                if (dbContext.Users.Count() < 3)
                {
                    dbContext.Users.Add(new TUser()
                    {
                        Id = 0,
                        UserName = "dev",
                        Email = "email1@domain.com"
                    });

                    dbContext.Users.Add(new TUser()
                    {
                        Id = 1,
                        UserName = "qud",
                        Email = "email2@domain.com"
                    });

                    dbContext.Users.Add(new TUser()
                    {
                        Id = 2,
                        UserName = "bia",
                        Email = "biarity@outlook.com"
                    });
                }

                InitDb(dbContext);

                await dbContext.SaveChangesAsync();
            }
        }

        protected abstract void InitDb(TDbContext dbContext);

    }
}
