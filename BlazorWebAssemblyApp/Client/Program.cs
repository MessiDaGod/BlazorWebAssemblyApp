using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorWebAssemblyApp.Client;
using Microsoft.Extensions.DependencyInjection;
using Stl.OS;
using Stl.DependencyInjection;
using Stl.Fusion;
using Stl.Fusion.Client;
using Stl.Fusion.Authentication;
using Stl.Fusion.UI;

namespace BlazorWebAssemblyApp.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (!OSInfo.IsWebAssembly)
            throw new ApplicationException("This app runs only in browser.");

        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddHttpClient("BlazorWebAssemblyApp.ServerAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
            .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

        // Supply HttpClient instances that include access tokens when making requests to the server project
        builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("BlazorWebAssemblyApp.ServerAPI"));
        ConfigureServices(builder.Services, builder);
        builder.Services.AddApiAuthorization();
        var host = builder.Build();
        await host.Services.HostedServices().Start();
        await host.RunAsync();
    }

    public static void ConfigureServices(IServiceCollection services, WebAssemblyHostBuilder builder)
    {
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
        var apiBaseUri = new Uri($"{baseUri}api/");

        // Fusion
        var fusion = services.AddFusion();
        var fusionClient = fusion.AddRestEaseClient(
            (c, o) => {
                o.BaseUri = baseUri;
                o.IsLoggingEnabled = true;
                o.IsMessageLoggingEnabled = false;
            }).ConfigureHttpClientFactory(
            (c, name, o) => {
                var isFusionClient = ( name ?? "" ).StartsWith("Stl.Fusion");
                var clientBaseUri = isFusionClient ? baseUri : apiBaseUri;
                o.HttpClientActions.Add(client => client.BaseAddress = clientBaseUri);
            });
        var fusionAuth = fusion.AddAuthentication().AddRestEaseClient();

        // Fusion services
        fusionClient.AddClientService<IAuthBackend>();
        ConfigureSharedServices(services);
    }

    public static void ConfigureSharedServices(IServiceCollection services)
    {
        // Fusion services
        var fusion = services.AddFusion();
        // Default update delay is 0.1s
        services.AddTransient<IUpdateDelayer>(c => new UpdateDelayer(c.UICommandTracker(), 0.1));
    }

}
