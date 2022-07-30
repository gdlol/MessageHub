using MessageHub.HomeServer;

namespace MessageHub.DependencyInjection;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder MonitorDevices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<DeviceMonitorMiddleware>();
    }
}
