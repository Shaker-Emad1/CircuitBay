using CircuitBay.Application.Common;
using CircuitBay.Contracts.Common;

namespace CircuitBay.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            var correlationId = CorrelationIdMiddleware.GetCurrentId(context);
            var status = ex switch
            {
                ValidationAppException => StatusCodes.Status400BadRequest,
                UnauthorizedAppException => StatusCodes.Status401Unauthorized,
                NotFoundAppException => StatusCodes.Status404NotFound,
                ConflictAppException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ApiError(ex.Code, ex.Message) { CorrelationId = correlationId });
        }
        catch (Exception ex)
        {
            var correlationId = CorrelationIdMiddleware.GetCurrentId(context);
            logger.LogError(ex, "Unhandled failure {CorrelationId} for {Method} {Path}",
                correlationId, context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ApiError("server_error", "The request could not be completed.") { CorrelationId = correlationId });
        }
    }
}
