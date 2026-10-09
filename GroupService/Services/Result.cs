namespace GroupService.Services;

public class Result<TStatus, TValue>
{
    public Result(TStatus status, TValue? value = default, string? error = null)
    {
        Status = status;
        Value = value;
        Error = error;
    }

    public TStatus Status { get; }
    public TValue? Value { get; }
    public string? Error { get; }
}
