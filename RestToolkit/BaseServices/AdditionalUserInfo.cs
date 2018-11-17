using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace RestToolkit.BaseServices
{
    public interface IAdditionalUserInfo<TUser>
        where TUser : ToolkitUser
    {
        void Map(ref TUser user);
    }
}
