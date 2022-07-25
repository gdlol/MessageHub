using MessageHub.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer.Protocol;

public static class ApplicationResults
{
    private class JsonResult<T> : IActionResult
    {
        private readonly T value;
        private readonly int statusCode;

        public JsonResult(T value, int statusCode)
        {
            this.value = value;
            this.statusCode = statusCode;
        }

        // Set content type without charset;
        public Task ExecuteResultAsync(ActionContext context)
        {
            var resonse = context.HttpContext.Response;
            resonse.ContentType = "application/json";
            resonse.StatusCode = statusCode;
            return DefaultJsonSerializer.SerializeAsync(resonse.Body, value);
        }
    }

    public static IActionResult Json<T>(T value, int statusCode = StatusCodes.Status200OK)
    {
        return new JsonResult<T>(value, statusCode);
    }
}
