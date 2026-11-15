using Humanitarian.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Middleware;

/// <summary>Maps domain/application exceptions to consistent RFC 7807 ProblemDetails responses.</summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (404, "Not found"),
                ConflictException => (409, "Conflict"),
                ForbiddenException => (403, "Forbidden"),
                AppValidationException => (400, "Validation failed"),
                AuthenticationFailedException => (401, "Authentication failed"),
                OperationCanceledException => (499, "Request cancelled"),
                _ => (500, "Unexpected error")
            };
            if (status == 500) log.LogError(ex, "Unhandled exception for {Path}", ctx.Request.Path);

            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                // Never leak internals for 500s
                Detail = status == 500 ? "An internal error occurred." : ex.Message,
                Instance = ctx.Request.Path
            });
        }
    }
}
