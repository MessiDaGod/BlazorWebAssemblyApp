using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Stl.DependencyInjection;
using Stl.Fusion;
using Stl.Fusion.Authentication;
using Stl.Fusion.Blazor;
using Stl.Fusion.Bridge;
using Stl.Fusion.Client;
using Stl.Fusion.EntityFramework;
using Stl.Fusion.Server;
using Stl.IO;
using Stl.Fusion.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stl.Fusion.Operations.Reprocessing;
using Stl.Fusion.Server.Controllers;
using Stl.Fusion.Server.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using BlazorWebAssemblyApp.Server.Data;
using BlazorWebAssemblyApp.Server.Models;

namespace BlazorWebAssemblyApp.Server;

public class Startup
{
    private IConfiguration Cfg { get; }
    private IWebHostEnvironment Env { get; }
    private ServerSettings ServerSettings { get; set; } = null!;
    private ILogger Log { get; set; } = NullLogger<Startup>.Instance;

    public Startup(IConfiguration cfg, IWebHostEnvironment environment)
    {
        Cfg = cfg;
        Env = environment;

    }

    /// <summary>
    /// Configures the input and output formatters.
    /// </summary>
    private static void ConfigureFormatters(IMvcBuilder mvcBuilder)
    {
        // Adds the XML input and output formatter using the DataContractSerializer.
        mvcBuilder.AddXmlDataContractSerializerFormatters();
        // $End-XmlFormatter-DataContractSerializer$
        // $Start-XmlFormatter-XmlSerializer$

        // Adds the XML input and output formatter using the XmlSerializer.
        mvcBuilder.AddXmlSerializerFormatters();
        // $End-XmlFormatter-XmlSerializer$
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Cookies
        services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
        });

        // Logging
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Error);
            if (Env.IsDevelopment()) {
                logging.AddFilter("Microsoft", LogLevel.Error);
                logging.AddFilter("Microsoft.AspNetCore.Hosting", LogLevel.Error);
                logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Error);
                logging.AddFilter("Stl.Fusion.Operations", LogLevel.Error);
            }
        });

        services.AddCors(policy =>
        {
            policy.AddPolicy("CorsPolicy", opt => opt
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
        });

        // Creating Log and ServerSettings as early as possible
        services.AddSettings<ServerSettings>("Server");
#pragma warning disable ASP0000
        var tmpServices = services.BuildServiceProvider();
        Log = tmpServices.GetRequiredService<ILogger<Startup>>();
        ServerSettings = tmpServices.GetRequiredService<ServerSettings>();

        // DbContext & related services

        try {
            var appDir = FilePath.GetApplicationDirectory();
            var dbPath = appDir & "Shakely.db";
            services.AddDbContextFactory<ShakelyContext>(dbContext =>
            {
                dbContext.UseSqlite($"Data Source={dbPath}");
                if (Env.IsDevelopment())
                    dbContext.EnableSensitiveDataLogging();

            });
            services.AddDbContext<ShakelyContext>(ServiceLifetime.Transient);
            services.AddDbContextServices<ShakelyContext>(dbContext =>
            {
                dbContext.AddEntityResolver<long, Price>();
                dbContext.AddEntityResolver<long, Price>((_, options) =>
                {
                    options.QueryTransformer = prices => prices.Include(c => c.AdjustedClose);
                });

                dbContext.AddOperations((_, o) =>
                {
                    // We use FileBasedDbOperationLogChangeMonitor, so unconditional wake up period
                    // can be arbitrary long - all depends on the reliability of Notifier-Monitor chain.
                    o.UnconditionalWakeUpPeriod = TimeSpan.FromSeconds(Env.IsDevelopment() ? 60 : 5);
                });
                dbContext.AddFileBasedOperationLogChangeTracking(dbPath + "_changed");
                dbContext.AddAuthentication();
            });
        } catch (Exception) {
            throw;
        }

        services.AddSingleton(new Publisher.Options() { Id = ServerSettings.PublisherId });
        var fusion = services.AddFusion();
        var fusionServer = fusion.AddWebServer();
        var fusionClient = fusion.AddRestEaseClient();
        var fusionAuth = fusion.AddAuthentication().AddServer(
            signInControllerSettingsFactory: _ => SignInController.DefaultSettings with
            {
                DefaultScheme = MicrosoftAccountDefaults.AuthenticationScheme,
                SignInPropertiesBuilder = (_, properties) => {
                    properties.IsPersistent = true;
                }
            },
            serverAuthHelperSettingsFactory: _ => ServerAuthHelper.DefaultSettings with
            {
                NameClaimKeys = Array.Empty<string>(),
            });

        services.AddSingleton(new PresenceService.Options() { UpdatePeriod = TimeSpan.FromMinutes(1) });
        services.AddControllers();

        fusion.AddSandboxedKeyValueStore();
        fusion.AddOperationReprocessor();
        // You don't need to manually add TransientFailureDetector -
        // it's here only to show that operation reprocessor works
        // when TodoService.AddOrUpdate throws this exception.
        // Database-related transient errors are auto-detected by
        // DbOperationScopeProvider<TDbContext> (it uses DbContext's
        // IExecutionStrategy to do this).
        services.TryAddEnumerable(ServiceDescriptor.Singleton(
            TransientFailureDetector.New(e => e is DbUpdateConcurrencyException)));
        // services.AddScoped<Stl.Fusion.Extensions.IKeyValueStore>();

        // Shared UI services
        BlazorWebAssemblyApp.Client.Program.ConfigureSharedServices(services);
        services.AddLocalization();
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");

        // Data protection
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ShakelyContext>>().CreateDbContext());
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
        // ASP.NET Core authentication providers
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        }).AddCookie(options => {
            options.LoginPath = "/signIn";
            options.LogoutPath = "/signOut";
            if (Env.IsDevelopment())
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            // This controls the expiration time stored in the cookie itself
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
            // And this controls when the browser forgets the cookie
            options.Events.OnSigningIn = ctx => {
                ctx.CookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(28);
                return Task.CompletedTask;
            };
        }).AddMicrosoftAccount(options =>
        {
            options.ClientId = ServerSettings.MicrosoftAccountClientId;
            options.ClientSecret = ServerSettings.MicrosoftAccountClientSecret;
            // That's for personal account authentication flow
            options.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
            options.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        });

        // Web
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });
        services.AddRouting();
        services.AddRazorPages();
        services.AddMvc().AddApplicationPart(Assembly.GetExecutingAssembly()).AddXmlSerializerFormatters();
        services.AddServerSideBlazor(o => o.DetailedErrors = true);
        fusionAuth.AddBlazor(o => { }); // Must follow services.AddServerSideBlazor()!

    }

    public void Configure(IApplicationBuilder app, ILogger<Startup> log)
    {
        if (ServerSettings.AssumeHttps) {
            Log.LogInformation("AssumeHttps on");
            app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next();
            });
        }

        // This server serves static content from Blazor Client,
        // and since we don't copy it to local wwwroot,
        // we need to find Client's wwwroot in bin/(Debug/Release) folder
        // and set it as this server's content root.
        var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        var binCfgPart = Regex.Match(baseDir, @"[\\/]bin[\\/]\w+[\\/]").Value;
        var wwwRootPath = Path.Combine(baseDir, "wwwroot");
        if (!Directory.Exists(Path.Combine(wwwRootPath, "_framework")))
            // This is a regular build, not a build produced w/ "publish",
            // so we remap wwwroot to the client's wwwroot folder
            wwwRootPath = Path.GetFullPath(Path.Combine(baseDir, $"../../../../Client/{binCfgPart}/net6.0/wwwroot"));
        Env.WebRootPath = wwwRootPath;
        Env.WebRootFileProvider = new PhysicalFileProvider(Env.WebRootPath);
        StaticWebAssetsLoader.UseStaticWebAssets(Env, Cfg);

        if (Env.IsDevelopment()) {
            app.UseDeveloperExceptionPage();
            app.UseWebAssemblyDebugging();
        } else {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }
        app.UseHttpsRedirection();
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto
        });

        app.UseWebSockets(new WebSocketOptions()
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30),
        });
        app.UseFusionSession();
        app.UseCors("CorsPolicy");

        // Static + Swagger
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        // API controllers
        app.UseRouting();

        app.UseCookiePolicy();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBlazorHub();
            endpoints.MapFusionWebSocketServer();
            endpoints.MapControllers();
            endpoints.MapFallbackToPage("index.html");
        });
    }
}
