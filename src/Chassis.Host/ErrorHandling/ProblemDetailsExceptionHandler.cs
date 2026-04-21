using System;
using System.Threading;
using System.Threading.Tasks;
using Chassis.Host.Observability;
using Chassis.Host.Tenancy;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Chassis.Host.ErrorHandling;

/// <summary>
/// Exception handler that maps known exception types to RFC 7807 ProblemDetails responses.
/// Registered via <c>app.UseExceptionHandler</c> using the built-in
/// <see cref="IExceptionHandler"/> interface (ASP.NET Core 8+).
/// </summary>
/// <remarks>
/// Exception-to-status mapping:
/// <list type="table">
///   <listheader><term>Exception</term><description>HTTP status / code</description></listheader>
///   <item><term><see cref="MissingTenantException"/></term><description>401 missing_tenant_claim</description></item>
///   <item><term><see cref="ValidationException"/> (FluentValidation)</term><description>400 validation_failed</description></item>
///   <item><term><see cref="UnauthorizedAccessException"/></term><description>403 forbidden</description></item>
///   <item><term><see cref="KeyNotFoundException"/></term><description>404 not_found</description></item>
///   <item><term>Everything else</term><description>500 internal_error — details redacted in all environments</description></item>
/// </list>
/// Stack traces are NEVER included in the response body.
/// </remarks>
internal sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;

    public ProblemDetailsExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ProblemDetailsExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // RLS denial: PostgresException with SQLSTATE 42501 (insufficient_privilege).
        // Increment the counter before building the response so the metric is always recorded
        // even if response writing subsequently fails.
        if (exception is PostgresException { SqlState: "42501" } rlsEx)
        {
            // The table name is in the Npgsql exception detail; not always present — use "unknown"
            // as a safe fallback so the counter always has a value for both tags.
            string tableName = rlsEx.TableName ?? "unknown";

            // Module tag: derive from the request path segment (e.g. /api/v1/ledger/... → "ledger").
            // Fall back to "unknown" when path is absent or non-standard.
            string moduleName = ResolveModuleFromPath(httpContext.Request.Path);

            System.Diagnostics.TagList rlsTags = new System.Diagnostics.TagList
            {
                { "module", moduleName },
                { "table", tableName },
            };
            ChassisMeters.RlsDenials.Add(1, rlsTags);

            _logger.LogWarning(
                "RLS denial (42501): table={Table} module={Module} path={Path}",
                tableName,
                moduleName,
                httpContext.Request.Path);
        }

        ProblemDetails problem = exception switch
        {
            MissingTenantException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Missing tenant identity",
                Detail = exception.Message,
                Extensions = { ["code"] = "missing_tenant_claim" },
            },

            ValidationException ve => BuildValidationProblem(ve),

            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "You do not have permission to perform this action.",
                Extensions = { ["code"] = "forbidden" },
            },

            KeyNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = "The requested resource was not found.",
                Extensions = { ["code"] = "not_found" },
            },

            _ => BuildInternalErrorProblem(exception),
        };

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        }).ConfigureAwait(false);
    }

    private static string ResolveModuleFromPath(PathString path)
    {
        // Extract module from /api/v{n}/{module}/... e.g. /api/v1/ledger/accounts → "ledger"
        ReadOnlySpan<char> span = path.Value.AsSpan();

        // Strip leading slash.
        if (span.StartsWith("/", StringComparison.Ordinal))
        {
            span = span.Slice(1);
        }

        // Skip "api" segment.
        int first = span.IndexOf('/');
        if (first < 0)
        {
            return "unknown";
        }

        span = span.Slice(first + 1);

        // Skip version segment "v{n}".
        int second = span.IndexOf('/');
        if (second < 0)
        {
            return "unknown";
        }

        span = span.Slice(second + 1);

        // The module name is the next path segment.
        int third = span.IndexOf('/');
        string module = third >= 0
            ? span.Slice(0, third).ToString()
            : span.ToString();

        return string.IsNullOrEmpty(module) ? "unknown" : module;
    }

    private static ProblemDetails BuildValidationProblem(ValidationException ve)
    {
        var errors = new System.Collections.Generic.List<object>();
        foreach (FluentValidation.Results.ValidationFailure failure in ve.Errors)
        {
            errors.Add(new { field = failure.PropertyName, message = failure.ErrorMessage });
        }

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred.",
            Extensions =
            {
                ["code"] = "validation_failed",
                ["errors"] = errors,
            },
        };
    }

    private ProblemDetails BuildInternalErrorProblem(Exception exception)
    {
        // Always log with full detail; never include details in the response body.
        _logger.LogError(
            exception,
            "Unhandled exception: {ExceptionType} — {Message}",
            exception.GetType().Name,
            exception.Message);

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred",
            Detail = "An internal server error occurred. Please contact support.",
            Extensions = { ["code"] = "internal_error" },
        };
    }
}
