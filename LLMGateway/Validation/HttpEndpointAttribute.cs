using System.ComponentModel.DataAnnotations;

namespace LLMGateway.Validation;

public sealed class HttpEndpointAttribute : ValidationAttribute
{
    public HttpEndpointAttribute() : base("Use an absolute HTTP or HTTPS URL without credentials, query or fragment.") { }

    public override bool IsValid(object? value) => value is string endpoint
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
