using MessageHub.ClientServer.Protocol;
using MessageHub.Complement.HomeServer;
using MessageHub.HomeServer.P2p.Providers;
using Microsoft.AspNetCore.Routing.Template;
using Yarp.ReverseProxy.Transforms;

namespace MessageHub.Complement.ReverseProxy;

public class ContentRepositoryProxy : IMiddleware
{
    private const string contentTypeStoreName = "ContentTypes";

    private readonly ILogger logger;
    private readonly IStorageProvider storageProvider;
    private readonly TemplateBinderFactory templateBinderFactory;
    private readonly HomeServerHttpForwarder forwarder;
    private readonly IUserRegistration userRegistration;

    public ContentRepositoryProxy(
        ILogger<ContentRepositoryProxy> logger,
        IStorageProvider storageProvider,
        TemplateBinderFactory templateBinderFactory,
        HomeServerHttpForwarder forwarder,
        IUserRegistration userRegistration)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(templateBinderFactory);
        ArgumentNullException.ThrowIfNull(forwarder);
        ArgumentNullException.ThrowIfNull(userRegistration);

        this.logger = logger;
        this.storageProvider = storageProvider;
        this.templateBinderFactory = templateBinderFactory;
        this.forwarder = forwarder;
        this.userRegistration = userRegistration;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetEndpoint() is not RouteEndpoint routeEndpoint)
        {
            throw new InvalidOperationException();
        }
        string? serverName = context.GetRouteValue(nameof(serverName))?.ToString();
        string? mediaId = context.GetRouteValue(nameof(mediaId))?.ToString();
        if (serverName is not null && mediaId is not null) // download
        {
            var host = serverName.Split('.', 2);
            string userName = host[0];
            string p2pServerName = host[1];
            string? serverAddress = await userRegistration.TryGetAddressAsync(userName);
            serverAddress = serverAddress ?? throw new InvalidOperationException();

            string newPath = context.UpdateRoute(templateBinderFactory, nameof(serverName), p2pServerName);
            await forwarder.SendAsync(context, serverAddress, context =>
            {
                context.AddRequestTransform(context =>
                {
                    context.Path = newPath;
                    return ValueTask.CompletedTask;
                });
                context.AddResponseTransform(async context =>
                {
                    using var store = storageProvider.GetKeyValueStore(contentTypeStoreName);
                    string? contentType = await store.GetStringAsync($"mxc://{serverName}/{mediaId}");
                    if (contentType is not null)
                    {
                        context.HttpContext.Response.ContentType = contentType;
                    }
                });
            });
        }
        else
        {
            if (context.User.Identity?.Name is string userName)
            {
                string? serverAddress = await userRegistration.TryGetAddressAsync(userName);
                serverAddress = serverAddress ?? throw new InvalidOperationException();
                if (context.Request.Path.ToString().EndsWith("/upload"))
                {
                    string? contentType = context.Request.ContentType;
                    await forwarder.SendAsync(context, serverAddress, context =>
                    {
                        context.AddResponseTransform(async context =>
                        {
                            if (context.ProxyResponse?.IsSuccessStatusCode == true)
                            {
                                var response = await context.ProxyResponse.Content.ReadFromJsonAsync<UploadResponse>();
                                response = response ?? throw new InvalidOperationException();
                                context.SuppressResponseBody = true;

                                // Rewrite response.
                                logger.LogDebug("Rewriting content URL: {}", response.ContentUrl);
                                var uri = new Uri(response.ContentUrl);
                                var builder = new UriBuilder
                                {
                                    Scheme = uri.Scheme,
                                    Host = $"{userName}.{uri.Host}",
                                    Path = uri.AbsolutePath
                                };
                                response.ContentUrl = builder.ToString();
                                logger.LogDebug("Rewritten content URL: {}", response.ContentUrl);

                                // Save content type.
                                if (contentType is not null)
                                {
                                    using var store = storageProvider.GetKeyValueStore(contentTypeStoreName);
                                    await store.PutStringAsync(response.ContentUrl, contentType);
                                    await store.CommitAsync();
                                }

                                context.HttpContext.Response.ContentLength = null;
                                await context.HttpContext.Response.WriteAsJsonAsync(response);
                            }
                        });
                    });
                }
                else
                {
                    await forwarder.SendAsync(context, serverAddress);
                }
            }
            else
            {
                await next(context);
            }
        }
    }
}

public class ContentRepositoryProxyPipeline
{
    public void Configure(IApplicationBuilder app) => app.UseMiddleware<ContentRepositoryProxy>();
}
