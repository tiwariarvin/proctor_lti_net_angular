using System.Net.Http.Headers;
using System.Text.Json;

namespace ProctorLti.Api.Services.Lms;

internal static class LmsOAuthTokenClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<LmsAccessToken> RequestClientCredentialsAsync(
        HttpClient http,
        string tokenUrl,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        };

        return await RequestTokenAsync(http, tokenUrl, form, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<LmsAccessToken> RequestRefreshTokenAsync(
        HttpClient http,
        string tokenUrl,
        string clientId,
        string clientSecret,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
        };

        return await RequestTokenAsync(http, tokenUrl, form, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<LmsAccessToken> RequestTokenAsync(
        HttpClient http,
        string tokenUrl,
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var response = await http.PostAsync(new Uri(tokenUrl), content, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"LMS token request failed ({(int)response.StatusCode}): {body}");

        var token = JsonSerializer.Deserialize<OAuthTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("LMS token response was empty.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("LMS token response did not include access_token.");

        var expiresIn = token.ExpiresIn > 0 ? token.ExpiresIn : 3600;
        var skew = TimeSpan.FromMinutes(2);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn).Subtract(skew);

        return new LmsAccessToken(token.AccessToken, expiresAt);
    }

    private sealed class OAuthTokenResponse
    {
        public string? AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}
