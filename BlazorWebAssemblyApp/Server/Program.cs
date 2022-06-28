using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BlazorWebAssemblyApp.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using BlazorWebAssemblyApp.Server.Data;
using BlazorWebAssemblyApp.Server.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCaching();

var host = Host.CreateDefaultBuilder()
    .ConfigureHostConfiguration(cfg => {
        // Looks like there is no better way to set _default_ URL
        cfg.Sources.Insert(0, new MemoryConfigurationSource()
        {
            InitialData = new Dictionary<string, string>() {
                {WebHostDefaults.ServerUrlsKey, "https://localhost:7230"},
            }
        });
    })
    .ConfigureWebHostDefaults(webHost => webHost
        .UseDefaultServiceProvider((ctx, options) => {
            options.ValidateScopes = ctx.HostingEnvironment.IsDevelopment();
            options.ValidateOnBuild = false;
        })
        .UseStartup<Startup>())
    .Build();

// Ensure the DB is created
var dbContextFactory = host.Services.GetRequiredService<IDbContextFactory<ShakelyContext>>();
var dbContextFactory2 = host.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
await using var dbContext = dbContextFactory.CreateDbContext();
await using var dbContext2 = dbContextFactory2.CreateDbContext();
// await dbContext.Database.EnsureDeletedAsync();
await dbContext.Database.EnsureCreatedAsync();
await dbContext2.Database.EnsureCreatedAsync();

await dbContext.DisposeAsync();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddIdentityServer()
    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>();

var app = builder.Build();

app.UseResponseCaching();

// app.MapBlazorHub();

app.Use(async (context, next) =>
{
    context.Response.GetTypedHeaders().CacheControl =
        new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(10)
        };
    context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
        new string[] { "Accept-Encoding" };

    await next();
});

await host.RunAsync();