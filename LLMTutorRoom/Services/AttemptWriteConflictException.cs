namespace LLMTutorRoom.Services;

public sealed class AttemptWriteConflictException : Exception
{
    public AttemptWriteConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
