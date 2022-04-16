using System.Text.Json;
using System.Text.Json.Nodes;
using MessageHub.DependencyInjection;
using Microsoft.AspNetCore.Mvc;

[assembly: ApiController]

var builder = WebApplication.CreateBuilder(args);
string url = "http://localhost:8448";
builder.WebHost.UseUrls(url);
builder.Services.AddCors();
builder.Services.AddDummyHomeServer();
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
app.MapControllers().RequireAuthorization();
app.Run();
