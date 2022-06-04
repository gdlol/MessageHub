using System.Text.Json;
using MessageHub.DependencyInjection;
using MessageHub.HomeServer.P2p;
using MessageHub.HomeServer.P2p.Libp2p;
using Microsoft.AspNetCore.Mvc;

[assembly: ApiController]

string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.json"));
var config = JsonSerializer.Deserialize<Config>(json)!;
if (string.IsNullOrEmpty(config.ContentPath))
{
    config.ContentPath = Path.Combine(AppContext.BaseDirectory, "Content");
}
if (string.IsNullOrEmpty(config.DataPath))
{
    config.DataPath = Path.Combine(AppContext.BaseDirectory, "Data");
}
Directory.CreateDirectory(config.ContentPath);
string url = $"http://{config.ListenAddress}";
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = config.ContentPath
});
builder.Services.AddSingleton(config);
builder.WebHost.UseUrls(url);
builder.WebHost.ConfigureLogging(builder =>
{
    builder.AddFilter("Default", LogLevel.Information);
    builder.AddFilter(nameof(MessageHub), LogLevel.Debug);
    builder.AddFilter("Microsoft", LogLevel.Warning);
    builder.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
});
builder.Services.AddCors();
builder.Services.AddFasterKV(config.DataPath);
builder.Services.AddLibp2p(
    new HostConfig
    {
        AdvertisePrivateAddresses = true
    },
    new DHTConfig
    {
        BootstrapPeers = Array.Empty<string>()
    });
builder.Services.AddP2pHomeServer();
builder.Services.AddMatrixAuthentication();
builder.Services.AddControllers();

var app = builder.Build();
app.UseCors(builder =>
{
    builder
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader();
});
app.Map("/.well-known/matrix/client", () => new Dictionary<string, object>
{
    ["m.homeserver"] = new { base_url = url },
    ["m.identity_server"] = new { base_url = url }
});
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/_matrix/media"))
    {
        context.Response.Headers.Add(
            "Content-Security-Policy",
            "sandbox; default-src 'none'; script-src 'none'; plugin-types application/pdf; "
            + "style-src 'unsafe-inline'; object-src 'self';");
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
