using MessageHub.Complement.HomeServer;

namespace MessageHub.Complement.ReverseProxy;

public class P2pServerProxy : IMiddleware
{
    private readonly ILogger logger;
    private readonly HomeServerHttpForwarder forwarder;
    private readonly IUserRegistration userRegistration;

    public P2pServerProxy(
        ILogger<P2pServerProxy> logger,
        HomeServerHttpForwarder forwarder,
        IUserRegistration userRegistration)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(forwarder);
        ArgumentNullException.ThrowIfNull(userRegistration);

        this.logger = logger;
        this.forwarder = forwarder;
        this.userRegistration = userRegistration;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.Name is string userName)
        {
            string? serverAddress = await userRegistration.TryGetAddressAsync(userName);
            if (serverAddress is null)
            {
                logger.LogError("Server address not found for {}", userName);
                throw new InvalidOperationException();
            }
            await forwarder.SendAsync(context, serverAddress);
        }
        else
        {
            logger.LogError("Request is not authenticated.");
            throw new InvalidOperationException();
        }
    }
}

public class P2pServerProxyPipeline
{
    public void Configure(IApplicationBuilder app) => app.UseMiddleware<P2pServerProxy>();
}
