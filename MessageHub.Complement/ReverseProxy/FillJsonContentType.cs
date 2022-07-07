namespace MessageHub.Complement.ReverseProxy;

public class FillJsonContentType : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Request.Headers.TryAdd("Content-Type", "application/json");
        return next(context);
    }
}

public class FillJsonContentTypePipeline
{
    public void Configure(IApplicationBuilder app) => app.UseMiddleware<FillJsonContentType>();
}
