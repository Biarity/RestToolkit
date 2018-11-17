using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace RestToolkit.Base
{
    public class ToolkitHub : Hub
    {
        protected readonly IDistributedCache _cache;
        protected readonly ILogger<ToolkitHub> _logger;

        protected string hubName;

        public ToolkitHub(IDistributedCache cache, ILogger<ToolkitHub> logger)
        {
            _cache = cache;
            _logger = logger;
            hubName = GetType().Name;
        }

        /*
        public override Task OnConnectedAsync()
        {
            _logger.LogInformation($"Hub named '{hubName}' connected.");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation($"Hub named '{hubName}' disconnected.");
            return base.OnDisconnectedAsync(exception);
        }

        // Relates: UserId + HubName + CacheKey.UserConnectionsAndGroups
        // To     : ConnectionId:GroupName
        protected async Task AddToGroupAndRemember(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await AssociateConnectionAndGroupToUser(groupName);
        }

        protected async Task RemoveFromGroupAndRemember(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await AssociateConnectionAndGroupToUser(groupName, false);
        }

        // Kicks user connections form all groups in in this 
        // hub that were joined using AddToGroupAndRemember
        protected async Task KickUserFromAllGroups()
        {
            var connectionGroups = await GetUserConnectionsAndGroups();

            if (connectionGroups != null)
                foreach (var cg in connectionGroups)
                    await Groups.RemoveFromGroupAsync(cg.Item1, cg.Item2);

            var key = GetUserCacheKey(hubName, CacheKey.UserConnectionsAndGroups);

            await _cache.RemoveAsync(key);
        }

        // Number of user connections in groups joined using AddToGroupAndRemember
        protected async Task<int> GetUserGroupCount()
        {
            var key = GetUserCacheKey(hubName, CacheKey.UserConnectionsAndGroups);

            var connectionGroups = await _cache.GetStringAsync(key) ?? "";

            return connectionGroups.Split(';', StringSplitOptions.RemoveEmptyEntries).Count();
        }

        protected async Task<IEnumerable<Tuple<string, string>>> GetUserConnectionsAndGroups()
        {
            var key = GetUserCacheKey(hubName, CacheKey.UserConnectionsAndGroups);

            var connectionGroups = await _cache.GetStringAsync(key) ?? "";

            return connectionGroups == ""
                ? null
                : connectionGroups.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(cg => cg.Split(':', StringSplitOptions.RemoveEmptyEntries))
                .Select(cg => new Tuple<string, string>(cg.ElementAtOrDefault(0), cg.ElementAtOrDefault(1)));
        }

        private async Task AssociateConnectionAndGroupToUser(string groupName, bool associate = true)
        {
            var key = GetUserCacheKey(hubName, CacheKey.UserConnectionsAndGroups);

            var connectionGroups = await _cache.GetStringAsync(key) ?? "";

            var value = $"{Context.ConnectionId}:{groupName}";

            if (associate)
            {
                if (!connectionGroups.Contains(value))
                {
                    value = $"{connectionGroups};{value}";
                    await _cache.SetStringAsync(key, value);
                }
            }
            else
            {
                value = connectionGroups.Replace($";{value}", "");

                if (String.IsNullOrWhiteSpace(value))
                {
                    await _cache.RemoveAsync(key);
                }
                else
                {
                    await _cache.SetStringAsync(key, value);
                }
            }
        }

        private string GetUserCacheKey(string hubName, CacheKey cacheKey)
        {
            return $"{hubName}+{cacheKey}+{Context.User.GetUserId()}";
        }

        /*
        protected bool ExceededMaxGroupCount(HubName hubName, int max = 5)
        {
            var key = GetUserCacheKey(hubName, CacheKey.GroupCounter);

            _cache.TryGetValue(key, out int count);

            return count >= max; // Max n groups per user
        }

        protected void IncrementGroupCounter(HubName hubName, bool increment = true)
        {
            var key = GetUserCacheKey(hubName, CacheKey.GroupCounter);

            _cache.TryGetValue(key, out int count);

            if (increment)
            {
                _cache.Set(key, count++);
            }
            else
            {
                _cache.Set(key, count--);
            }
        }
        */
   
    }
}
