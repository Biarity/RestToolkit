﻿using RestToolkit.Base;
using Sieve.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;

namespace RestToolkit.Extras
{
    [DataContract]
    public abstract class ToolkitComment<TUser, TParent, TSelf, TReaction, TReactionType> : ToolkitEntity<TUser>, IReactionParent
        where TUser : ToolkitUser
        where TParent : ToolkitEntity<TUser>
        where TSelf : ToolkitComment<TUser, TParent, TSelf, TReaction, TReactionType>
        where TReaction : ToolkitReaction<TUser, TSelf, TReactionType>
        where TReactionType : Enum
    {
        [DataMember(IsRequired = true)]
        public int ParentId { get; set; }
        public TParent Parent { get; set; }

        [DataMember(IsRequired = true), MinLength(5), MaxLength(1000)]
        public string Body { get; set; }

        [DataMember]
        public int? ParentCommentId { get; set; }
        public TSelf ParentComment { get; set; }
        public List<TSelf> ChildComments { get; set; }

        [DataMember, Sieve(CanSort = true)]
        public virtual DateTimeOffset LastActive { get; set; }

        [DataMember]
        public int VoteReactionCounter { get; set; }

        public List<TReaction> Reactions { get; set; }

        public override void Create(int userId = 0)
        {
            base.Create(userId);
            VoteReactionCounter = 1;
        }
    }
}
