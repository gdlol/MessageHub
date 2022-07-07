using System.Diagnostics;
using System.Net;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms.Builder;

namespace MessageHub.Complement.ReverseProxy;

public sealed class HomeServerHttpForwarder : IDisposable
{
    private readonly IHttpForwarder forwarder;
    private readonly ITransformBuilder transformBuilder;
    private readonly HttpMessageInvoker client;

    public HomeServerHttpForwarder(IHttpForwarder forwarder, ITransformBuilder transformBuilder)
    {
        ArgumentNullException.ThrowIfNull(forwarder);
        ArgumentNullException.ThrowIfNull(transformBuilder);

        this.forwarder = forwarder;
        this.transformBuilder = transformBuilder;
        client = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current)
        });
    }

    public void Dispose()
    {
        client.Dispose();
    }

    public HttpTransformer CreateTransform(Action<TransformBuilderContext> action)
    {
        return transformBuilder.Create(action);
    }

    public async Task SendAsync(
        HttpContext context,
        string homeServerAddress,
        Action<TransformBuilderContext>? transform = null)
    {
        var transformer = HttpTransformer.Default;
        if (transform is not null)
        {
            transformer = transformBuilder.Create(transform);
        }
        await forwarder.SendAsync(
            context,
            $"http://{homeServerAddress}",
            client,
            ForwarderRequestConfig.Empty,
            transformer);
    }
}
