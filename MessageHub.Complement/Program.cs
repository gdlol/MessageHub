using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using MessageHub.Complement;
using MessageHub.Complement.DependencyInjection;
using MessageHub.Complement.Logging;
using MessageHub.DependencyInjection;
using Microsoft.AspNetCore.Mvc;

[assembly: ApiController]

using var consoleLoggerProvider = ConsoleLoggerProviderFactory.Create();
using var complementProvider = new PrefixedLoggerProvider(consoleLoggerProvider, $"{nameof(MessageHub.Complement)}: ");

var secret = new byte[32];
RandomNumberGenerator.Fill(secret);
var config = new Config
{
    ServerName = Environment.GetEnvironmentVariable("SERVER_NAME") ?? nameof(MessageHub).ToLower(),
    SelfUrl = "http://127.0.0.1:8008",
    DataPath = "/root/data",
    PrivateNetworkSecret = Convert.ToHexString(secret),
    ConsoleLoggerProvider = consoleLoggerProvider
};
Directory.CreateDirectory(config.DataPath);

using var caCertificate = X509Certificate2.CreateFromPemFile("/complement/ca/ca.crt", "/complement/ca/ca.key");
using var httpsCertificate = PKI.CreateCertificate(caCertificate, nameof(MessageHub));
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls(new[]
{
    "http://0.0.0.0:8008",
    "https://0.0.0.0:8448"
});
builder.WebHost.ConfigureLogging(builder =>
{
    builder.ClearProviders();
    builder.AddProvider(complementProvider);
    builder.AddFilter("Microsoft", LogLevel.Warning);
    builder.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
    builder.AddFilter("MessageHub.Complement", LogLevel.Debug);
    builder.AddFilter("MessageHub.Complement.Authentication", LogLevel.Information);
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(options =>
    {
        options.ServerCertificate = httpsCertificate;
    });
});
builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager => manager.ApplicationParts.Clear())
    .AddApplicationPart(Assembly.GetExecutingAssembly())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddSingleton(config);
builder.Services.AddFasterKV(config.DataPath);
builder.Services.AddComplementHomeServer();

var app = builder.Build();
app.Use((context, next) =>
{
    context.Request.Headers.TryAdd("Content-Type", "application/json");
    return next();
});
app.MapControllers();
app.Run();
