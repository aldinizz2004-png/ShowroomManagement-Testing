namespace Showroom.Api.Common;

public enum ServiceErrorType
{
    None,
    Validation,
    NotFound
}

public class ServiceResult<T>
{
    public bool Succeeded => ErrorType == ServiceErrorType.None;
    public T? Value { get; private init; }
    public ServiceErrorType ErrorType { get; private init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; private init; } = new Dictionary<string, string[]>();

    public static ServiceResult<T> Ok(T value) => new() { Value = value };

    public static ServiceResult<T> Invalid(IDictionary<string, string[]> errors) =>
        new() { ErrorType = ServiceErrorType.Validation, Errors = new Dictionary<string, string[]>(errors) };

    public static ServiceResult<T> Invalid(string field, string message) =>
        Invalid(new Dictionary<string, string[]> { [field] = [message] });

    public static ServiceResult<T> NotFound(string message) =>
        new() { ErrorType = ServiceErrorType.NotFound, Errors = new Dictionary<string, string[]> { ["id"] = [message] } };
}
