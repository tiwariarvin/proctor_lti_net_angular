using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ProctorLti.Api.Models;
using ProctorLti.Api.Options;

namespace ProctorLti.Api.Services.Lms;

public sealed class CanvasLmsProvider(
    IHttpClientFactory httpFactory,
    IMemoryCache cache,
    IOptions<LmsOptions> options) : LmsProviderBase(httpFactory, cache)
{
    public const string ClientName = "lms-canvas";

    private readonly CanvasLmsOptions _opt = options.Value.Canvas;

    public override LmsPlatform Platform => LmsPlatform.Canvas;

    protected override string HttpClientName => ClientName;

    protected override string ApiBaseUrl => _opt.ResolvedApiBaseUrl;

    protected override Task<LmsAccessToken> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (_opt.UsesStaticToken)
        {
            // Static API tokens do not expire; cache for one day and refresh from config.
            var expiresAt = DateTimeOffset.UtcNow.AddDays(1);
            return Task.FromResult(new LmsAccessToken(_opt.ApiToken!, expiresAt));
        }

        var http = CreateHttpClient();
        var grant = _opt.GrantType.Trim();

        if (string.Equals(grant, "refresh_token", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_opt.RefreshToken))
            {
                throw new InvalidOperationException(
                    "Canvas refresh_token grant requires Lms:Canvas:RefreshToken.");
            }

            return LmsOAuthTokenClient.RequestRefreshTokenAsync(
                http,
                _opt.ResolvedTokenUrl,
                _opt.ClientId!,
                _opt.ClientSecret!,
                _opt.RefreshToken,
                cancellationToken);
        }

        return LmsOAuthTokenClient.RequestClientCredentialsAsync(
            http,
            _opt.ResolvedTokenUrl,
            _opt.ClientId!,
            _opt.ClientSecret!,
            cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_opt.BaseUrl))
        {
            throw new InvalidOperationException("Canvas LMS is not configured. Set Lms:Canvas:BaseUrl.");
        }

        if (!_opt.UsesStaticToken && !_opt.UsesOAuth)
        {
            throw new InvalidOperationException(
                "Canvas LMS requires either Lms:Canvas:ApiToken or ClientId + ClientSecret for OAuth.");
        }
    }
}
