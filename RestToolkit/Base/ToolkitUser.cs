using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace RestToolkit.Base
{
    [DataContract]
    public abstract class ToolkitUser : IdentityUser<int>
    {

    }

    //public interface IToolkitAdditionalUserInfo<TUser>
    //    where TUser : ToolkitUser
    //{
    //    string UserName { get; set; }
    //    void Map(ref TUser user);
    //}
    
    //public class ToolkitAdditionalUserInfo<TUser> : IToolkitAdditionalUserInfo<TUser>
    //    where TUser : ToolkitUser
    //{
    //    [Required]
    //    [MinLength(3), MaxLength(10)]
    //    [RegularExpression("[A-Za-z0-9]+")]
    //    [PersonalData]
    //    public string UserName { get; set; }

    //    public void Map(ref TUser user)
    //    {
    //        user.UserName = UserName;
    //    }
    //}
}