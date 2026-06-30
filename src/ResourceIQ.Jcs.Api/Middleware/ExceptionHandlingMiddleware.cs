using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Api.Middleware;

/// <summary>
/// Translates domain/application exceptions to HTTP status codes. Never leaks stack traces or
/// sensitive content; logs at warning without secrets or full copy content .
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ForbiddenException ex)        { await Write(context, StatusCodes.Status403Forbidden, ex.Message); }
        catch (NotFoundException ex)         { await Write(context, StatusCodes.Status404NotFound, ex.Message); }
        catch (DomainException ex)           { await Write(context, StatusCodes.Status409Conflict, ex.Message); }
        catch (NotSupportedException ex)     { await Write(context, StatusCodes.Status501NotImplemented, ex.Message); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error on {Path}", context.Request.Path);
            await Write(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static Task Write(HttpContext ctx, int status, string message)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { error = message, status });
    }
}
