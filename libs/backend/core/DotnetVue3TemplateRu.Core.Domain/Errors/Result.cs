namespace DotnetVue3TemplateRu.Core.Domain.Errors;

/// <summary>
/// Результат фабрики переиспользуемого value object (напр. Email, Bin в shared kernel).
/// Общий leaf-VO валидирует формат, но не выбирает контекст: при провале несёт
/// обезличенный код (ErrorCode) и параметры, а агрегат-композитор разворачивает
/// результат и бросает DomainException с контекстным кодом. Собственные инварианты
/// агрегата бросают DomainException напрямую (Result там не нужен). См. ADR 0024.
/// </summary>
public readonly struct Result<T>
{
    public bool IsSuccess { get; }

    public T? Value { get; }

    public string? ErrorCode { get; }

    public IReadOnlyList<object?> Args { get; }

    private Result(bool isSuccess, T? value, string? errorCode, object?[] args)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        Args = args;
    }

    public static Result<T> Success(T value) => new(true, value, null, []);

    public static Result<T> Failure(string errorCode, params object?[] args) =>
        new(false, default, errorCode, args ?? []);
}
