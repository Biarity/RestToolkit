using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestToolkit.Infrastructure;
using System;
using System.Linq;

namespace RestToolkit.Services
{
    public abstract class ToolkitRepository<TEntity, TDbContext, TUser>
        where TEntity : ToolkitEntity<TUser>, new()
        where TDbContext : DbContext, new()
        where TUser : IdentityUser
    {
        protected HttpContext _httpContext { get; set; }
        protected TDbContext _dbContext { get; set; }

        public bool IsAllAllowedByDefault { get; set; } = false;

        public ToolkitRepository(IHttpContextAccessor httpContextAccessor,
            TDbContext dbContext)
        {
            _httpContext = httpContextAccessor.HttpContext;
            _dbContext = dbContext;
        }

        public virtual bool OnCreate(TEntity entity)
        {
            InitCreate(entity);
            return IsAllAllowedByDefault;
        }

        public virtual object OnRead(IQueryable<TEntity> entities, int? id)
        {
            return IsAllAllowedByDefault ? entities : null;
        }

        public virtual bool OnUpdate(TEntity entity)
        {
            return IsAllAllowedByDefault;
        }

        public virtual bool OnDelete(int entityId)
        {
            return IsAllAllowedByDefault;
        }

        #region HELPERS

        protected int CurrentUserId
        {
            get
            {
                return _httpContext.User.GetUserId();
            }
        }

        public virtual TEntity InitCreate(TEntity entity, dynamic info = null)
        {
            return InitCreate<TEntity, TUser>(entity, info);
        }

        public _TEntity InitCreate<_TEntity, _TUser>(_TEntity entity, dynamic info = null)
            where _TEntity : ToolkitEntity<_TUser>, new()
            where _TUser : IdentityUser
        {
            entity.Created = DateTimeOffset.UtcNow;
            entity.UserId = CurrentUserId;
            entity.InitCreate(info);
            return entity;
        }

        protected string GetQueryString(string key)
        {
            var query = _httpContext.Request.Query[key].ToString();
            return query.Length < 1 ? null : query;
        }

        protected bool DoesCurrentUserOwnAnyOf(int Id)
        {
            return _dbContext.Set<TEntity>().AsNoTracking()
                    .Any(e => e.Id == Id && e.UserId == CurrentUserId);
        }
        
        protected void FilterToUserOwned(ref IQueryable<TEntity> entities)
        {
            entities = entities.Where(e => e.UserId == CurrentUserId);
        }

        #endregion HELPERS
    }

}
