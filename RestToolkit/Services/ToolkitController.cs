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
        public ToolkitController(TDbContext dbContext, SieveProcessor sieveProcessor, TRepository entityRepo, ILogger<ToolkitController<TEntity, TRepository, TDbContext, TUser, ExtraQueries, SieveProcessor, SieveModel, FilterTerm, SortTerm>> logger, bool saveChangesOnRead = false, bool allowSieveOnRead = true) : base(dbContext, sieveProcessor, entityRepo, logger, saveChangesOnRead, allowSieveOnRead)
        {
        }
    }

    [ApiController]
    [ProducesResponseType(StatusCodes.Status200OK), ProducesResponseType(StatusCodes.Status400BadRequest), ProducesResponseType(StatusCodes.Status405MethodNotAllowed), ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        protected bool _saveChangesOnRead;
        protected bool _allowSieveOnRead;

        public ToolkitController(
            TDbContext dbContext,
            TSieveProcessor sieveProcessor,
            TRepository entityRepo,
            ILogger<ToolkitController<TEntity, TRepository, TDbContext, TUser, TExtraQueries, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm>> logger,
            bool saveChangesOnRead = false,
            bool allowSieveOnRead = true)
        {
            _dbContext = dbContext;
            _sieveProcessor = sieveProcessor;
            _entityRepo = entityRepo;
            _logger = logger;

            _saveChangesOnRead = saveChangesOnRead;
            _allowSieveOnRead = allowSieveOnRead;
        }

        #region POST

        [Authorize]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Create([FromBody]TEntity entity)
        {
            entity.Create(User.GetUserId());
            entity.Normalise();

            var repoResp = await _entityRepo.OnCreateAsync(entity);
            if (repoResp.Success)
            {
                try
                {
                    _dbContext.Add(entity);
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
                    throw; // server error
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
        public virtual async Task<IActionResult> Read(int id)
        {
            return await Read(id, null);
        }

        [HttpGet]
        public virtual async Task<IActionResult> Read([FromQuery]TSieveModel sieveModel)
        {
            return await Read(null, sieveModel);
        }

        protected virtual async Task<IActionResult> Read(int? id, [FromBody]TSieveModel sieveModel, TExtraQueries extraQueries = null)
        {
            var source = _dbContext.Set<TEntity>().AsNoTracking();
            var applySieve = id == null && _allowSieveOnRead && sieveModel != null;

            //source = source.Where(e => !e.IsDeleted);

            if (id != null)
            {
                source = source.Where(e => e.Id == id);
            }

            if (applySieve)
            {
                try
                {
                    source = _sieveProcessor.Apply(sieveModel, source, 
                        applyPagination: false);
                }
                catch (SieveException)
                {
                    return BadRequest("Filter/sort error.");
                }
            }

            var (repoResp, repoResult) = await _entityRepo.OnReadAsync(source, id, extraQueries);

            if (!repoResp.Success || repoResult is null)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed, repoResp.ErrorMessage);
            }
            else
            {
                if (applySieve)
                {
                    try
                    {
                        source = _sieveProcessor.Apply(sieveModel, source, 
                            applyFiltering: false, applySorting: false);
                    }
                    catch (SieveException)
                    {
                        return BadRequest("Pagination error.");
                    }
                }

                var result = id == null ? new { data = await repoResult.ToListAsync() }
                                        : await repoResult.FirstOrDefaultAsync();

                if (_saveChangesOnRead)
                {
                    try
                    {
                        await _dbContext.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        throw; // server error
                    }
                }

                return Ok(result);
            }

        }

        #endregion

        #region PUT

        [Authorize]
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Update(int id, [FromBody]TEntity entity)
        {
            entity.Id = id;
            entity.Update(User.GetUserId());
            entity.Normalise();

            var repoResp = await _entityRepo.OnUpdateAsync(id, entity);

            if (!repoResp.Success)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed, repoResp.ErrorMessage);
            }

            try
            {
                _dbContext.Update(entity);
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
                throw; // server error
            }

            return Ok(entity);
        }

        #endregion

        #region DELETE

        [Authorize]
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        public virtual async Task<IActionResult> Delete(int id)
        {
            var entity = new TEntity() { Id = id };

            var repoResp = await _entityRepo.OnDeleteAsync(id);

            if (!repoResp.Success)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed, repoResp.ErrorMessage);
            }

            try
            {
                _dbContext.Remove(entity);
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
                throw; // server error
            }

            return Ok();
        }

        #endregion

    }

}
