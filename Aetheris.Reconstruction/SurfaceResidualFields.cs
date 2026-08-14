using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Reconstruction;

public enum ResidualInterpolationModel { PiecewiseBilinearGrid }
public enum ResidualSeamPolicy { SharedBoundarySamples, ZeroAtBoundary }
public enum ResidualNormalAuthority { DifferentialInterpretationOnly }

public sealed record ResidualFieldEvidence(
    PredicateEvidenceKind Kind,
    string Provenance,
    string Method,
    double PositionRms,
    double PositionMaximum,
    double NormalCorrectionRmsDegrees,
    double NormalCorrectionMaximumDegrees,
    int SourceSampleCount);

/// <summary>A bounded scalar displacement along the evaluated base unit normal.</summary>
public sealed class BilinearScalarField
{
    private readonly double[,] _values;

    public BilinearScalarField(double[,] values, ParametricDomain domain)
    {
        ArgumentNullException.ThrowIfNull(values); ArgumentNullException.ThrowIfNull(domain);
        if (values.GetLength(0) < 2 || values.GetLength(1) < 2) throw new ArgumentException("A bilinear residual grid needs at least 2 x 2 samples.", nameof(values));
        if (values.Cast<double>().Any(value => !double.IsFinite(value))) throw new ArgumentException("Residual samples must be finite.", nameof(values));
        _values = (double[,])values.Clone(); Domain = domain;
    }

    public ParametricDomain Domain { get; }
    public int CountU => _values.GetLength(0);
    public int CountV => _values.GetLength(1);
    public int SampleCount => CountU * CountV;
    public ResidualInterpolationModel Interpolation => ResidualInterpolationModel.PiecewiseBilinearGrid;

    public double Evaluate(double u, double v)
    {
        var (i, fu) = Cell(u, Domain.U, CountU); var (j, fv) = Cell(v, Domain.V, CountV);
        return Lerp(Lerp(_values[i, j], _values[i + 1, j], fu), Lerp(_values[i, j + 1], _values[i + 1, j + 1], fu), fv);
    }

    public (double Du, double Dv) Gradient(double u, double v)
    {
        var (i, fu) = Cell(u, Domain.U, CountU); var (j, fv) = Cell(v, Domain.V, CountV);
        var du = Lerp(_values[i + 1, j] - _values[i, j], _values[i + 1, j + 1] - _values[i, j + 1], fv) * (CountU - 1) / (Domain.U.Maximum - Domain.U.Minimum);
        var dv = Lerp(_values[i, j + 1] - _values[i, j], _values[i + 1, j + 1] - _values[i + 1, j], fu) * (CountV - 1) / (Domain.V.Maximum - Domain.V.Minimum);
        return (du, dv);
    }

    public double this[int i, int j] => _values[i, j];
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static (int Cell, double Fraction) Cell(double parameter, ParameterInterval2 interval, int count)
    {
        if (!double.IsFinite(parameter) || parameter < interval.Minimum || parameter > interval.Maximum) throw new ArgumentOutOfRangeException(nameof(parameter));
        var scaled = (parameter - interval.Minimum) / (interval.Maximum - interval.Minimum) * (count - 1);
        var cell = Math.Min(count - 2, (int)Math.Floor(scaled));
        return (cell, scaled - cell);
    }
}

/// <summary>Explicit target normals. They are interpretation evidence and never displace points.</summary>
public sealed class BilinearNormalField
{
    private readonly Vector3D[,] _values;
    public BilinearNormalField(Vector3D[,] values, ParametricDomain domain)
    {
        ArgumentNullException.ThrowIfNull(values); ArgumentNullException.ThrowIfNull(domain);
        if (values.GetLength(0) < 2 || values.GetLength(1) < 2) throw new ArgumentException("A bilinear normal grid needs at least 2 x 2 samples.", nameof(values));
        _values = new Vector3D[values.GetLength(0), values.GetLength(1)];
        for (var i = 0; i < values.GetLength(0); i++) for (var j = 0; j < values.GetLength(1); j++)
        {
            if (!values[i, j].TryNormalize(out var normal)) throw new ArgumentException("Normal samples must be finite non-zero vectors.", nameof(values));
            _values[i, j] = normal;
        }
        Domain = domain;
    }
    public ParametricDomain Domain { get; }
    public int CountU => _values.GetLength(0);
    public int CountV => _values.GetLength(1);
    public int SampleCount => CountU * CountV;
    public ResidualNormalAuthority Authority => ResidualNormalAuthority.DifferentialInterpretationOnly;
    public Vector3D Evaluate(double u, double v)
    {
        var (i, fu) = Cell(u, Domain.U, CountU); var (j, fv) = Cell(v, Domain.V, CountV);
        var a = _values[i, j] * (1 - fu) + _values[i + 1, j] * fu;
        var b = _values[i, j + 1] * (1 - fu) + _values[i + 1, j + 1] * fu;
        var value = a * (1 - fv) + b * fv;
        if (!value.TryNormalize(out value)) throw new ArithmeticException("Interpolated normal is singular.");
        return value;
    }
    private static (int, double) Cell(double p, ParameterInterval2 d, int n)
    {
        if (!double.IsFinite(p) || p < d.Minimum || p > d.Maximum) throw new ArgumentOutOfRangeException(nameof(p));
        var x = (p - d.Minimum) / (d.Maximum - d.Minimum) * (n - 1); var i = Math.Min(n - 2, (int)Math.Floor(x)); return (i, x - i);
    }
}

