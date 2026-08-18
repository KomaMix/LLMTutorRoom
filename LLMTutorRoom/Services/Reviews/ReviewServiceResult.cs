using System.Net;

namespace LLMTutorRoom.Services.Reviews;

public sealed record ReviewServiceResult<T>(
    HttpStatusCode StatusCode,
    T? Value,
    string Error)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
}
