using System;
using System.Linq;
using System.Security.Claims;

namespace RestToolkit.Infrastructure
{
    public static class ClaimsPrincipalExtensions
    {
        // string is TKey
        public static string GetUserId(this ClaimsPrincipal user)
        {
            return user.Claims?.FirstOrDefault(c => c.Type == "sub")?.Value;
        }

        public static string GetUserName(this ClaimsPrincipal user)
        {
            return user.Claims?.FirstOrDefault(c => c.Type == "name")?.Value;
        }
    }
}
