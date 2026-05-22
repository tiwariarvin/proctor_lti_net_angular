using ProctorLti.Api.Models;

namespace ProctorLti.Api.Services.Lms;

public sealed class LmsService(D2lLmsProvider d2l, CanvasLmsProvider canvas) : ILmsService
{
    public ILmsProvider D2l { get; } = d2l;

    public ILmsProvider Canvas { get; } = canvas;

    public ILmsProvider GetProvider(LmsPlatform platform) => platform switch
    {
        LmsPlatform.D2l => D2l,
        LmsPlatform.Canvas => Canvas,
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown LMS platform."),
    };
}
