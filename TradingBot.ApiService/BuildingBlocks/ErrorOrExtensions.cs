using ErrorOr;

namespace TradingBot.ApiService.BuildingBlocks;

public static class ErrorOrExtensions
{
    public static IResult ToHttpResult<T>(this ErrorOr<T> errorOr)
    {
        if (!errorOr.IsError)
        {
            return errorOr.Value switch
            {
                Updated => Results.NoContent(),
                _ => Results.Ok(errorOr.Value)
            };
        }

        var firstError = errorOr.FirstError;

        return firstError.Type switch
        {
            ErrorType.Validation => Results.Problem(
                statusCode: 400,
                title: "Validation Error",
                detail: firstError.Description,
                extensions: errorOr.Errors.Count > 1
                    ? new Dictionary<string, object?>
                    {
                        ["errors"] = errorOr.Errors.Select(e => new { code = e.Code, message = e.Description }).ToList()
                    }
                    : new Dictionary<string, object?>
                    {
                        ["errors"] = new[] { new { code = firstError.Code, message = firstError.Description } }
                    }),
            ErrorType.NotFound => Results.Problem(
                statusCode: 404,
                title: "Not Found",
                detail: firstError.Description),
            ErrorType.Conflict => Results.Problem(
                statusCode: 409,
                title: "Conflict",
                detail: firstError.Description),
            _ => Results.Problem(
                statusCode: 500,
                title: "Internal Error",
                detail: firstError.Description)
        };
    }
}
