using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Sieve.Attributes;
using System;

namespace RestToolkit.Services
{
    public abstract class ToolkitEntity<TUser>
        where TUser : IdentityUser
    {
        [BindNever]
        public virtual int Id { get; set; }

        [Sieve(CanSort = true)]
        [BindNever]
        public virtual DateTimeOffset Created { get; set; }

        [Sieve(CanSort = true)]
        [BindNever]
        public virtual DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

        [BindNever]
        public virtual int UserId { get; set; }

        [BindNever, JsonIgnore]
        public virtual TUser User { get; set; }

        // TODO: IsDeleted flag, would have implications on
        //       ToolkitController logic to ignore entity
        //[BindNever, JsonIgnore]
        //public bool IsDeleted { get; set; }


        public virtual void Normalise() { }
        public virtual void InitCreate(dynamic info) { }
    }

}
