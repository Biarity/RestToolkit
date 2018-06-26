using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestToolkit.Infrastructure;
using Sieve.Models;
using Sieve.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RestToolkit.Services
{
    [ApiController]
    [ProducesResponseType(403), ProducesResponseType(500)]
    public abstract class ToolkitController<TEntity, TRepository, TDbContext, TUser, TKey> : ControllerBase
        where TEntity : ToolkitEntity<TUser, TKey>, new()
        where TRepository : ToolkitRepository<TEntity, TDbContext, TUser, TKey>
        where TDbContext : DbContext
        where TUser : IdentityUser<TKey>
        where TKey : IEquatable<TKey>
    {
        protected TDbContext _dbContext;
        protected SieveProcessor _sieveProcessor;
        protected TRepository _entityRepo;

        protected bool _saveChangesOnRead;
        protected bool _allowSieveOnRead;

        public ToolkitController(
            TDbContext dbContext,
            SieveProcessor sieveProcessor,
            TRepository entityRepo,
            bool saveChangesOnRead = false,
            bool allowSieveOnRead = false)
        {
            _dbContext = dbContext;
            _sieveProcessor = sieveProcessor;
            _entityRepo = entityRepo;

            _saveChangesOnRead = saveChangesOnRead;
            _allowSieveOnRead = allowSieveOnRead;
        }

        #region POST

        [HttpPost]
        [ProducesResponseType(200), ProducesResponseType(410)]
        public virtual async Task<IActionResult> Create([FromBody]TEntity entity)
        {
            entity.Created = DateTimeOffset.UtcNow;
            entity.UserId = (TKey)Convert.ChangeType(User.GetUserId(), typeof(TKey));
            entity.InitCreate();
            entity.Normalise();

            if (await _entityRepo.OnCreateAsync(entity))
            {
                try
                {
                    _dbContext.Add(entity);
                    await _dbContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    return StatusCode(410);
                }
                catch (DbUpdateException)
                {
                    throw; // server error
                }

                return Ok(entity);
            }
            else
            {
                return Forbid();
            }
        }

        #endregion

        #region GET

        [HttpGet("{id}")]
        [ProducesResponseType(200)]
        public virtual async Task<IActionResult> Read(int id)
        {
            return await Read(id, null);
        }

        [HttpGet]
        [ProducesResponseType(200)]
        public virtual async Task<IActionResult> Read([FromQuery]SieveModel sieveModel)
        {
            return await Read(null, sieveModel);
        }

        private async Task<IActionResult> Read(int? id, [FromQuery]SieveModel sieveModel)
        {
            var source = _dbContext.Set<TEntity>().AsNoTracking();

            //source = source.Where(e => !e.IsDeleted);

            if (id != null)
            {
                source = source.Where(e => e.Id == id);
            }
            else if (_allowSieveOnRead && sieveModel != null)
            {
                source = _sieveProcessor.Apply(sieveModel, source);
            }

            var result = await _entityRepo.OnReadAsync(source, id);

            if (result is null)
            {
                return Forbid();
            }
            else if (result is IQueryable<object> entities)
            {
                result = id == null ? new { data = await entities.ToListAsync() }
                                    : await entities.FirstOrDefaultAsync();
            }

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

        #endregion

        #region PUT

        [HttpPut("{id}")]
        [ProducesResponseType(204), ProducesResponseType(410)]
        public virtual async Task<IActionResult> Update(int id, [FromBody]TEntity entity)
        {
            entity.Normalise();
            entity.Id = id;

            if (! await _entityRepo.OnUpdateAsync(entity))
            {
                return Forbid();
            }

            try
            {
                _dbContext.Update(entity);
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(410);
            }
            catch (DbUpdateException)
            {
                throw; // server error
            }

            return NoContent();
        }

        #endregion

        #region DELETE

        [HttpDelete("{id}")]
        [ProducesResponseType(204), ProducesResponseType(410)]
        public virtual async Task<IActionResult> Delete(int id)
        {
            var entity = new TEntity() { Id = id };

            if (! await _entityRepo.OnDeleteAsync(id))
            {
                return Forbid();
            }

            try
            {
                _dbContext.Remove(entity);
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(410);
            }
            catch (DbUpdateException)
            {
                throw; // server error
            }

            return NoContent();
        }

        #endregion

    }

}
