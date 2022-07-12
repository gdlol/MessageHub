using Microsoft.AspNetCore.Routing.Template;

namespace MessageHub.Complement.ReverseProxy;

internal static class HttpContextExtensioncs
{
    public static string UpdateRoute(
        this HttpContext context,
        TemplateBinderFactory templateBinderFactory,
        string key,
        string value)
    {
        if (context.GetEndpoint() is not RouteEndpoint routeEndpoint)
        {
            throw new InvalidOperationException();
        }
        var routeData = context.GetRouteData().Values;
        var newRouteData = new RouteValueDictionary();
        foreach (var item in routeData)
        {
            if (item.Key == key)
            {
                newRouteData[key] = value;
            }
            else
            {
                newRouteData[item.Key] = item.Value;
            }
        }
        var binder = templateBinderFactory.Create(routeEndpoint.RoutePattern);
        string? newPath = binder.BindValues(newRouteData);
        newPath = newPath ?? throw new InvalidOperationException();
        return newPath;
    }
}
