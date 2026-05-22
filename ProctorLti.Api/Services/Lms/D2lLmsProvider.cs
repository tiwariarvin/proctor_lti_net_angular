using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ProctorLti.Api.Models;
using ProctorLti.Api.Options;

namespace ProctorLti.Api.Services.Lms;

public sealed class D2lLmsProvider(
    IHttpClientFactory httpFactory,
    IMemoryCache cache,
    IOptions<LmsOptions> options) : LmsProviderBase(httpFactory, cache)
{
    public const string ClientName = "lms-d2l";

    private readonly D2lLmsOptions _opt = options.Value.D2l;

    public override LmsPlatform Platform => LmsPlatform.D2l;

    protected override string HttpClientName => ClientName;

    protected override string ApiBaseUrl => _opt.ResolvedApiBaseUrl;

    protected override Task<LmsAccessToken> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var http = CreateHttpClient();
        return LmsOAuthTokenClient.RequestClientCredentialsAsync(
            http,
            _opt.ResolvedTokenUrl,
            _opt.ClientId,
            _opt.ClientSecret,
            cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_opt.BaseUrl)
            || string.IsNullOrWhiteSpace(_opt.ClientId)
            || string.IsNullOrWhiteSpace(_opt.ClientSecret))
        {
            throw new InvalidOperationException(
                "D2L LMS is not configured. Set Lms:D2l:BaseUrl, ClientId, and ClientSecret.");
        }
    }
}
