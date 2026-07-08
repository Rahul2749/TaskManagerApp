using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Middleware
{
    /// <summary>
    /// Catches every unhandled exception from the API pipeline and converts it into a
    /// consistent RFC 7807 <c>application/problem+json</c> response so clients (and the
    /// Blazor ApiService) get predictable error payloads instead of a 500 stack trace.
    /// </summary>
    public sealed class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (status, title) = exception switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
                // Entity-finding helpers throw KeyNotFoundException when a resource is missing.
                KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
                InvalidOperationException => (HttpStatusCode.BadRequest, "Invalid operation"),
                ArgumentException => (HttpStatusCode.BadRequest, "Invalid request"),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
            };

            // Never leak internals on a 500; log them server-side instead.
            if (status == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);
            }
            else
            {
                _logger.LogWarning(exception, "Handled {Status} on {Method} {Path}",
                    (int)status, context.Request.Method, context.Request.Path);
            }

            var problem = new ProblemDetails
            {
                Status = (int)status,
                Title = title,
                Detail = status == HttpStatusCode.InternalServerError
                    ? "An unexpected error occurred. Please try again later."
                    : exception.Message,
                Instance = context.Request.Path
            };

            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/problem+json";

            await JsonSerializer.SerializeAsync(context.Response.Body, problem);
        }
    }
}
