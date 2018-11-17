using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace RestToolkit.BaseServices
{
    [DataContract]
    public class ToolkitUser : IdentityUser<int>
    {

    }
}