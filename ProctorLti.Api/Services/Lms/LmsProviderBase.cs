using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using ProctorLti.Api.Models;

namespace ProctorLti.Api.Services.Lms;

public abstract class LmsProviderBase(
    IHttpClientFactory httpFactory,
    IMemoryCache cache) : ILmsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public abstract LmsPlatform Platform { get; }

    protected abstract string HttpClientName { get; }

    protected abstract string ApiBaseUrl { get; }

    protected abstract Task<LmsAccessToken> AcquireTokenAsync(CancellationToken cancellationToken);

    protected HttpClient CreateHttpClient() => httpFactory.CreateClient(HttpClientName);

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await cache.GetOrCreateAsync(
            $"lms-token:{Platform}",
            async entry =>
            {
                var acquired = await AcquireTokenAsync(cancellationToken).ConfigureAwait(false);
                entry.AbsoluteExpiration = acquired.ExpiresAt;
                return acquired;
            }).ConfigureAwait(false);

        if (token is null || string.IsNullOrWhiteSpace(token.Value))
            throw new InvalidOperationException($"Could not obtain LMS access token for {Platform}.");

        return token.Value;
    }

    public Task<HttpResponseMessage> GetAsync(string relativePath, CancellationToken cancellationToken = default) =>
        SendAuthorizedAsync(HttpMethod.Get, relativePath, null, cancellationToken);

    public Task<HttpResponseMessage> PostAsync(
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken = default) =>
        SendAuthorizedAsync(HttpMethod.Post, relativePath, content, cancellationToken);

    public Task<HttpResponseMessage> PutAsync(
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken = default) =>
        SendAuthorizedAsync(HttpMethod.Put, relativePath, content, cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(string relativePath, CancellationToken cancellationToken = default) =>
        SendAuthorizedAsync(HttpMethod.Delete, relativePath, null, cancellationToken);

    public async Task<T?> GetJsonAsync<T>(string relativePath, CancellationToken cancellationToken = default)
    {
        using var response = await GetAsync(relativePath, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
        string relativePath,
        TRequest body,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await PostAsync(relativePath, content, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<TResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    protected async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var client = httpFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(method, BuildApiUri(relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null)
            request.Content = content;

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected Uri BuildApiUri(string relativePath)
    {
        var path = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;
        return new Uri($"{ApiBaseUrl.TrimEnd('/')}{path}");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException(
            $"LMS API call failed ({(int)response.StatusCode}): {body}",
            null,
            response.StatusCode);
    }
}
