using RestToolkit.Base;
using System;
using System.Runtime.Serialization;

namespace RestToolkit.Extras
{
    [DataContract]
    public class Reaction<TUser, TParent, TReactionType> : ToolkitEntity<TUser>
        where TUser : ToolkitUser
        where TParent : ToolkitEntity<TUser>
        where TReactionType : Enum
    {
        [DataMember]
        public int ParentId { get; set; }
        public TParent Comment { get; set; }

        [DataMember]
        public TReactionType Type { get; set; }
    }
}
