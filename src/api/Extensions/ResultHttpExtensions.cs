using Microsoft.AspNetCore.Mvc;

namespace EventReservation.Api.Extensions;

public static class ResultHttpExtensions
{
    private static readonly Dictionary<ErrorCategory, int> CategoryPrecedence = new()
    {
        [ErrorCategory.Unexpected] = 0,
        [ErrorCategory.Unavailable] = 1,
        [ErrorCategory.Unprocessable] = 2,
        [ErrorCategory.Conflict] = 3,
        [ErrorCategory.NotFound] = 4,
        [ErrorCategory.Validation] = 5
    };

    private static readonly Dictionary<ErrorCategory, int> CategoryStatusCode = new()
    {
        [ErrorCategory.Validation] = StatusCodes.Status400BadRequest,
        [ErrorCategory.NotFound] = StatusCodes.Status404NotFound,
        [ErrorCategory.Conflict] = StatusCodes.Status409Conflict,
        [ErrorCategory.Unavailable] = StatusCodes.Status503ServiceUnavailable,
        [ErrorCategory.Unprocessable] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCategory.Unexpected] = StatusCodes.Status500InternalServerError
    };

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess
            ? Results.NoContent()
            : ToProblemResult(result);

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblemResult(result);

    private static IResult ToProblemResult(Result result)
    {
        var category = result.Errors
            .Select(e => e.Category)
            .OrderBy(c => CategoryPrecedence[c])
            .FirstOrDefault();

        var statusCode = CategoryStatusCode.GetValueOrDefault(category, StatusCodes.Status500InternalServerError);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = category switch
            {
                ErrorCategory.Validation => "Validation Failed.",
                ErrorCategory.NotFound => "Resource not found.",
                ErrorCategory.Conflict => "Conflict.",
                ErrorCategory.Unavailable => "Service unavailable.",
                ErrorCategory.Unprocessable => "Unable to process entity given in request body",
                ErrorCategory.Unexpected => "An unexpected error occurred.",
                _ => "An unexpected error occurred."
            },
            Extensions = { ["errors"] = result.ToFailureMessages() }
        };

        return Results.Problem(problemDetails);
    }
}