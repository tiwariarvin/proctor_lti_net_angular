using System.ComponentModel.DataAnnotations;

namespace ProctorLti.Api.Options;

public class LmsOptions
{
    public const string SectionName = "Lms";

    public D2lLmsOptions D2l { get; set; } = new();

    public CanvasLmsOptions Canvas { get; set; } = new();
}

public class D2lLmsOptions
{
    /// <summary>Brightspace host base URL, e.g. https://university.brightspace.com</summary>
    [Required]
    public string BaseUrl { get; set; } = "";

    [Required]
    public string ClientId { get; set; } = "";

    [Required]
    public string ClientSecret { get; set; } = "";

    /// <summary>Override OAuth2 token endpoint. Default: {BaseUrl}/d2l/auth/oauth2/token</summary>
    public string? TokenUrl { get; set; }

    /// <summary>Override Valence LP API root. Default: {BaseUrl}/d2l/api/lp</summary>
    public string? ApiBaseUrl { get; set; }

    public string ResolvedTokenUrl =>
        string.IsNullOrWhiteSpace(TokenUrl)
            ? $"{BaseUrl.TrimEnd('/')}/d2l/auth/oauth2/token"
            : TokenUrl.Trim();

    public string ResolvedApiBaseUrl =>
        string.IsNullOrWhiteSpace(ApiBaseUrl)
            ? $"{BaseUrl.TrimEnd('/')}/d2l/api/lp"
            : ApiBaseUrl.TrimEnd('/');
}

public class CanvasLmsOptions
{
    /// <summary>Canvas root URL, e.g. https://school.instructure.com</summary>
    [Required]
    public string BaseUrl { get; set; } = "";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>
    /// Optional long-lived API token for server-to-server calls (skips OAuth when set).
    /// </summary>
    public string? ApiToken { get; set; }

    public string? RefreshToken { get; set; }

    /// <summary>OAuth grant type when using client credentials or refresh token. Default: client_credentials</summary>
    public string GrantType { get; set; } = "client_credentials";

    /// <summary>Override OAuth2 token endpoint. Default: {BaseUrl}/login/oauth2/token</summary>
    public string? TokenUrl { get; set; }

    /// <summary>Override REST API root. Default: {BaseUrl}/api/v1</summary>
    public string? ApiBaseUrl { get; set; }

    public bool UsesOAuth =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);

    public bool UsesStaticToken => !string.IsNullOrWhiteSpace(ApiToken);

    public string ResolvedTokenUrl =>
        string.IsNullOrWhiteSpace(TokenUrl)
            ? $"{BaseUrl.TrimEnd('/')}/login/oauth2/token"
            : TokenUrl.Trim();

    public string ResolvedApiBaseUrl =>
        string.IsNullOrWhiteSpace(ApiBaseUrl)
            ? $"{BaseUrl.TrimEnd('/')}/api/v1"
            : ApiBaseUrl.TrimEnd('/');
}
