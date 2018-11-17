using Microsoft.AspNetCore.Identity;
using System.Runtime.Serialization;

namespace RestToolkit.Base
{
    [DataContract]
    public class ToolkitUser : IdentityUser<int>
    {

    }
}