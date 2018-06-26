using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestToolkit.Infrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RestToolkit.Services
{
    public abstract class ToolkitRepository<TEntity, TDbContext, TUser, TKey>
        where TEntity : ToolkitEntity<TUser, TKey>, new()
        where TDbContext : DbContext
        where TUser : IdentityUser<TKey>
        where TKey : IEquatable<TKey>
    {
        protected HttpContext _httpContext { get; set; }
        protected TDbContext _dbContext { get; set; }
        protected readonly ILogger _logger;

        public bool IsAllAllowedByDefault { get; set; } = false;

        public ToolkitRepository(IHttpContextAccessor httpContextAccessor,
            TDbContext dbContext,
            ILogger<ToolkitRepository<TEntity, TDbContext, TUser, TKey>> logger)
        {
            _httpContext = httpContextAccessor.HttpContext;
            _dbContext = dbContext;
            _logger = logger;
        }

        #region PUBLIC ASYNC CRUD METHODS
        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        public virtual async Task<bool> OnCreateAsync(TEntity entity)
        {
            return OnCreate(entity);
        }

        public virtual async Task<object> OnReadAsync(IQueryable<TEntity> entities, int? id)
        {
            return OnRead(entities, id);
        }

        public virtual async Task<bool> OnUpdateAsync(TEntity entity)
        {
            return OnUpdate(entity);
        }

        public virtual async Task<bool> OnDeleteAsync(int entityId)
        {
            return OnDelete(entityId);
        }

        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        #endregion

        #region PRIVATE NON-ASYNC CRUD METHODS

        private bool OnCreate(TEntity entity)
        {
            return IsAllAllowedByDefault;
        }

        private object OnRead(IQueryable<TEntity> entities, int? id)
        {
            return IsAllAllowedByDefault ? entities : null;
        }

        private bool OnUpdate(TEntity entity)
        {
            return IsAllAllowedByDefault;
        }

        private bool OnDelete(int entityId)
        {
            return IsAllAllowedByDefault;
        }

        #endregion

        #region HELPERS

        protected string CurrentUserId
        {
            get
            {
                return _httpContext.User.GetUserId();
            }
        }

        protected string GetQueryString(string key)
        {
            var query = _httpContext.Request.Query[key].ToString();
            return query.Length < 1 ? null : query;
        }
        /*
        protected bool DoesCurrentUserOwnAnyOf(TKey Id)
        {
            return _dbContext.Set<TEntity>().AsNoTracking()
                    .Any(e => e.Id == Id && e.UserId == CurrentUserId);
        }
        
        protected void FilterToUserOwned(ref IQueryable<TEntity> entities)
        {
            entities = entities.Where(e => e.UserId == CurrentUserId);
        }
        */
        #endregion HELPERS
    }

}
