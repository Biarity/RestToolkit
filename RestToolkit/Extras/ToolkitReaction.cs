using RestToolkit.Base;
using System;
using System.Runtime.Serialization;

namespace RestToolkit.Extras
{
    [DataContract]
    public abstract class ToolkitReaction<TUser, TParent, TReactionType> : ToolkitEntity<TUser>
        where TUser : ToolkitUser
        where TParent : ToolkitEntity<TUser>, IReactionParent
        where TReactionType : Enum
    {
        [DataMember]
        public int ParentId { get; set; }
        public TParent Parent { get; set; }

        [DataMember]
        public TReactionType Type { get; set; }
    }

    public interface IReactionParent
    {
        int VoteReactionCounter { get; set; }
    }
}
