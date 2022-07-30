using MessageHub.Complement.HomeServer;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Routing.Template;
using Yarp.ReverseProxy.Transforms;

namespace MessageHub.Complement.ReverseProxy;

public class UserProfileProxy : IMiddleware
{
    private readonly TemplateBinderFactory templateBinderFactory;
    private readonly HomeServerHttpForwarder forwarder;
    private readonly IUserRegistration userRegistration;

    public UserProfileProxy(
        TemplateBinderFactory templateBinderFactory,
        HomeServerHttpForwarder forwarder,
        IUserRegistration userRegistration)
    {
        ArgumentNullException.ThrowIfNull(templateBinderFactory);
        ArgumentNullException.ThrowIfNull(forwarder);
        ArgumentNullException.ThrowIfNull(userRegistration);

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
        string? userId = context.GetRouteValue(nameof(userId))?.ToString();
        if (userId is not null
            && UserIdentifier.TryParse(userId, out var userIdentifier)
            && (await userRegistration.TryGetAddressAsync(userIdentifier.UserName)) is string serverAddress
            && (await userRegistration.TryGetP2pUserIdAsync(userIdentifier.UserName)) is string p2pUserId)
        {
            string newPath = context.UpdateRoute(templateBinderFactory, nameof(userId), p2pUserId);
            await forwarder.SendAsync(context, serverAddress, context =>
            {
                context.AddRequestTransform(context =>
                {
                    context.Path = newPath;
                    return ValueTask.CompletedTask;
                });
            });
        }
        else
        {
            await next(context);
        }
    }
}

public class UserProfileProxyPipeline
{
    public void Configure(IApplicationBuilder app) => app.UseMiddleware<UserProfileProxy>();
}
