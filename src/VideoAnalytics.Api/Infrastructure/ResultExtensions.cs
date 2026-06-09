namespace VideoAnalytics.Api.Infrastructure;

using ErrorOr;

internal static class ResultExtensions
{
    internal static IResult ToHttpResult(this Error error) => error.Type switch
    {
        ErrorType.NotFound     => TypedResults.Problem(error.Description, statusCode: 404, title: error.Code),
        ErrorType.Conflict     => TypedResults.Problem(error.Description, statusCode: 409, title: error.Code),
        ErrorType.Validation   => TypedResults.Problem(error.Description, statusCode: 422, title: error.Code),
        ErrorType.Unauthorized => TypedResults.Problem(error.Description, statusCode: 401, title: error.Code),
        _                      => TypedResults.Problem("Internal server error", statusCode: 500, title: error.Code)
    };
}
