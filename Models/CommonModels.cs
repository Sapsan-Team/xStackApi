namespace xstackApi.Models;

public enum ServiceErrorType
{
    NotFound,
    Forbidden,
    Conflict,
    BadRequest
}

public sealed record ServiceError(ServiceErrorType Type, string Message);

public sealed record ServiceResult<T>
{
    public T? Data { get; }
    public ServiceError? Error { get; }
    public bool IsSuccess => Error is null;

    private ServiceResult(T data) => Data = data;
    private ServiceResult(ServiceError error) => Error = error;

    public static ServiceResult<T> Success(T data) => new(data);
    public static ServiceResult<T> NotFound(string message = "Resource not found") => new(new ServiceError(ServiceErrorType.NotFound, message));
    public static ServiceResult<T> Forbidden(string message = "Access denied") => new(new ServiceError(ServiceErrorType.Forbidden, message));
    public static ServiceResult<T> Conflict(string message = "Conflict occurred") => new(new ServiceError(ServiceErrorType.Conflict, message));
    public static ServiceResult<T> BadRequest(string message = "Bad request") => new(new ServiceError(ServiceErrorType.BadRequest, message));
}

public sealed record ServiceResult
{
    public ServiceError? Error { get; }
    public bool IsSuccess => Error is null;

    private ServiceResult() { }
    private ServiceResult(ServiceError error) => Error = error;

    public static ServiceResult Success() => new();
    public static ServiceResult NotFound(string message = "Resource not found") => new(new ServiceError(ServiceErrorType.NotFound, message));
    public static ServiceResult Forbidden(string message = "Access denied") => new(new ServiceError(ServiceErrorType.Forbidden, message));
    public static ServiceResult Conflict(string message = "Conflict occurred") => new(new ServiceError(ServiceErrorType.Conflict, message));
    public static ServiceResult BadRequest(string message = "Bad request") => new(new ServiceError(ServiceErrorType.BadRequest, message));
}
