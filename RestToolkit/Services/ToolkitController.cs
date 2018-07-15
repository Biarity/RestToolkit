using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using RestToolkit.Infrastructure;
using Sieve.Models;
using Sieve.Services;
using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace RestToolkit.Services
{
    public abstract class ToolkitController<TEntity, TDbContext, TUser>
        : ToolkitController<TEntity, TDbContext, TUser, SieveProcessor, SieveModel, FilterTerm, SortTerm>
        where TEntity : ToolkitEntity<TUser>, new()
        where TDbContext : DbContext
        where TUser : IdentityUser<int>
    {
        public ToolkitController(TDbContext dbContext, SieveProcessor sieveProcessor, ILogger<ToolkitController<TEntity, TDbContext, TUser, SieveProcessor, SieveModel, FilterTerm, SortTerm>> logger) : base(dbContext, sieveProcessor, logger)
        {
        }
    }

    [ApiController]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public abstract class ToolkitController<TEntity, TDbContext, TUser, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm> : ControllerBase
        where TEntity : ToolkitEntity<TUser>, new()
        where TDbContext : DbContext
        where TUser : IdentityUser<int>
        where TSieveProcessor : class, ISieveProcessor<TSieveModel, TFilterTerm, TSortTerm>
        where TSieveModel : class, ISieveModel<TFilterTerm, TSortTerm>
        where TFilterTerm : IFilterTerm, new()
        where TSortTerm : ISortTerm, new()
    {
        protected TDbContext _dbContext;
        protected TSieveProcessor _sieveProcessor;
        private readonly ILogger _logger;

        public ToolkitController(
            TDbContext dbContext,
            TSieveProcessor sieveProcessor,
            ILogger<ToolkitController<TEntity, TDbContext, TUser, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm>> logger)
        {
            _dbContext = dbContext;
            _sieveProcessor = sieveProcessor;
            _logger = logger;
        }


        [Authorize]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Create([FromBody]TEntity entity)
        {
            entity.Create(CurrentUserId);

            _dbContext.Add(entity);

            return await SaveChangesAndReturn(entity);
        }
        
        [HttpGet("{id}")]
        [ResponseCache(Duration = 30)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public virtual async Task<IActionResult> Read(int id)
        {
            var source = GetAsNoTracking();

            source = FilterToId(id, source);

            var result = await source.FirstOrDefaultAsync();

            return Ok(result);
        }

        [HttpGet]
        [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "*" })]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public virtual async Task<IActionResult> Read([FromQuery]TSieveModel sieveModel)
        {
            var source = GetAsNoTracking();
            
            source = ApplyFilterAndSort(sieveModel, source);
            source = ApplyPagination(sieveModel, source);
            
            var result = await source.ToListAsync();

            return Ok(result);
        }

        [Authorize]
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Update(int id, [FromBody]TEntity entity)
        {
            var entry = AttachGetEntry(id, entity);

            // Mark proeprties modified

            return await SaveChangesAndReturn();
        }

        [Authorize]
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Delete(int id)
        {
            RemoveWithId(id);

            return await SaveChangesAndReturn();
        }

        #region HELEPRS

        protected virtual async Task<IActionResult> SaveChangesAndReturn(object result = null)
        {
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is DbException)
            {
                return BadRequest();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(StatusCodes.Status410Gone);
            }
            catch (DbUpdateException)
            {
                throw; // 500
            }

            if (result == null)
            {
                return NoContent();
            }
            else
            {
                return Ok(result);
            }
        }

        protected virtual bool IsUserAuthenticated
        {
            get
            {
                return User.Identity.IsAuthenticated;
            }
        }

        protected virtual int CurrentUserId
        {
            get
            {
                return User.GetUserId();
            }
        }

        protected virtual string GetQueryString(string key)
        {
            var query = Request.Path.ToString();
            return query.Length < 1 ? null : query;
        }

        protected virtual IQueryable<TEntity> GetAsNoTracking()
        {
            return _dbContext.Set<TEntity>().AsNoTracking();
        }

        protected virtual async Task<bool> DoesCurrentUserOwnAnyOfAsync(int Id)
        {
            return await GetAsNoTracking()
                    .AnyAsync(e => e.Id == Id && e.UserId == CurrentUserId);
        }
        
        protected virtual void FilterToUserOwned(ref IQueryable<TEntity> entities)
        {
            entities = entities.Where(e => e.UserId == CurrentUserId);
        }

        protected virtual IQueryable<TEntity> FilterToId(int? id, IQueryable<TEntity> source)
        {
            return id == null ? source : source.Where(e => e.Id == id);
        }

        protected virtual IQueryable<TEntity> FilterDeletedFlag(IQueryable<TEntity> source)
        {
            return source.Where(e => !e.IsDeleted);
        }

        protected virtual IQueryable<TEntity> ApplyPagination(TSieveModel sieveModel, IQueryable<TEntity> source)
        {
            return _sieveProcessor.Apply(sieveModel, source,
                applyFiltering: false, applySorting: false);
        }

        protected virtual IQueryable<TEntity> ApplyFilterAndSort(TSieveModel sieveModel, IQueryable<TEntity> source)
        {
            return _sieveProcessor.Apply(sieveModel, source,
                applyPagination: false);
        }

        protected virtual EntityEntry<TEntity> AttachGetEntry(int id, TEntity entity)
        {
            entity.Update(id, User.GetUserId());

            var entry = _dbContext.Attach(entity);

            entry.Property(e => e.Updated).IsModified = true;

            return entry;
        }

        protected virtual void MarkPropertiesModified(EntityEntry<TEntity> entityEntry, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
                entityEntry.Property(propertyName).IsModified = true;
        }

        protected virtual void MarkAllPropertiesModified(EntityEntry<TEntity> entityEntry)
        {
            foreach (var property in entityEntry.Properties)
                property.IsModified = true;
        }

        protected virtual void MarkPropertiesNotModified(EntityEntry<TEntity> entityEntry, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
                entityEntry.Property(propertyName).IsModified = false;
        }

        protected virtual async Task<bool> IsDeletedFlag(int id)
        {
            return await _dbContext.Set<TEntity>().AnyAsync(e => e.Id == id
                                                            && e.IsDeleted);
        }

        protected virtual void SetDeletedFlag(TEntity entity)
        {
            var set = _dbContext.Set<TEntity>();
            entity.IsDeleted = true;
            _dbContext.Set<TEntity>().Attach(entity).Property(e => e.IsDeleted).IsModified = true;
        }

        protected virtual void RemoveWithId(int id)
        {
            _dbContext.Remove(new TEntity() { Id = id });
        }

        #endregion HELPERS
    }

}
