// joeshakely
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Stl.Fusion.EntityFramework.Authentication;
using Stl.Fusion.EntityFramework.Operations;
using Microsoft.Data.Sqlite;
using Stl.Fusion.EntityFramework;
using Stl.Fusion.EntityFramework.Extensions;
using Stl.IO;
using System.IO;
using BlazorWebAssemblyApp.Server.Models;

namespace BlazorWebAssemblyApp.Server.Data
{
    public partial class ShakelyContext : DbContextBase
    {
        public ShakelyContext() { }
        public ShakelyContext(DbContextOptions options) : base(options) { }
        // public ShakelyContext() { }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var appDir = FilePath.GetApplicationDirectory();
            var files = Directory.EnumerateFiles(appDir, "Shakely*.db");

            var dbPath = Path.GetFileName(files.FirstOrDefault() ?? "Shakely.db");
            if (!optionsBuilder.IsConfigured) {
                var builder = new SqliteConnectionStringBuilder()
                {
                    DataSource = dbPath,
                    Cache = SqliteCacheMode.Private,

                };

                optionsBuilder.UseSqlite(builder.ToString());
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Price>()
                .HasIndex(l => l.Symbol);

            base.OnModelCreating(builder);
        }

        public DbSet<Price> Prices { get; protected set; } = null!;
    }

}