public sealed record CorrectedSurfaceSample(
    Point3D Point,
    Vector3D BaseNormal,
    Vector3D GeometricNormal,
    Vector3D InterpretedNormal,
    double Offset,
    bool PositionWasDisplaced,
    bool NormalCorrectionWasApplied);

/// <summary>
/// Reconstruction-owned detail attached to one structural patch. Offset authority is geometric;
/// normal correction authority is explicitly differential-only.
/// </summary>
public sealed class SurfaceResidualField
{
    public SurfaceResidualField(string stableId, string basePatchIdentity, ParametricDomain domain,
        BilinearScalarField? positionOffset, BilinearNormalField? normalCorrection,
        ResidualSeamPolicy seamPolicy, ResidualFieldEvidence evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId); ArgumentException.ThrowIfNullOrWhiteSpace(basePatchIdentity);
        ArgumentNullException.ThrowIfNull(domain); ArgumentNullException.ThrowIfNull(evidence);
        if (positionOffset is null && normalCorrection is null) throw new ArgumentException("A residual field must contain position or normal evidence.");
        if (positionOffset is not null && positionOffset.Domain != domain) throw new ArgumentException("Offset domain must match the base domain.");
        if (normalCorrection is not null && normalCorrection.Domain != domain) throw new ArgumentException("Normal domain must match the base domain.");
        StableId = stableId; BasePatchIdentity = basePatchIdentity; Domain = domain; PositionOffset = positionOffset;
        NormalCorrection = normalCorrection; SeamPolicy = seamPolicy; Evidence = evidence;
    }

    public string StableId { get; }
    public string BasePatchIdentity { get; }
    public ParametricDomain Domain { get; }
    public BilinearScalarField? PositionOffset { get; }
    public BilinearNormalField? NormalCorrection { get; }
    public ResidualSeamPolicy SeamPolicy { get; }
    public ResidualFieldEvidence Evidence { get; }
    public int ControlCount => (PositionOffset?.SampleCount ?? 0) + (NormalCorrection?.SampleCount ?? 0);

    public CorrectedSurfaceSample Evaluate(BoundedParametricPatch3 basePatch, double u, double v)
    {
        ArgumentNullException.ThrowIfNull(basePatch);
        if (!string.Equals(basePatch.StableId, BasePatchIdentity, StringComparison.Ordinal)) throw new ArgumentException("Residual field base identity does not match the supplied patch.", nameof(basePatch));
        var jet = basePatch.EvaluateJet1(u, v);
        if (jet.Normal is null) throw new InvalidOperationException("Residual evaluation requires a regular base first jet.");
        var baseNormal = jet.Normal.Value.ToVector(); var offset = PositionOffset?.Evaluate(u, v) ?? 0;
        var point = jet.Point + baseNormal * offset;
        var geometricNormal = PositionOffset is null ? baseNormal : DisplacedNormal(basePatch, u, v);
        var interpreted = NormalCorrection?.Evaluate(u, v) ?? geometricNormal;
        return new(point, baseNormal, geometricNormal, interpreted, offset, PositionOffset is not null, NormalCorrection is not null);
    }

    private Vector3D DisplacedNormal(BoundedParametricPatch3 patch, double u, double v)
    {
        var hu = (Domain.U.Maximum - Domain.U.Minimum) * 1e-5; var hv = (Domain.V.Maximum - Domain.V.Minimum) * 1e-5;
        var u0 = Math.Max(Domain.U.Minimum, u - hu); var u1 = Math.Min(Domain.U.Maximum, u + hu);
        var v0 = Math.Max(Domain.V.Minimum, v - hv); var v1 = Math.Min(Domain.V.Maximum, v + hv);
        Point3D P(double x, double y) { var j = patch.EvaluateJet1(x, y); return j.Point + j.Normal!.Value.ToVector() * PositionOffset!.Evaluate(x, y); }
        var du = (P(u1, v) - P(u0, v)) * (1 / (u1 - u0)); var dv = (P(u, v1) - P(u, v0)) * (1 / (v1 - v0));
        if (!du.Cross(dv).TryNormalize(out var normal)) throw new ArithmeticException("Displaced residual surface is singular.");
        return normal;
    }
}

