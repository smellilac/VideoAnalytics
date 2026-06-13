using VideoAnalytics.Application.Datasets.Common;

namespace VideoAnalytics.Api.Infrastructure;

using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using VideoAnalytics.Application.Common;

internal static class ErrorOrExtensions
{
    internal static IResult ToHttpResult(this Error error) => error.Type switch
    {
        ErrorType.NotFound     => TypedResults.Problem(error.Description, statusCode: 404, title: error.Code),
        ErrorType.Conflict     => TypedResults.Problem(error.Description, statusCode: 409, title: error.Code),
        ErrorType.Validation   => TypedResults.Problem(error.Description, statusCode: 422, title: error.Code),
        ErrorType.Unauthorized => TypedResults.Problem(error.Description, statusCode: 401, title: error.Code),
        ErrorType.Forbidden    => TypedResults.Problem(error.Description, statusCode: 403, title: error.Code),
        // below case for (ErrorType)503 - numeric type
        _ when error.Code == DatasetErrors.Codes.DataNotReady => BuildDataNotReadyResult(error),
        _ => TypedResults.Problem("Internal server error", statusCode: 500, title: error.Code)
    };

    private static IResult BuildDataNotReadyResult(Error error)
    {
        var problem = new ProblemDetails
        {
            Status = error.NumericType,
            Title = error.Description
        };

        if (error.Metadata?.TryGetValue("issues", out var issues) == true)
            problem.Extensions["issues"] = issues;

        return TypedResults.Problem(problem);
    }
}