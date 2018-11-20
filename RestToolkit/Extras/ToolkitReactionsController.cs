using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestToolkit.Base;
using Sieve.Models;
using Sieve.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RestToolkit.Extras
{
    public abstract class ToolkitReactionsController<TDbContext, TUser,
        TReaction, TReactionParent, TReactionType>
        : ToolkitController<TReaction, TDbContext, TUser, SieveProcessor, SieveModel, FilterTerm, SortTerm>
        where TDbContext : ToolkitDbContext<TUser>
        where TUser : ToolkitUser
        where TReactionParent : ToolkitEntity<TUser>, IReactionParent, new()
        where TReaction : ToolkitReaction<TUser, TReactionParent, TReactionType>, new()
        where TReactionType : Enum
    {
        public ToolkitReactionsController(TDbContext dbContext, SieveProcessor sieveProcessor, IConfiguration config, ILogger<ToolkitController<TReaction, TDbContext, TUser, SieveProcessor, SieveModel, FilterTerm, SortTerm>> logger) : base(dbContext, sieveProcessor, config, logger)
        {
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public abstract class ToolkitReactionsController<TDbContext, TUser, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm, 
        TReaction, TReactionParent, TReactionType>
        : ToolkitController<TReaction, TDbContext, TUser, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm>
        where TDbContext : ToolkitDbContext<TUser>
        where TUser : ToolkitUser
        where TSieveProcessor : class, ISieveProcessor<TSieveModel, TFilterTerm, TSortTerm>
        where TSieveModel : class, ISieveModel<TFilterTerm, TSortTerm>
        where TFilterTerm : IFilterTerm, new()
        where TSortTerm : ISortTerm, new()
        where TReactionParent : ToolkitEntity<TUser>, IReactionParent, new()
        where TReaction : ToolkitReaction<TUser, TReactionParent, TReactionType>, new()
        where TReactionType : Enum
    {
        private const string ReactionParentVoteReactionCounterPropertyName = "VoteReactionCounter";

        public ToolkitReactionsController(TDbContext dbContext, TSieveProcessor sieveProcessor, IConfiguration config, ILogger<ToolkitController<TReaction, TDbContext, TUser, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm>> logger) : base(dbContext, sieveProcessor, config, logger)
        {
        }

        [HttpPost("")]
        public async Task<IActionResult> Create([FromBody]TReaction reaction)
        {
            CreateAndAdd(reaction);

            if (!await CreateReaction(reaction))
                return Unauthorized();
           // TODO
           // if (IncrementVoteCountOnCreate(reaction))
           //     IncrementVoteCount(reaction.Type, reaction.ParentId, )

            return await SaveChangesAndReturn(reaction);
        }

        [AllowAnonymous]
        [HttpGet("Parent/{parentId}")]
        [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "*" })]
        public async Task<IActionResult> Read(int parentId, [FromQuery]TSieveModel sieveModel)
        {
            var source = GetAsNoTracking();

            source = ApplyFilterAndSort(sieveModel, source);

            source = source
                .Where(r => r.ParentId == parentId
                         && r.Parent.UserId == CurrentUserId);

            source = ApplyPagination(sieveModel, source);

            var result = await source.ToListAsync();

            return Ok(new { data = result });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {

            var reaction = await _dbContext.Set<TReaction>()
                .Select(r => new
                {
                    r.Id,
                    r.Type,
                    r.UserId,
                    r.ParentId,
                    ParentVoteCount = r.Parent.VoteReactionCounter
                })
                .FirstOrDefaultAsync(r => r.Id == id
                                       && r.UserId == CurrentUserId);

            if (reaction == null)
            {
                return Unauthorized();
            }
            else
            {
                RemoveWithId(id);
                // TODO
                // if (DecrementVoteCountOnDelete(new TReaction() { reaction.Id, }))
                // IncrementVoteCount(reaction.Type, reaction.ParentId, reaction.ParentVoteCount, false);
                return await SaveChangesAndReturn();
            }

        }                                                                                                                                                                                                         

        protected abstract Task<bool> CreateReaction(TReaction reaction);

        protected abstract bool IncrementVoteCountOnCreate(TReaction reaction);
        protected abstract bool DecrementVoteCountOnDelete(TReaction reaction);

        private void IncrementVoteCount(TReactionType reactionType, int parentId, int startingVoteCount, bool increment = true)
        {
            //if ((int)(object)reactionType == 0)
            {
                var parent = new TReactionParent()
                { 
                    Id = parentId,
                    VoteReactionCounter = startingVoteCount
                };

                var entry = _dbContext.Attach(parent);
                entry.Property(ReactionParentVoteReactionCounterPropertyName).IsModified = true;

                if (increment)
                    parent.VoteReactionCounter++;
                else
                    parent.VoteReactionCounter--;
            }

        }

    }
}
