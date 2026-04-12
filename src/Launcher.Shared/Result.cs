// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Shared;

/// <summary>
/// 统一操作结果。所有可能失败的操作都返回 Result。
/// 通过返回值传递错误，不通过异常控制流。
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Ok() => new(true, null);

    public static Result Fail(Error error) => new(false, error);

    public static Result Fail(string code, string userMessage)
        => new(false, new Error { Code = code, UserMessage = userMessage });

    public static Result<T> Ok<T>(T value) => new(value, true, null);

    public static Result<T> Fail<T>(Error error) => new(default, false, error);

    public static Result<T> Fail<T>(string code, string userMessage)
        => new(default, false, new Error { Code = code, UserMessage = userMessage });
}

/// <summary>
/// 带值的操作结果。
/// </summary>
public sealed class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T? value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    /// <summary>
    /// 链式转换：成功时应用 map 函数，失败时直接传递错误。
    /// </summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        return IsSuccess
            ? Result.Ok(map(Value!))
            : Result.Fail<TOut>(Error!);
    }

    /// <summary>
    /// 链式绑定：成功时执行下一步操作，失败时直接传递错误。
    /// </summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
    {
        return IsSuccess
            ? bind(Value!)
            : Result.Fail<TOut>(Error!);
    }
}
