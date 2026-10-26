using System.Web;

namespace Tot.Integrations.Runtime.Auth;

internal sealed class ApiKeyAuthProvider : IAuthenticationProvider
{
    private readonly string _headerOrParamName;
    private readonly string _value;
    private readonly bool _inQuery;

    internal ApiKeyAuthProvider(string headerOrParamName, string value, bool inQuery = false)
    {
        _headerOrParamName = headerOrParamName;
        _value = value;
        _inQuery = inQuery;
    }

    public Task AuthenticateAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_inQuery)
        {
            var uri = request.RequestUri
                ?? throw new InvalidOperationException("Request URI is null; cannot append API key query parameter.");
            var ub = new UriBuilder(uri);
            var qs = HttpUtility.ParseQueryString(ub.Query);
            qs[_headerOrParamName] = _value;
            ub.Query = qs.ToString();
            request.RequestUri = ub.Uri;
        }
        else
        {
            request.Headers.TryAddWithoutValidation(_headerOrParamName, _value);
        }
        return Task.CompletedTask;
    }
}
