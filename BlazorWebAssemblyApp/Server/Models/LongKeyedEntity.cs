using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Stl;
using System;

namespace BlazorWebAssemblyApp.Server.Models
{
    public record LongKeyedEntity : IHasId<long>
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; init; }
    }

}

