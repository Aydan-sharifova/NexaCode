using Coding.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coding.Api.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        this.problemDetailsService = problemDetailsService;
        this.logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            ValidationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled request exception");
        else
            logger.LogInformation(exception, "Request rejected with status {StatusCode}", statusCode);

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : exception.Message
            }
        });
    }
}