public sealed record ResidualDecompositionSample(
    Point3D SourcePoint, double U, double V, Vector3D Residual,
    double NormalComponent, Vector3D TangentialComponent, double TangentialMagnitude,
    double? NormalCorrectionDegrees, PredicateEvidenceKind Evidence);

public static class SurfaceResidualExtractor
{
    public static ResidualDecompositionSample Decompose(Point3D sourcePoint, Vector3D? sourceNormal,
        BoundedParametricPatch3 basePatch, DistanceQueryPolicy? policy = null)
    {
        policy ??= DistanceQueryPolicy.Default; var closest = ClosestPointQuery.Between(sourcePoint, basePatch, policy);
        if (closest.Status != DistanceQueryStatus.Available || closest.PointOnB is null || closest.ParameterOnB?.U is not double u || closest.ParameterOnB.V is not double v)
            throw new InvalidOperationException("Bounded closest-point projection did not converge; scalar offset suitability is unknown.");
        var jet = basePatch.EvaluateJet1(u, v); if (jet.Normal is null) throw new InvalidOperationException("Closest base point has a singular normal.");
        var n = jet.Normal.Value.ToVector(); var residual = sourcePoint - closest.PointOnB.Value; var normal = residual.Dot(n); var tangential = residual - n * normal;
        double? angle = null; if (sourceNormal is Vector3D candidate && candidate.TryNormalize(out candidate)) angle = Math.Acos(Math.Clamp(candidate.Dot(n), -1, 1)) * 180 / Math.PI;
        return new(sourcePoint, u, v, residual, normal, tangential, tangential.Length, angle, closest.Evidence);
    }

    public static bool IsScalarOffsetSuitable(ResidualDecompositionSample sample, double maximumTangentialFraction = .2, double absoluteTolerance = 1e-6)
    {
        if (!double.IsFinite(maximumTangentialFraction) || maximumTangentialFraction < 0 || maximumTangentialFraction > 1) throw new ArgumentOutOfRangeException(nameof(maximumTangentialFraction));
        var length = sample.Residual.Length; return sample.TangentialMagnitude <= absoluteTolerance || (length > 0 && sample.TangentialMagnitude / length <= maximumTangentialFraction);
    }
}

/// <summary>Adapter that lets SurfaceMeshIR consume a real authored bounded patch without owning its semantic type.</summary>
public sealed class SurfaceMeshBoundedPatchAdapter : ISurfaceMeshBoundedPatch
{
    private readonly BoundedParametricPatch3 _patch;
    private readonly SurfaceResidualField? _residual;
    public SurfaceMeshBoundedPatchAdapter(BoundedParametricPatch3 patch, SurfaceResidualField? residual = null)
    {
        _patch = patch ?? throw new ArgumentNullException(nameof(patch));
        if (residual is not null && !string.Equals(residual.BasePatchIdentity, patch.StableId, StringComparison.Ordinal)) throw new ArgumentException("Residual base identity does not match the patch.", nameof(residual));
        _residual = residual;
    }
    public string StableId => _patch.StableId;
    public double MinimumU => _patch.Domain.U.Minimum;
    public double MaximumU => _patch.Domain.U.Maximum;
    public double MinimumV => _patch.Domain.V.Minimum;
    public double MaximumV => _patch.Domain.V.Maximum;
    public SurfaceMeshParametricJet Evaluate(double u, double v)
    {
        if (_residual is null) { var jet = _patch.EvaluateJet1(u, v); return new(jet.Point, jet.Du, jet.Dv); }
        var point = _residual.Evaluate(_patch, u, v).Point;
        var hu = (MaximumU - MinimumU) * 1e-5; var hv = (MaximumV - MinimumV) * 1e-5;
        var u0 = Math.Max(MinimumU, u - hu); var u1 = Math.Min(MaximumU, u + hu); var v0 = Math.Max(MinimumV, v - hv); var v1 = Math.Min(MaximumV, v + hv);
        var du = (_residual.Evaluate(_patch, u1, v).Point - _residual.Evaluate(_patch, u0, v).Point) * (1 / (u1 - u0));
        var dv = (_residual.Evaluate(_patch, u, v1).Point - _residual.Evaluate(_patch, u, v0).Point) * (1 / (v1 - v0));
        return new(point, du, dv);
    }
    public bool TryProject(Point3D point, out double u, out double v)
    {
        var hit = ClosestPointQuery.Between(point, _patch); u = hit.ParameterOnB?.U ?? 0; v = hit.ParameterOnB?.V ?? 0; return hit.Status == DistanceQueryStatus.Available && hit.ParameterOnB?.U is not null && hit.ParameterOnB.V is not null;
    }
}
