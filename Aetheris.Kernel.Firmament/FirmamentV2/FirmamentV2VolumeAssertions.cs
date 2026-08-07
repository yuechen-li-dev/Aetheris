using Aetheris.Kernel.Core.Brep.Verification;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Source-level verification contract. It is never geometry or PMI input.</summary>
public sealed record FirmamentV2VolumeAssertion(
    string Id,
    string TargetBodyId,
    double ExpectedMm3,
    double ToleranceMm3,
    string? Note,
    FirmamentV2SourceSpan SourceSpan,
    string Provenance = "FirmamentSource");

public sealed record FirmamentV2VolumeAssertionResult(
    string AssertionId,
    string TargetBodyId,
    double ExpectedMm3,
    double? MeasuredMm3,
    double? DeltaMm3,
    double? AbsoluteDeltaMm3,
    double ToleranceMm3,
    bool Passed,
    string? Note,
    string MeasurementMethod,
    double? MeasurementErrorBoundMm3,
    string? Diagnostic,
    FirmamentV2SourceSpan SourceSpan,
    string Provenance);

public static class FirmamentV2VolumeAssertionComparer
{
    public static FirmamentV2VolumeAssertionResult Compare(FirmamentV2VolumeAssertion assertion, BrepMassPropertiesResult mass)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        ArgumentNullException.ThrowIfNull(mass);
        if (mass.Status == BrepMassPropertiesStatus.Unavailable)
        {
            return new(assertion.Id, assertion.TargetBodyId, assertion.ExpectedMm3, null, null, null, assertion.ToleranceMm3, false,
                assertion.Note, mass.EvaluationMethod, mass.ErrorBound,
                $"firmament-v2-assert-volume-measurement-unavailable:{assertion.TargetBodyId}:{assertion.Id}", assertion.SourceSpan, assertion.Provenance);
        }

        var delta = mass.AbsoluteVolume - assertion.ExpectedMm3;
        var absoluteDelta = double.Abs(delta);
        var passed = absoluteDelta <= assertion.ToleranceMm3;
        var diagnostic = passed ? null : $"firmament-v2-assert-volume-failed:target={assertion.TargetBodyId}:expectedMm3={assertion.ExpectedMm3:G17}:measuredMm3={mass.AbsoluteVolume:G17}:deltaMm3={delta:G17}:toleranceMm3={assertion.ToleranceMm3:G17}:method={mass.EvaluationMethod}:errorBoundMm3={mass.ErrorBound?.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable"}";
        return new(assertion.Id, assertion.TargetBodyId, assertion.ExpectedMm3, mass.AbsoluteVolume, delta, absoluteDelta, assertion.ToleranceMm3,
            passed, assertion.Note, mass.EvaluationMethod, mass.ErrorBound, diagnostic, assertion.SourceSpan, assertion.Provenance);
    }
}
