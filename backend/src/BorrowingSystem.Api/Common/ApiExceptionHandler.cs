using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BorrowingSystem.Api.Common;

/// <summary>
/// Translates exceptions into RFC 7807 problem responses so controllers stay free of try/catch.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Not allowed"),
            ConflictException => (StatusCodes.Status409Conflict, "Schedule conflict"),
            DomainException => (StatusCodes.Status400BadRequest, "Request could not be completed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Never leak internals on a 500.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Something went wrong on our side. Please try again."
                    : exception.Message
            }
        });
    }
}
