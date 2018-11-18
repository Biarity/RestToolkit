using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestToolkit.Base;
using Sieve.Models;
using Sieve.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestToolkit.Extras
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public abstract class CommentsController<TDbContext, TUser, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm,
        TComment, TCommentParent, TReaction, TReactionType> 
        : ToolkitController<TComment, TDbContext, TUser, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm>
        where TDbContext : ToolkitDbContext<TUser>
        where TUser : ToolkitUser
        where TSieveProcessor : class, ISieveProcessor<TSieveModel, TFilterTerm, TSortTerm>
        where TSieveModel : class, ISieveModel<TFilterTerm, TSortTerm>
        where TFilterTerm : IFilterTerm, new()
        where TSortTerm : ISortTerm, new()
        where TCommentParent : ToolkitEntity<TUser>
        where TReaction : Reaction<TUser, TComment, TReactionType>, new()
        where TReactionType : Enum
        where TComment : Comment<TUser, TCommentParent, TComment, TReaction, TReactionType>, new()
    {
        protected const string CommentBodyPropertyName = "Body";
        protected const string CommentLastActivePropertyName = "LastActive";

        //private readonly IHubContext<CommentsHub> _commentsHubContext;

        public CommentsController(TDbContext dbContext, TSieveProcessor sieveProcessor, IConfiguration config, ILogger<ToolkitController<TComment, TDbContext, TUser, TSieveProcessor, TSieveModel, TFilterTerm, TSortTerm>> logger) : base(dbContext, sieveProcessor, config, logger)
        {
        }

        [HttpPost("")]
        public async Task<IActionResult> Create([FromBody]TComment comment)
        {
            CreateAndAdd(comment);
            CreateInitialLoveReaction(comment);

            if (!await CreateCommentOnParent(comment))
                return Unauthorized();

            if (comment.ParentCommentId != null)
            {
                var parentId = await CreateCommentOnComment(comment);
                if (parentId != null)
                    await Bump((int)parentId);
                else
                    return Unauthorized();
            }


            var result = new
            {
                Comment = comment,
                UserReactions = comment.Reactions
            };
            
            // TODO put into method
            //await _commentsHubContext.Clients
            //    .Group(CommentsHub.GetCommentGroupName(parentType, parentId))
            //    .SendAsync(HubMethod.CommentRecieved.ToString(), JsonConvert.SerializeObject(result));

            return await SaveChangesAndReturn(result);
        }

        [AllowAnonymous]
        [HttpGet("Story/{storyId}")]
        [ResponseCache(Duration = 10, VaryByQueryKeys = new[] { "*" })]
        public async Task<IActionResult> Read(int parentId, [FromQuery]TSieveModel sieveModel)
        {
            var source = GetAsNoTracking();

            source = ApplyFilterAndSort(sieveModel, source);
            source = FilterCanAccessComment(source);

            source = source.Where(c => c.ParentId == parentId
                                    && c.ParentCommentId == null);

            var result = await SelectComment(sieveModel, source)
                                    .ToListAsync();

            return Ok(new { data = result });

        }

        [AllowAnonymous]
        [HttpGet("Comment/{commentId}")]
        [ResponseCache(Duration = 10, VaryByQueryKeys = new[] { "*" })]
        public async Task<IActionResult> ReadOfComment(int commentId, [FromQuery]TSieveModel sieveModel)
        {
            var source = GetAsNoTracking();

            source = ApplyFilterAndSort(sieveModel, source);
            source = FilterCanAccessComment(source);

            source = source.Where(c => c.ParentCommentId == commentId);

            var result = await SelectComment(sieveModel, source, true)
                                    .ToListAsync();

            return Ok(new { data = result });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody]TComment comment)
        {
            if (!await DoesCurrentUserOwnAnyOfAsync(id))
                return Unauthorized();

            var entry = AttachGetEntry(id, comment);

            MarkPropertiesModified(entry, CommentBodyPropertyName);

            return await SaveChangesAndReturn();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await DoesCurrentUserOwnAnyOfAsync(id))
                return Unauthorized();

            RemoveWithId(id);

            return await SaveChangesAndReturn();
        }

        protected virtual async Task Bump(int commentId, int? parentCommentId = null)
        {
            var comment = new TComment { Id = commentId };

            _dbContext.Attach(comment).Property(CommentLastActivePropertyName).IsModified = true;
            comment.LastActive = DateTimeOffset.Now;

            if (parentCommentId != null)
            {
                await Bump((int)parentCommentId);
            }
            else
            {
                var parentCommentIdDb = (await _dbContext.Set<TComment>()
                            .Select(c => new { c.Id, c.ParentCommentId })
                            .FirstOrDefaultAsync(c => c.Id == commentId)).ParentCommentId;

                if (parentCommentIdDb != null)
                {
                    await Bump((int)parentCommentIdDb);
                }
            }
        }

        protected abstract Task<bool> CreateCommentOnParent(TComment comment);

        /// Returns parent comment id if ok else null
        protected abstract Task<int?> CreateCommentOnComment(TComment comment);
        
        protected abstract IQueryable<TComment> FilterCanAccessComment(IQueryable<TComment> source);

        protected virtual void CreateInitialLoveReaction(TComment comment)
        {
            var initialLove = new TReaction();

            initialLove.Create(CurrentUserId);

            comment.Reactions = new List<TReaction>
            {
                initialLove
            };
        }

        protected virtual IQueryable<object> SelectComment(TSieveModel sieveModel, IQueryable<TComment> source, bool excludeChildComments = false)
        {
            var currentUserId = IsUserAuthenticated ? CurrentUserId : -1;

            var result = source
            .Select(c => new
            {
                Comment = c,
                c.User.UserName,
                UserReactions = c.Reactions.Where(r => r.UserId == currentUserId).Select(r => new { r.Id, r.Type }),
                ChildComments = excludeChildComments ? null : c.ChildComments
                    .OrderByDescending(cc => cc.Created)
                    .Select(cc => new
                    {
                        Comment = cc,
                        cc.User.UserName,
                        UserReactions = cc.Reactions.Where(r => r.UserId == currentUserId).Select(r => new { r.Id, r.Type }),
                    }).Take(5)
            });

            return _sieveProcessor.Apply(sieveModel, result, applyFiltering: false, applySorting: false);
        }
    }
}
