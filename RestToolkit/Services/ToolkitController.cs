using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestToolkit.Infrastructure;
using RestToolkit.Models;
using Sieve.Exceptions;
using Sieve.Models;
using Sieve.Services;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace RestToolkit.Services
{
    public abstract class ToolkitController<TEntity, TRepository, TDbContext, TUser>
        : ToolkitController<TEntity, TRepository, TDbContext, TUser, ExtraQueries, SieveProcessor, SieveModel, FilterTerm, SortTerm>
        where TEntity : ToolkitEntity<TUser>, new()
        where TRepository : ToolkitRepository<TEntity, TDbContext, TUser, ExtraQueries>
        where TDbContext : DbContext
        where TUser : IdentityUser<int>
    {
        public ToolkitController(TDbContext dbContext, SieveProcessor sieveProcessor, TRepository entityRepo, ILogger<ToolkitController<TEntity, TRepository, TDbContext, TUser, ExtraQueries, SieveProcessor, SieveModel, FilterTerm, SortTerm>> logger, bool allowSieveOnRead = true, bool useDeletionFlags = false, bool filterDeletedWhenUsingDeletionFlags = true) : base(dbContext, sieveProcessor, entityRepo, logger, allowSieveOnRead, useDeletionFlags, filterDeletedWhenUsingDeletionFlags)
        {
        }
    }

    [ApiController]
    [ProducesResponseType(StatusCodes.Status400BadRequest), ProducesResponseType(StatusCodes.Status405MethodNotAllowed), ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public abstract class ToolkitController<TEntity, TRepository, TDbContext, TUser, TExtraQueries, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm> : ControllerBase
        where TEntity : ToolkitEntity<TUser>, new()
        where TRepository : ToolkitRepository<TEntity, TDbContext, TUser, TExtraQueries>
        where TDbContext : DbContext
        where TUser : IdentityUser<int>
        where TExtraQueries : class
        where TSieveProcessor : class, ISieveProcessor<TSieveModel, TFilterTerm, TSortTerm>
        where TSieveModel : class, ISieveModel<TFilterTerm, TSortTerm>
        where TFilterTerm : IFilterTerm, new()
        where TSortTerm : ISortTerm, new()
    {
        protected TDbContext _dbContext;
        protected TSieveProcessor _sieveProcessor;
        protected TRepository _entityRepo;
        private readonly ILogger _logger;
        
        protected bool _allowSieveOnRead;
        protected bool _useDeletionFlags;
        protected bool _filterDeletedWhenUsingDeletionFlags;

        public ToolkitController(
            TDbContext dbContext,
            TSieveProcessor sieveProcessor,
            TRepository entityRepo,
            ILogger<ToolkitController<TEntity, TRepository, TDbContext, TUser, TExtraQueries, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm>> logger,
            bool allowSieveOnRead = true,
            bool useDeletionFlags = false,
            bool filterDeletedWhenUsingDeletionFlags = true)
        {
            _dbContext = dbContext;
            _sieveProcessor = sieveProcessor;
            _entityRepo = entityRepo;
            _logger = logger;
            
            _allowSieveOnRead = allowSieveOnRead;
            _useDeletionFlags = useDeletionFlags;
            _filterDeletedWhenUsingDeletionFlags = filterDeletedWhenUsingDeletionFlags;
        }

        #region POST

        [Authorize]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK), ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Create([FromBody]TEntity entity)
        {
            entity.Create(User.GetUserId());
            entity.Normalise();

            _dbContext.Add(entity);

            var repoResp = await _entityRepo.OnCreateAsync(entity);
            if (repoResp.Success)
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
                
                return Ok(entity);
            }
            else
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed, repoResp.ErrorMessage);
            }
        }

        #endregion

        #region GET

        [HttpGet("{id}")]
        [ResponseCache(Duration = 30)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public virtual async Task<IActionResult> Read(int id)
        {
            return await Read(id, null);
        }

        [HttpGet]
        [ResponseCache(Duration = 30)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public virtual async Task<IActionResult> Read([FromQuery]TSieveModel sieveModel)
        {
            return await Read(null, sieveModel);
        }

        protected virtual async Task<IActionResult> Read(int? id, TSieveModel sieveModel, TExtraQueries extraQueries = null)
        {
            var source = _dbContext.Set<TEntity>().AsNoTracking();

            var applySieve = (id == null) 
                && _allowSieveOnRead 
                && (sieveModel != null);

            if (_useDeletionFlags && _filterDeletedWhenUsingDeletionFlags)
                source = source.Where(e => !e.IsDeleted);

            if (id != null)
                source = source.Where(e => e.Id == id);

            if (applySieve)
                try
                {
                    source = _sieveProcessor.Apply(sieveModel, source, 
                        applyPagination: false);
                }
                catch (SieveException)
                {
                    return BadRequest("Filter/sort error.");
                }

            var (repoResp, repoResult) = await _entityRepo.OnReadAsync(source, id, extraQueries);

            if (!repoResp.Success || repoResult == null)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed, repoResp.ErrorMessage);
            }
            else
            {
                if (applySieve)
                    try
                    {
                        repoResult = _sieveProcessor.Apply(sieveModel, repoResult, 
                            applyFiltering: false, applySorting: false);
                    }
                    catch (SieveException)
                    {
                        return BadRequest("Pagination error.");
                    }

                var result = id == null ? new { data = await repoResult.ToListAsync() }
                                        : await repoResult.FirstOrDefaultAsync();

                return Ok(result);
            }

        }

        #endregion

        #region PUT

        [Authorize]
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent), ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Update(int id, [FromBody]TEntity entity)
        {
            // Note this implementation only does one db call (if not using deletion flags)
            // This means that the model bound (`entity`) will overrride the one in the db
            // So unbound strings will become null, unbound bools false, etc.
            // This means the client has to send all properties not just modified ones
            // A different approach would be to use Microsoft.AspNetCore.JsonPatch
            // Where the entity is read from the db, the patch supplied form the client
            //  is applied to it, then the entity saved (2 db calls)

            entity.Id = id;
            entity.Update(User.GetUserId());
            entity.Normalise();

            _dbContext.Attach(entity);
            var entry = _dbContext.Entry(entity);
            entry.Property(nameof(ToolkitEntity<TUser>.LastUpdated)).IsModified = true;

            var repoResp = await _entityRepo.OnUpdateAsync(id, entry);

            if (!repoResp.Success)
                return StatusCode(StatusCodes.Status405MethodNotAllowed, repoResp.ErrorMessage);

            try
            {
                if (_useDeletionFlags
                    && await _dbContext.Set<TEntity>()
                                    .AnyAsync(e => e.Id == id
                                                && e.IsDeleted))
                    return StatusCode(StatusCodes.Status410Gone);

                //_dbContext.Update(entity);
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

            return NoContent();
        }

        #endregion

        #region DELETE

        [Authorize]
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent), ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Delete(int id)
        {
            var entity = new TEntity() { Id = id };

            var repoResp = await _entityRepo.OnDeleteAsync(id);

            if (!repoResp.Success)
                return StatusCode(StatusCodes.Status405MethodNotAllowed, repoResp.ErrorMessage);

            try
            {
                if (_useDeletionFlags)
                {
                    var set = _dbContext.Set<TEntity>();
                    
                    if (await set.AnyAsync(e => e.Id == id && e.IsDeleted))
                        return StatusCode(StatusCodes.Status410Gone);

                    entity.IsDeleted = true;
                    _dbContext.Set<TEntity>().Attach(entity).Property(x => x.IsDeleted).IsModified = true;
                }
                else
                {
                    _dbContext.Remove(entity);
                }

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

            return NoContent();
        }

        #endregion

    }

}
