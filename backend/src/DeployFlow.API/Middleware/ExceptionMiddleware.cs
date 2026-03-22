using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DeployFlow.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await HandleExceptionAsync(ctx, ex, _env.IsDevelopment());
        }
    }

    private static async Task HandleExceptionAsync(HttpContext ctx, Exception ex, bool isDevelopment)
    {
        (HttpStatusCode status, string message) result = ex switch
        {
            ValidationException ve => (HttpStatusCode.UnprocessableEntity,
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Access denied."),
            KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            _ => (HttpStatusCode.InternalServerError, isDevelopment
                ? $"{ex.GetType().Name}: {ex.Message}"
                : "An unexpected error occurred.")
        };

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = (int)result.status;

        object response = isDevelopment && result.status == HttpStatusCode.InternalServerError
            ? new
            {
                status = (int)result.status,
                error = result.message,
                detail = ex.ToString(),
                traceId = ctx.TraceIdentifier
            }
            : new
            {
                status = (int)result.status,
                error = result.message,
                traceId = ctx.TraceIdentifier
            };

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionMiddleware>();
}
