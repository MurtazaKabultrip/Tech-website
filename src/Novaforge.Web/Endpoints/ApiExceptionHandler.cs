using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Novaforge.Web.Services;

namespace Novaforge.Web.Endpoints;

/// <summary>Maps service exceptions to RFC 7807 problem responses for /api requests.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            return false;
        }

        ProblemDetails problem = exception switch
        {
            ValidationFailedException v => new ValidationProblemDetails(v.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            },
            NotFoundException n => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = n.Message },
            UnauthorizedAccessException => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Forbidden" },
            PaymentGatewayException p => new ProblemDetails { Status = StatusCodes.Status502BadGateway, Title = p.Message },
            AiUnavailableException a => new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = a.Message },
            _ => new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "An unexpected error occurred." }
        };

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
