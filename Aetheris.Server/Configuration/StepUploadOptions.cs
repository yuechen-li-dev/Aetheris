namespace Aetheris.Server.Configuration;

public sealed class StepUploadOptions
{
    public const string SectionName = "StepUpload";

    public int MaxUploadSizeMb { get; init; } = 250;

    public long MaxUploadSizeBytes => MaxUploadSizeMb * 1024L * 1024L;
}
