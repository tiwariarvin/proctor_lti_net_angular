using ProctorLti.Api.Models;

namespace ProctorLti.Api.Services.Lms;

/// <summary>
/// Server-to-server access to a single LMS platform (OAuth token + REST fetch/post).
/// </summary>
public interface ILmsProvider
{
    LmsPlatform Platform { get; }

    /// <summary>Returns a bearer token for REST calls (cached until expiry).</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> GetAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> PostAsync(
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> PutAsync(
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<T?> GetJsonAsync<T>(string relativePath, CancellationToken cancellationToken = default);

    Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
        string relativePath,
        TRequest body,
        CancellationToken cancellationToken = default);
}
