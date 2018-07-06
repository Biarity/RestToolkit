using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestToolkit.Infrastructure;
using RestToolkit.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RestToolkit.Services
{
    public abstract class ToolkitRepository<TEntity, TDbContext, TUser> : ToolkitRepository<TEntity, TDbContext, TUser, ExtraQueries>
        where TEntity : ToolkitEntity<TUser>, new()
        where TDbContext : DbContext
        where TUser : IdentityUser<int>
    {
        public ToolkitRepository(IHttpContextAccessor httpContextAccessor, TDbContext dbContext, ILogger<ToolkitRepository<TEntity, TDbContext, TUser, ExtraQueries>> logger) : base(httpContextAccessor, dbContext, logger)
        {
        }
    }

    public abstract class ToolkitRepository<TEntity, TDbContext, TUser, TExtraQueries>
        where TEntity : ToolkitEntity<TUser>, new()
        where TDbContext : DbContext
        where TUser : IdentityUser<int>
        where TExtraQueries : class
    {
        protected HttpContext _httpContext { get; set; }
        protected TDbContext _dbContext { get; set; }
        protected readonly ILogger _logger;

        public bool IsAllAllowedByDefault { get; set; } = false;

        public ToolkitRepository(IHttpContextAccessor httpContextAccessor,
            TDbContext dbContext,
            ILogger<ToolkitRepository<TEntity, TDbContext, TUser, TExtraQueries>> logger)
        {
            _httpContext = httpContextAccessor.HttpContext;
            _dbContext = dbContext;
            _logger = logger;
        }

        #region PUBLIC ASYNC CRUD METHODS
        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        public virtual async Task<RepositoryResponse> OnCreateAsync(TEntity entity)
        {
            return OnCreate(entity);
        }

        public virtual async Task<(RepositoryResponse, IQueryable<object>)> OnReadAsync(IQueryable<TEntity> entities, int? id, TExtraQueries extraQueries = null)
        {
            return OnRead(entities, id, extraQueries);
        }

        public virtual async Task<RepositoryResponse> OnUpdateAsync(int entityId, TEntity entity)
        {
            return OnUpdate(entityId, entity);
        }

        public virtual async Task<RepositoryResponse> OnDeleteAsync(int entityId)
        {
            return OnDelete(entityId);
        }

        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        #endregion

        #region PRIVATE NON-ASYNC CRUD METHODS

        private RepositoryResponse OnCreate(TEntity entity)
        {
            return new RepositoryResponse
            {
                Success = IsAllAllowedByDefault
            };
        }

        private (RepositoryResponse, IQueryable<object>) OnRead(IQueryable<TEntity> entities, int? id, TExtraQueries extraQueries = null)
        {
            return (new RepositoryResponse
            {
                Success = IsAllAllowedByDefault
            }, entities);
        }

        private RepositoryResponse OnUpdate(int entityId, TEntity entity)
        {
            return new RepositoryResponse
            {
                Success = IsAllAllowedByDefault
            };
        }

        private RepositoryResponse OnDelete(int entityId)
        {
            return new RepositoryResponse
            {
                Success = IsAllAllowedByDefault
            };
        }

        #endregion

        #region HELPERS

        protected bool IsUserAuthenticated
        {
            get
            {
                return _httpContext.User.Identity.IsAuthenticated;
            }
        }

        protected int CurrentUserId
        {
            get
            {
                return _httpContext.User.GetUserId();
            }
        }

        protected string GetQueryString(string key)
        {
            var query = _httpContext.Request.Path.ToString();
            return query.Length < 1 ? null : query;
        }
       
        protected async Task<bool> DoesCurrentUserOwnAnyOfAsync(int Id)
        {
            return await _dbContext.Set<TEntity>().AsNoTracking()
                    .AnyAsync(e => e.Id == Id && e.UserId == CurrentUserId);
        }
        
        protected void FilterToUserOwned(ref IQueryable<TEntity> entities)
        {
            entities = entities.Where(e => e.UserId == CurrentUserId);
        }
        
        #endregion HELPERS
    }

}
