using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Firmament.Analysis;

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
    bool MeasurementAuthoritative,
    string? Diagnostic,
    FirmamentV2SourceSpan SourceSpan,
    string Provenance)
{
    public double? LowerBoundMm3 { get; init; }
    public double? UpperBoundMm3 { get; init; }
    public string VerificationStatus { get; init; } = "Unverified";
    public int? RegionsVisited { get; init; }
}

public static class FirmamentV2VolumeAssertionComparer
{
    public static FirmamentV2VolumeAssertionResult Compare(FirmamentV2VolumeAssertion assertion, BrepMassPropertiesResult mass)
    {
        ArgumentNullException.ThrowIfNull(mass);
        return Compare(assertion, new FirmamentVolumeMeasurement(mass.Status == BrepMassPropertiesStatus.Unavailable ? null : mass.AbsoluteVolume,
            mass.IsAuthoritativeForVolumeAssertion ? mass.AbsoluteVolume : null,
            mass.IsAuthoritativeForVolumeAssertion ? mass.AbsoluteVolume : null, mass.EvaluationMethod, mass.IsAuthoritativeForVolumeAssertion));
    }

    public static FirmamentV2VolumeAssertionResult Compare(FirmamentV2VolumeAssertion assertion, FirmamentVolumeMeasurement measurement)
    {
        var lo = measurement.LowerBoundMm3; var hi = measurement.UpperBoundMm3;
        var permittedMin = assertion.ExpectedMm3 - assertion.ToleranceMm3;
        var permittedMax = assertion.ExpectedMm3 + assertion.ToleranceMm3;
        var verified = measurement.Authoritative && lo.HasValue && hi.HasValue
            && double.IsFinite(lo.Value) && double.IsFinite(hi.Value) && lo.Value <= hi.Value;
        var passed = verified && lo >= permittedMin && hi <= permittedMax;
        var failed = verified && (hi < permittedMin || lo > permittedMax);
        var status = passed ? "Passed" : failed ? "Failed" : "Unverified";
        var code = failed ? "firmament-v2-assert-volume-failed" : "firmament-v2-assert-volume-unverified";
        string? diagnostic = null;
        if (!passed)
            diagnostic = measurement.Diagnostic ?? FormattableString.Invariant(
                $"{code}: target={assertion.TargetBodyId}; expectedMm3={assertion.ExpectedMm3:G17}; measuredMm3={measurement.ValueMm3:G17}; deltaMm3={measurement.ValueMm3 - assertion.ExpectedMm3:G17}; toleranceMm3={assertion.ToleranceMm3:G17}; intervalMm3=[{lo:G17},{hi:G17}]; method={measurement.Method}. ")
                + (failed ? "The measured interval lies outside the allowed range." : "No proof at the requested tolerance. Use 'aetheris measure' to inspect the measurement interval; do not copy an approximate value with zero tolerance.");
        return new(assertion.Id, assertion.TargetBodyId, assertion.ExpectedMm3, measurement.ValueMm3,
            measurement.ValueMm3 - assertion.ExpectedMm3, measurement.ValueMm3 is { } value ? Math.Abs(value - assertion.ExpectedMm3) : null,
            assertion.ToleranceMm3, passed, assertion.Note, measurement.Method, measurement.ErrorBoundMm3, measurement.Authoritative,
            diagnostic, assertion.SourceSpan, assertion.Provenance)
        { LowerBoundMm3 = lo, UpperBoundMm3 = hi, VerificationStatus = status, RegionsVisited = measurement.RegionsVisited };
    }
}
