// joeshakely
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebAssemblyApp.Server.Models
{
    [Table("Prices")]
    [Index(nameof(Symbol))]
    [Index(nameof(Date))]
    public record Price : LongKeyedEntity
    {
        public string? UserId { get; set; }
        public DateTime CreatedAt { get { return DateTime.UtcNow; } set { value = DateTime.UtcNow; } }
        public DateTime Date { get; set; }
        public double? Open { get; set; }
        public double? High { get; set; }
        public double? Low { get; set; }
        public double? Close { get; set; }
        public double AdjustedClose { get; set; }
        public double? Volume { get; set; }
        public double? CurrentHoldings { get; set; }
        public string? Symbol { get; set; }
        public double? Pct_Change { get; set; }

        public override string ToString() => $@"""Date"": ""{Date}"", ""Open"": ""{Open}"", ""High"": ""{High}"", ""Low"": ""{Low}"", ""Close"": ""{Close}"", ""AdjustedClose"": ""{AdjustedClose}"", ""Volume"": ""{Volume}"", ""Pct_Change"": ""{Pct_Change}""" + "\n";
    }

}

