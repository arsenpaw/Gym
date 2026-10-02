using FitnessClub.Application.Common;
using FitnessClub.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace FitnessClub.Api.ErrorHandling;

public sealed class ExceptionToProblemDetailsHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ExceptionToProblemDetailsHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            DomainException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        });
    }
}
