namespace TeachingService.Services
{
    public sealed class Result<S, V>
    {
        public Result(S status, V? value = default, string? error = null)
        {
            Status = status;
            Value = value;
            Error = error;
        }

        public S Status { get; }
        public V? Value { get; }
        public string? Error { get; }
    }
}
