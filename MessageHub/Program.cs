using System.Text.Json;
using MessageHub.DependencyInjection;
using MessageHub.HomeServer.P2p;
using Microsoft.AspNetCore.Mvc;

[assembly: ApiController]

var builder = WebApplication.CreateBuilder(args);
string json = File.ReadAllText("config.json");
var config = JsonSerializer.Deserialize<Config>(json)!;
if (string.IsNullOrEmpty(config.ContentPath))
{
    config.ContentPath = Path.Combine(Directory.GetCurrentDirectory(), "Content");
}
if (string.IsNullOrEmpty(config.DataPath))
{
    config.DataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
}
Directory.CreateDirectory(config.ContentPath);
builder.Services.AddSingleton(config);
string url = $"http://{config.Peers[config.PeerId]}";
builder.WebHost.UseUrls(url);
builder.WebHost.ConfigureLogging(builder =>
{
    builder.AddFilter("Default", LogLevel.Information);
    builder.AddFilter("Microsoft", LogLevel.Warning);
    builder.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
});
builder.Services.AddCors();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
