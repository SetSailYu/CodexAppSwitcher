namespace CodexAppSwitcher.Models;

/// <summary>
/// 操作结果，避免用裸异常表达业务流程。
/// </summary>
public sealed class OperationResult
{
    private OperationResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 结果消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    public static OperationResult Success(string message) => new(true, message);

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    public static OperationResult Failure(string message) => new(false, message);
}
