using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using BlazorWebAssemblyApp.Server.Data;
using BlazorWebAssemblyApp.Server.Models;
using Stl.Fusion.EntityFramework;
using Stl.IO;
using BlazorWebAssemblyApp.Server;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stl.Fusion.Operations.Reprocessing;
using Stl.Fusion.Bridge;
using Stl.Fusion;
using Stl.Fusion.Server;
using Stl.Fusion.Server.Controllers;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Stl.Fusion.Server.Authentication;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddIdentityServer()
    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>();

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
        }))
    .Build();

var server = new ServerSettings();
builder.Services.AddSingleton(new Publisher.Options() { Id = server.PublisherId });
var fusion = builder.Services.AddFusion();
var fusionServer = fusion.AddWebServer();
//var fusionClient = fusion.AddRestEaseClient();
var fusionAuth = fusion.AddAuthentication().AddServer(
    signInControllerSettingsFactory: _ => SignInController.DefaultSettings with
    {
        DefaultScheme = MicrosoftAccountDefaults.AuthenticationScheme,
        SignInPropertiesBuilder = (_, properties) =>
        {
            properties.IsPersistent = true;
        }
    },
    serverAuthHelperSettingsFactory: _ => ServerAuthHelper.DefaultSettings with
    {
        NameClaimKeys = Array.Empty<string>(),
    });

builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(
    TransientFailureDetector.New(e => e is DbUpdateConcurrencyException)));

builder.Services.AddDbContext<ShakelyContext>(ServiceLifetime.Transient);
builder.Services.AddDbContext<ApplicationDbContext>(ServiceLifetime.Transient);

//var tmpServices = builder.Services.BuildServiceProvider();

builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ShakelyContext>>().CreateDbContext());

try {
    var appDir = FilePath.GetApplicationDirectory();
    var dbPath = appDir & "Shakely.db";

    builder.Services.AddDbContextFactory<ShakelyContext>(dbContext =>
    {
        dbContext.UseSqlite($"Data Source={dbPath}");

    });
    //builder.Services.AddDbContext<ShakelyContext>(ServiceLifetime.Transient);
    builder.Services.AddDbContextServices<ShakelyContext>(dbContext =>
    {
        dbContext.AddEntityResolver<long, Price>();
        dbContext.AddEntityResolver<long, Price>((_, options) =>
        {
            options.QueryTransformer = prices => prices.Include(c => c.AdjustedClose);
        });
        dbContext.AddFileBasedOperationLogChangeTracking(dbPath + "_changed");
        dbContext.AddAuthentication();
    });
} catch (Exception e) {
    Debug.WriteLine(e.Message);
    Debug.WriteLine(e.InnerException?.Message ?? "");
    throw;
}

builder.Services.AddAuthentication()
    .AddIdentityServerJwt();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.UseMigrationsEndPoint();
    app.UseWebAssemblyDebugging();
} else {
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();


app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();