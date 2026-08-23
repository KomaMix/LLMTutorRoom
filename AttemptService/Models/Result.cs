namespace AttemptService.Models;

public sealed class Result<TStatus, TValue>
{
    public required TStatus Status { get; init; }
    public TValue? Value { get; init; }
}
