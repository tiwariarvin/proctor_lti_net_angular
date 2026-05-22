namespace ProctorLti.Api.Services.Lms;

public sealed record LmsAccessToken(string Value, DateTimeOffset ExpiresAt);
