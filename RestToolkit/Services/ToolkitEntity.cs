﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Sieve.Attributes;
using System;
using System.Runtime.Serialization;

namespace RestToolkit.Services
{
    [DataContract]
    public abstract class ToolkitEntity<TUser>
        where TUser : IdentityUser<int>
    {
        [DataMember]
        public virtual int Id { get; set; }
        
        [DataMember, Sieve(CanSort = true)]
        public virtual DateTimeOffset Created { get; set; }

        [DataMember, Sieve(CanSort = true)]
        public virtual DateTimeOffset LastUpdated { get; set; }

        [DataMember, Sieve(CanFilter = true)]
        public virtual int UserId { get; set; }
        public virtual TUser User { get; set; }
        
        public virtual bool IsDeleted { get; set; }

        public virtual void Normalise() { }

        public virtual void Create(int userId = 0)
        {
            Id = 0;
            Created = DateTimeOffset.UtcNow;
            LastUpdated = DateTimeOffset.UtcNow;
            UserId = userId;
            IsDeleted = false;
        }
        
        public virtual void Update(int userId = 0)
        {
            LastUpdated = DateTimeOffset.UtcNow;
            UserId = userId;
        }
    }

}
