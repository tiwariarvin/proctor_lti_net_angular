using ProctorLti.Api.Models;

namespace ProctorLti.Api.Services.Lms;

/// <summary>
/// Facade for LMS integrations; resolves D2L (Brightspace) and Canvas providers.
/// </summary>
public interface ILmsService
{
    ILmsProvider GetProvider(LmsPlatform platform);

    ILmsProvider D2l { get; }

    ILmsProvider Canvas { get; }
}
