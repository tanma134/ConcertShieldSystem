using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EventAPI.Services;
public class GovernanceMiddleware(RequestDelegate next, ILogger<GovernanceMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client/Gateway đã đóng kết nối: không ghi JSON vào stream đã hủy.
            logger.LogWarning("Caller/Gateway aborted request at {Path}; trace={Trace}", context.Request.Path, context.TraceIdentifier);
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "Request failed at {Path}", context.Request.Path);
            var code = ex switch
            {
                GovernanceConflictException or DbUpdateConcurrencyException => 409,
                DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => 409,
                KeyNotFoundException => 404,
                UnauthorizedAccessException => 403,
                ArgumentException or InvalidOperationException => 400,
                AssetUploadException { ErrorCode: "StorageTimeout" } or OperationCanceledException => 504,
                HttpRequestException or AssetUploadException => 503,
                _ => 500
            };
            context.Response.StatusCode = code;
            await context.Response.WriteAsJsonAsync(new { success = false, message = ex is AssetUploadException || code < 500 ? ex.Message : code == 504 ? "Request timed out. Please retry." : "Service unavailable. Please retry.", errorCode = (ex as AssetUploadException)?.ErrorCode, traceId = context.TraceIdentifier });
        }
    }
}
