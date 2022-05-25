using System.Text.Json;
using MessageHub;
using MessageHub.DependencyInjection;
using Microsoft.AspNetCore.Mvc;

[assembly: ApiController]

var builder = WebApplication.CreateBuilder(args);
string json = File.ReadAllText("config.json");
var config = JsonSerializer.Deserialize<Config>(json)!;
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
app.MapControllers();
// app.Run();
_ = app.RunAsync();
await Task.Delay(1000);

Console.WriteLine("Creating host...");
using var serverHost = MessageHub.HomeServer.P2p.Libp2p.Host.Create(new MessageHub.HomeServer.P2p.Libp2p.HostConfig
{
    AdvertisePrivateAddresses = true,
    PrivateNetworkSecret = "test",
});
Console.WriteLine($"ServerHost ID: {serverHost.Id}");
Console.WriteLine($"ServerHost Addresses: {JsonSerializer.Deserialize<JsonElement>(serverHost.GetHostAddressInfo())}");
using var host = MessageHub.HomeServer.P2p.Libp2p.Host.Create(new MessageHub.HomeServer.P2p.Libp2p.HostConfig
{
    AdvertisePrivateAddresses = true,
    PrivateNetworkSecret = "test"
});
Console.WriteLine($"host ID: {host.Id}");
Console.WriteLine($"ServerHost Addresses: {JsonSerializer.Deserialize<JsonElement>(serverHost.GetHostAddressInfo())}");
Console.WriteLine("Creating DHT...");
using var serverDHT = MessageHub.HomeServer.P2p.Libp2p.DHT.Create(serverHost, new MessageHub.HomeServer.P2p.Libp2p.DHTConfig
{
    FilterPrivateAddresses = false
});
using var dht = MessageHub.HomeServer.P2p.Libp2p.DHT.Create(host, new MessageHub.HomeServer.P2p.Libp2p.DHTConfig
{
    FilterPrivateAddresses = false
});
Console.WriteLine("Bootstrapping DHT...");
serverDHT.Bootstrap();
dht.Bootstrap();
Console.WriteLine("Connecting...");
host.Connect(serverHost.GetHostAddressInfo());
Console.WriteLine("Connected.");
// await Task.Delay(5000);
var discovery = MessageHub.HomeServer.P2p.Libp2p.Discovery.Create(dht);
Console.WriteLine("Advertising...");
discovery.Advertise("Test");
Console.WriteLine("Finding peers...");
var result = discovery.FindPeers("Test");
Console.WriteLine(JsonSerializer.Serialize(result));

serverHost.StartProxyRequests("127.0.0.1:8448");
await Task.Delay(1000);
var response = host.SendRequest(serverHost.Id, new MessageHub.Federation.Protocol.SignedRequest
{
    Method = HttpMethod.Get.ToString(),
    Uri = $"/_matrix/key/v2/server",
    Origin = "dummy",
    OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    Destination = serverHost.Id,
    Signatures = JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["dummy"] = new Dictionary<string, string>
        {
            ["dummy"] = "dummy"
        }
    })
});
if (response.IsSuccessStatusCode)
{
    var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
    Console.WriteLine(responseBody);
}
else
{
    string content = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"{response.StatusCode}: {content}");
}

response = host.SendRequest(serverHost.Id, new MessageHub.Federation.Protocol.SignedRequest
{
    Method = HttpMethod.Get.ToString(),
    Uri = $"/_matrix/federation/v3/query/profile?field=displayname&user_id=dummy",
    Origin = "dummy",
    OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    Destination = serverHost.Id,
    Signatures = JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["dummy"] = new Dictionary<string, string>
        {
            ["dummy"] = "dummy"
        }
    })
});
if (response.IsSuccessStatusCode)
{
    var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
    Console.WriteLine(responseBody);
}
else
{
    string content = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"{response.StatusCode}: {content}");
}

// using var memberStore = new MessageHub.HomeServer.P2p.Libp2p.MemberStore();
// memberStore.AddMember("Test", host.Id);
// using var pubsub = MessageHub.HomeServer.P2p.Libp2p.PubSub.Create(dht, memberStore);
// using var topic = pubsub.JoinTopic("Test");
// _ = Task.Run(() =>
// {
//     using var subscription = topic.Subscribe();
//     while (true)
//     {
//         var (senderId, message) = subscription.Next();
//         Console.WriteLine($"{senderId}: {message}");
//     }
// });
// while (true)
// {
//     await Task.Delay(3000);
//     topic.Publish(JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }));
// }

