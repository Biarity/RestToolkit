using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestToolkit.Base;
using RestToolkit.Infrastructure;
using RestToolkit.Services;
using Sieve.Models;
using Sieve.Services;
using Swashbuckle.AspNetCore.Swagger;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;

namespace Choice
{
    public class ToolkitStartup<TSieveCustomSortMethods, TSieveCustomFilterMethods, TDbContext, TUser>
        where TSieveCustomFilterMethods : class, ISieveCustomFilterMethods
        where TSieveCustomSortMethods : class, ISieveCustomSortMethods
        where TDbContext : ToolkitDbContext
        where TUser : ToolkitUser
    {
        protected const string ClientRootPath = "ClientApp/dist";
        protected bool UseLocalDb = false;
        protected const string LocalDbConnectionConfigKey = "MSSQLLocalDB";
        protected const string NpgsqlDevConnectionConfigKey = "ElephantSql";
        protected const string NpgsqlConnectionConfigKey = "Default";
        protected const string DistributedRedisName = "MyRedis";
        protected const string DistributedRedisConnectionConfigKey = "MyRedis";
        protected const string SieveConfigSectionConfigKey = "Sieve";
        protected const string JwtAudienceConfigKey = "Auth:Audience";
        protected const string JwtAuthorityConfigKey = "Auth:AuthorityUrl";
        protected const string AuthEmailTokenProviderName = "Email";
        protected const string SignalRRedisConnectionConfigKey = "MyRedis";
        protected const string SwaggerEndpointName = "Toolkit V1";
        protected const string SpaSourcePath = "ClientApp";

        public ToolkitStartup(IConfiguration configuration, IHostingEnvironment env)
        {
            Config = configuration;
            Env = env;
        }

        public IConfiguration Config { get; }
        public IHostingEnvironment Env { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            // Directory to serve SPA files from
            // FOR PRODUCTION, RUN `npm run build` FIRST
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = ClientRootPath;
            });

            // SWASHBUCKLE
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "Choice", Version = "v1" });
            });

            // DATABASE
            services.AddDbContext<TDbContext>(opts =>
            {
                if (Env.IsDevelopment())
                {
                    opts.EnableSensitiveDataLogging(true);
                    if (UseLocalDb)
                        opts.UseSqlServer(Config.GetConnectionString(LocalDbConnectionConfigKey));
                    else
                        opts.UseNpgsql(Config.GetConnectionString(NpgsqlDevConnectionConfigKey));
                }
                else
                {
                    opts.UseNpgsql(Config.GetConnectionString(NpgsqlConnectionConfigKey));
                }
            });

            // CACHE
            services.AddMemoryCache(); // Not distributed, 1 per server, unused mostly

            if (Env.IsDevelopment())
            {
                services.AddDistributedMemoryCache(); // Not distributed, same as AddMemoryCache, good as a placeholder
            }
            else
            {
                services.AddDistributedRedisCache(options =>
                {
                    options.InstanceName = DistributedRedisName;
                    options.Configuration = Config.GetConnectionString(DistributedRedisConnectionConfigKey); // StackExchange.Redis format
                });
            }


            // MISC
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // SIEVE
            services.Configure<SieveOptions>(Config.GetSection(SieveConfigSectionConfigKey));
            services.AddScoped<SieveProcessor>();
            services.AddScoped<ISieveCustomSortMethods, TSieveCustomSortMethods>();
            services.AddScoped<ISieveCustomFilterMethods, TSieveCustomFilterMethods>();

            // EMAIL SENDER
            if (Env.IsDevelopment())
                services.AddTransient<IEmailSender, DevEmailSender>();
            else
                services.AddScoped<IEmailSender, SmtpMessageSender>();

            // RECAPTCHA
            services.AddScoped<ValidateRecaptchaAttribute>();

            // COOKIE POLICY
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            // IDENTITY
            services.AddIdentity<TUser, IdentityRole<int>>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
            })
                .AddUserManager<UserManager<TUser>>()
                .AddUserStore<UserStore<TUser, IdentityRole<int>, TDbContext, int>>()
                .AddRoleStore<RoleStore<IdentityRole<int>, TDbContext, int>>()
                .AddTokenProvider<EmailTokenProvider<TUser>>(AuthEmailTokenProviderName)
                .AddSignInManager();

            // COOKIE OPTIONS
            // Order matters for this one, should be after IDENTITY
            services.ConfigureApplicationCookie(options =>
            {
                // Return a 401 with [Authorized]
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
            });

            // AUTH TOKEN VALIDATION
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(opts =>
            {
                opts.Audience = Config[JwtAudienceConfigKey];
                opts.Authority = Config[JwtAuthorityConfigKey];
                opts.IncludeErrorDetails = true;

                if (Env.IsDevelopment())
                    opts.RequireHttpsMetadata = false;
            });

            // RESPONSE CACHING
            services.AddResponseCaching();

            // SIGNALR
            if (Env.IsDevelopment())
            {
                services.AddSignalR(); // Note: SignalR stores group/connection info in-memory (literally a dictionary)
                                       // and as such each connection is tied to a specific server. There is a possibility
                                       // of using distributed Redis instead but requires more work.
                                       // This also means it doesn't matter if you simply store group info yourself in a
                                       // dictionary (MemoryCache), no gain from using a distributed cache (Redis) for this
            }
            else
            {
                services.AddSignalR()
                    .AddRedis(Config.GetConnectionString(SignalRRedisConnectionConfigKey)); // TODO: does this clash?
            }
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseAuthentication();
            app.AddUserDetailCookie();

            if (env.IsDevelopment())
            {
                // SWASHBUCKLE
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", SwaggerEndpointName);
                });

                //app.UseSetDevelopmentUser();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseCookiePolicy();
            app.UseHttpsRedirection();

            app.UseResponseCaching();

            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseSieveExceptionHandler();

            // STILL NEED TO DO THIS IN CHILD CLASS
            //app.UseSignalR(routes =>
            //{
            //    routes.MapHub<CommentsHub>("/CommentsHub");
            //});

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "api/{controller}");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = SpaSourcePath;

                // FOR DEVELOPMENT, RUN `npm run serve` FIRST
                if (env.IsDevelopment())
                {
                    spa.UseProxyToSpaDevelopmentServer("http://localhost:5001");
                }
            });
        }
    }
}
