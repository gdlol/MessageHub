using System.Globalization;
using System.Text.Json;

namespace MessageHub.ElementServer;

using Node = Dictionary<string, object>;

public class Program
{
    public static Task RunAsync(string applicationPath, Config config, CancellationToken cancellationToken = default)
    {
        var elementConfig = new
        {
            default_server_config = new Node
            {
                ["m.homeserver"] = new
                {
                    base_url = $"http://{config.ListenAddress}"
                }
            },
            disable_custom_urls = true,
            disable_guests = true,
            disable_3pid_login = true,
            brand = nameof(MessageHub),
            default_country_code = RegionInfo.CurrentRegion.TwoLetterISORegionName
        };

        string elementPath = Path.Combine(applicationPath, "Clients", "Element");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = elementPath
        });
        builder.WebHost.UseUrls($"http://{config.ElementListenAddress}");
        builder.WebHost.ConfigureLogging(builder => builder.ClearProviders());
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
            context.Response.Headers.Add("Content-Security-Policy", "frame-ancestors 'none'");
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block;");
            context.Response.Headers.Add("Cache-Control", "no-cache");
            await next();
        });
        app.MapGet("/config.json", () =>
        {
            return Results.Json(elementConfig);
        });
        app.UseDefaultFiles();
        app.UseStaticFiles();
        return app.RunAsync(cancellationToken);
    }

    public static async Task Main()
    {
        string applicationPath = AppContext.BaseDirectory;
        string json = File.ReadAllText(Path.Combine(applicationPath, "config.json"));
        var config = JsonSerializer.Deserialize<Config>(json)!;
        if (config.ElementListenAddress is null)
        {
            Console.WriteLine("Listen address is null, exiting.");
            return;
        }
        await RunAsync(applicationPath, config);
    }
}
