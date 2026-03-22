namespace DeployFlow.Application.Common;

/// <summary>
/// Wrapper for operation results without a value.
/// </summary>
public class Result
{
    public bool IsSuccess { get; protected set; }
    public string? Error { get; protected set; }
    public string? ErrorCode { get; protected set; }

    protected Result(bool isSuccess, string? error = null, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true);
    public static Result Failure(string error, string? code = null) => new(false, error, code);
    public static Result Failure(string error, int statusCode) => new(false, error, statusCode.ToString());

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error, string? code = null) => Result<T>.Failure(error, code);
    public static Result<T> Failure<T>(string error, int statusCode) => Result<T>.Failure(error, statusCode.ToString());
}

/// <summary>
/// Wrapper for operation results with a value.
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; private set; }

    private Result(bool isSuccess, T? value, string? error, string? errorCode)
        : base(isSuccess, error, errorCode)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public new static Result<T> Failure(string error, string? code = null) => new(false, default, error, code);
    public new static Result<T> Failure(string error, int statusCode) => new(false, default, error, statusCode.ToString());
}