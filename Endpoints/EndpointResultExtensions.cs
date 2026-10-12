using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using xstackApi.Models;

namespace xstackApi.Endpoints;

public static class EndpointResultExtensions
{
    public static IResult ToHttpResult<T>(this ServiceResult<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess != null ? onSuccess(result.Data!) : TypedResults.Ok(result.Data);
        }

        return result.Error!.Type switch
        {
            ServiceErrorType.NotFound => TypedResults.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = result.Error.Message
            }),
            ServiceErrorType.Forbidden => TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: result.Error.Message),
            ServiceErrorType.Conflict => TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = result.Error.Message
            }),
            _ => TypedResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = result.Error.Message
            })
        };
    }

    public static IResult ToHttpResult(this ServiceResult result, Func<IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess != null ? onSuccess() : TypedResults.NoContent();
        }

        return result.Error!.Type switch
        {
            ServiceErrorType.NotFound => TypedResults.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = result.Error.Message
            }),
            ServiceErrorType.Forbidden => TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: result.Error.Message),
            ServiceErrorType.Conflict => TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = result.Error.Message
            }),
            _ => TypedResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = result.Error.Message
            })
        };
    }
}
