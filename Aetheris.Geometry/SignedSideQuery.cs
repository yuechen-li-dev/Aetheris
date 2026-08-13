using Aetheris.Kernel.Core.Math;

namespace Aetheris.Geometry;

public readonly record struct Plane3(Point3D Origin, Direction3D Normal)
{
    public double SignedDistance(Point3D point) => Normal.ToVector().Dot(point - Origin);
}

public enum SignedSideClassification { Positive, Negative, Touching, Crossing, Unknown }
public enum PredicateEvidenceKind { Structural, Certified, ToleranceBounded, Sampled, Heuristic, Unknown }
public enum SignedSideQueryMethod { Sampled, CertifiedInterval }
public enum GeometryQueryDiagnosticCode
{
    InvalidParameterDomain,
    SingularPatchEvaluation,
    InvalidPlane,
    UnsupportedCertifiedExpression,
    SubdivisionBudgetExhausted,
    NonFiniteEvaluation,
    EvidenceUnavailable,
    IterationBudgetExhausted,
    UnsupportedPairFamily,
    IllConditionedCandidate
}

public sealed record GeometryQueryDiagnostic(GeometryQueryDiagnosticCode Code, string Message);
public sealed record SignedSideWitness(double U, double V, Point3D Point, double SignedDistance, string Kind);
public sealed record SignedSidePolicy
{
    public SignedSidePolicy(SignedSideQueryMethod method, double tolerance = 1e-9, int samplesU = 9, int samplesV = 9,
        int maximumSubdivisionDepth = 10, int maximumLeafCount = 4096)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (samplesU < 2 || samplesV < 2) throw new ArgumentOutOfRangeException(nameof(samplesU));
        if (maximumSubdivisionDepth < 0 || maximumLeafCount < 1) throw new ArgumentOutOfRangeException(nameof(maximumSubdivisionDepth));
        Method = method; Tolerance = tolerance; SamplesU = samplesU; SamplesV = samplesV;
        MaximumSubdivisionDepth = maximumSubdivisionDepth; MaximumLeafCount = maximumLeafCount;
    }

    public SignedSideQueryMethod Method { get; }
    public double Tolerance { get; }
    public int SamplesU { get; }
    public int SamplesV { get; }
    public int MaximumSubdivisionDepth { get; }
    public int MaximumLeafCount { get; }
    public static SignedSidePolicy Sampled(double tolerance = 1e-9, int samplesU = 9, int samplesV = 9) => new(SignedSideQueryMethod.Sampled, tolerance, samplesU, samplesV);
    public static SignedSidePolicy Certified(double tolerance = 1e-9, int maximumSubdivisionDepth = 10, int maximumLeafCount = 4096) => new(SignedSideQueryMethod.CertifiedInterval, tolerance, maximumSubdivisionDepth: maximumSubdivisionDepth, maximumLeafCount: maximumLeafCount);
}

public sealed record SignedSideStatistics(int SampleCount, int LeafCount, int ResolvedLeaves, int UnresolvedLeaves, int MaximumDepthReached);
public sealed record SignedSideResult(
    SignedSideClassification Classification,
    PredicateEvidenceKind EvidenceKind,
    SignedSidePolicy Policy,
    ParametricDomain Domain,
    string PatchIdentity,
    string Provenance,
    GeometryRepresentationKind Representation,
    double? ObservedMinimum,
    double? ObservedMaximum,
    double? CertifiedLowerBound,
    double? CertifiedUpperBound,
    IReadOnlyList<SignedSideWitness> Witnesses,
    SignedSideStatistics Statistics,
    IReadOnlyList<GeometryQueryDiagnostic> Diagnostics);

/// <summary>A compile-time geometric obligation. It validates evidence and never requests topology construction.</summary>
public sealed record SignedSideExpectation(
    SignedSideClassification ExpectedClassification,
    PredicateEvidenceKind MinimumEvidence = PredicateEvidenceKind.Sampled)
{
    public SignedSideExpectationResult Evaluate(SignedSideResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var classificationSatisfied = result.Classification == ExpectedClassification;
        var evidenceSatisfied = EvidenceRank(result.EvidenceKind) >= EvidenceRank(MinimumEvidence);
        return new(this, result, classificationSatisfied && evidenceSatisfied,
            classificationSatisfied ? (evidenceSatisfied ? null : $"Expected at least {MinimumEvidence} evidence; observed {result.EvidenceKind}.")
                : $"Expected {ExpectedClassification}; observed {result.Classification}.");
    }

    private static int EvidenceRank(PredicateEvidenceKind kind) => kind switch
    {
        PredicateEvidenceKind.Structural => 5,
        PredicateEvidenceKind.Certified => 4,
        PredicateEvidenceKind.ToleranceBounded => 3,
        PredicateEvidenceKind.Sampled => 2,
        PredicateEvidenceKind.Heuristic => 1,
        _ => 0
    };
}

public sealed record SignedSideExpectationResult(
    SignedSideExpectation Expectation,
    SignedSideResult QueryResult,
    bool Satisfied,
    string? RejectionReason);

public static class SignedSideQuery
{
    public static SignedSideResult Query(BoundedParametricPatch3 patch, Plane3 plane, SignedSidePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(patch); ArgumentNullException.ThrowIfNull(policy);
        var normal = plane.Normal.ToVector();
        if (!double.IsFinite(normal.X) || !double.IsFinite(normal.Y) || !double.IsFinite(normal.Z) || normal.LengthSquared < .5d)
            return Result(SignedSideClassification.Unknown, PredicateEvidenceKind.Unknown, patch, policy, null, null, null, null, [],
                new(0, 0, 0, 1, 0), [new(GeometryQueryDiagnosticCode.InvalidPlane, "Plane normal must be finite and non-zero.")]);
        return policy.Method == SignedSideQueryMethod.Sampled ? Sample(patch, plane, policy) : Certify(patch, plane, policy);
    }

    private static SignedSideResult Sample(BoundedParametricPatch3 patch, Plane3 plane, SignedSidePolicy policy)
    {
        var witnesses = new List<SignedSideWitness>(); var diagnostics = new List<GeometryQueryDiagnostic>();
        var minimum = double.PositiveInfinity; var maximum = double.NegativeInfinity;
        SignedSideWitness? positive = null, negative = null, contact = null;
        for (var i = 0; i < policy.SamplesU; i++)
        for (var j = 0; j < policy.SamplesV; j++)
        {
            var u = patch.Domain.U.Map(i / (double)(policy.SamplesU - 1)); var v = patch.Domain.V.Map(j / (double)(policy.SamplesV - 1));
            try
            {
                var evaluated = patch.Evaluate(u, v); var distance = plane.SignedDistance(evaluated.Point);
                if (!double.IsFinite(distance)) { diagnostics.Add(new(GeometryQueryDiagnosticCode.NonFiniteEvaluation, $"Non-finite signed distance at ({u:R}, {v:R}).")); continue; }
                minimum = double.Min(minimum, distance); maximum = double.Max(maximum, distance);
                var witness = new SignedSideWitness(u, v, evaluated.Point, distance,
                    distance > policy.Tolerance ? "positive" : distance < -policy.Tolerance ? "negative" : "contact-candidate");
                if (distance > policy.Tolerance && (positive is null || distance > positive.SignedDistance)) positive = witness;
                else if (distance < -policy.Tolerance && (negative is null || distance < negative.SignedDistance)) negative = witness;
                else if (double.Abs(distance) <= policy.Tolerance && (contact is null || double.Abs(distance) < double.Abs(contact.SignedDistance))) contact = witness;
                if (evaluated.IsSingular) diagnostics.Add(new(GeometryQueryDiagnosticCode.SingularPatchEvaluation, $"Singular first jet observed at ({u:R}, {v:R})."));
            }
            catch (ArithmeticException ex) { diagnostics.Add(new(GeometryQueryDiagnosticCode.NonFiniteEvaluation, ex.Message)); }
        }
        if (negative is not null) witnesses.Add(negative); if (contact is not null) witnesses.Add(contact); if (positive is not null) witnesses.Add(positive);
        var classification = positive is not null && negative is not null ? SignedSideClassification.Crossing
            : positive is not null && contact is null ? SignedSideClassification.Positive
            : negative is not null && contact is null ? SignedSideClassification.Negative
            : SignedSideClassification.Unknown;
        if (!double.IsFinite(minimum)) { minimum = double.NaN; maximum = double.NaN; diagnostics.Add(new(GeometryQueryDiagnosticCode.EvidenceUnavailable, "No finite samples were available.")); classification = SignedSideClassification.Unknown; }
        return Result(classification, PredicateEvidenceKind.Sampled, patch, policy,
            double.IsFinite(minimum) ? minimum : null, double.IsFinite(maximum) ? maximum : null, null, null, witnesses,
            new(policy.SamplesU * policy.SamplesV, 0, 0, 0, 0), diagnostics);
    }

    private static SignedSideResult Certify(BoundedParametricPatch3 patch, Plane3 plane, SignedSidePolicy policy)
    {
        if (patch.PointExpression is null)
            return Result(SignedSideClassification.Unknown, PredicateEvidenceKind.Unknown, patch, policy, null, null, null, null, [], new(0, 0, 0, 1, 0),
                [new(GeometryQueryDiagnosticCode.UnsupportedCertifiedExpression, "Certified interval evaluation requires an authored expression tree; no sampled fallback was performed.")]);
        var n = plane.Normal.ToVector(); var p = patch.PointExpression;
        var scalar = SurfaceExpression.Subtract(
            SurfaceExpression.Add(SurfaceExpression.Add(Scale(p.X, n.X), Scale(p.Y, n.Y)), Scale(p.Z, n.Z)),
            SurfaceExpression.Length(n.Dot(plane.Origin - Point3D.Origin)));
        var queue = new Queue<Cell>(); queue.Enqueue(new(patch.Domain, 0));
        var resolved = 0; var unresolved = 0; var leaves = 0; var maxDepth = 0; var globalLower = double.PositiveInfinity; var globalUpper = double.NegativeInfinity;
        var hasPositive = false; var hasNegative = false; var budgetExhausted = false; var unsupported = false;
        SignedSideWitness? positiveWitness = null, negativeWitness = null;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue(); maxDepth = int.Max(maxDepth, cell.Depth);
            if (!IntervalEvaluator.TryEvaluate(scalar, cell.Domain, out var range)) { unsupported = true; unresolved++; break; }
            globalLower = double.Min(globalLower, range.Lower); globalUpper = double.Max(globalUpper, range.Upper);
            if (range.Lower > policy.Tolerance)
            {
                hasPositive = true; resolved++; leaves++;
                positiveWitness ??= CertifiedWitness(patch, plane, cell.Domain, "certified-positive-region");
                if (hasNegative) break;
                continue;
            }
            if (range.Upper < -policy.Tolerance)
            {
                hasNegative = true; resolved++; leaves++;
                negativeWitness ??= CertifiedWitness(patch, plane, cell.Domain, "certified-negative-region");
                if (hasPositive) break;
                continue;
            }
            if (cell.Depth >= policy.MaximumSubdivisionDepth || leaves + queue.Count + 4 > policy.MaximumLeafCount)
            { unresolved++; leaves++; budgetExhausted = true; continue; }
            foreach (var child in Subdivide(cell)) queue.Enqueue(child);
        }
        var classification = hasPositive && hasNegative ? SignedSideClassification.Crossing
            : unresolved == 0 && hasPositive ? SignedSideClassification.Positive
            : unresolved == 0 && hasNegative ? SignedSideClassification.Negative
            : SignedSideClassification.Unknown;
        var diagnostics = new List<GeometryQueryDiagnostic>();
        if (unsupported) diagnostics.Add(new(GeometryQueryDiagnosticCode.UnsupportedCertifiedExpression, "The authored scalar expression is outside the bounded interval evaluator or has an undefined interval operation."));
        if (budgetExhausted && classification == SignedSideClassification.Unknown) diagnostics.Add(new(GeometryQueryDiagnosticCode.SubdivisionBudgetExhausted, $"Certification remained inconclusive at depth {maxDepth} with leaf budget {policy.MaximumLeafCount}."));
        var evidence = classification == SignedSideClassification.Unknown ? PredicateEvidenceKind.Unknown : PredicateEvidenceKind.Certified;
        var witnesses = new List<SignedSideWitness>();
        if (negativeWitness is not null) witnesses.Add(negativeWitness);
        if (positiveWitness is not null) witnesses.Add(positiveWitness);
        return Result(classification, evidence, patch, policy, null, null,
            double.IsFinite(globalLower) ? globalLower : null, double.IsFinite(globalUpper) ? globalUpper : null, witnesses,
            new(0, leaves, resolved, unresolved, maxDepth), diagnostics);
    }

    private static SurfaceScalarExpression Scale(SurfaceScalarExpression value, double factor) =>
        SurfaceExpression.Multiply(value, SurfaceExpression.Number(factor));

    private static SignedSideWitness CertifiedWitness(BoundedParametricPatch3 patch, Plane3 plane, ParametricDomain domain, string kind)
    {
        var u = (domain.U.Minimum + domain.U.Maximum) / 2d; var v = (domain.V.Minimum + domain.V.Maximum) / 2d;
        var point = patch.Evaluate(u, v);
        return new(u, v, point.Point, plane.SignedDistance(point.Point), kind);
    }

    private static IEnumerable<Cell> Subdivide(Cell parent)
    {
        var u0 = parent.Domain.U.Minimum; var u1 = parent.Domain.U.Maximum; var um = (u0 + u1) / 2d;
        var v0 = parent.Domain.V.Minimum; var v1 = parent.Domain.V.Maximum; var vm = (v0 + v1) / 2d; var depth = parent.Depth + 1;
        yield return new(new(new(u0, um), new(v0, vm)), depth);
        yield return new(new(new(um, u1), new(v0, vm)), depth);
        yield return new(new(new(u0, um), new(vm, v1)), depth);
        yield return new(new(new(um, u1), new(vm, v1)), depth);
    }

    private static SignedSideResult Result(SignedSideClassification classification, PredicateEvidenceKind evidence, BoundedParametricPatch3 patch,
        SignedSidePolicy policy, double? observedMin, double? observedMax, double? certifiedMin, double? certifiedMax,
        IReadOnlyList<SignedSideWitness> witnesses, SignedSideStatistics statistics, IReadOnlyList<GeometryQueryDiagnostic> diagnostics) =>
        new(classification, evidence, policy, patch.Domain, patch.StableId, patch.Provenance, patch.Representation,
            observedMin, observedMax, certifiedMin, certifiedMax, witnesses, statistics, diagnostics);

    private sealed record Cell(ParametricDomain Domain, int Depth);
}

internal readonly record struct Interval(double Lower, double Upper)
{
    public static Interval Point(double value) => new(value, value);
    public static Interval Add(Interval a, Interval b) => Out(a.Lower + b.Lower, a.Upper + b.Upper);
    public static Interval Subtract(Interval a, Interval b) => Out(a.Lower - b.Upper, a.Upper - b.Lower);
    public static Interval Multiply(Interval a, Interval b)
    {
        var values = new[] { a.Lower * b.Lower, a.Lower * b.Upper, a.Upper * b.Lower, a.Upper * b.Upper };
        return Out(values.Min(), values.Max());
    }
    public static Interval Out(double lower, double upper) => new(double.BitDecrement(lower), double.BitIncrement(upper));
}

internal static class IntervalEvaluator
{
    public static bool TryEvaluate(SurfaceScalarExpression expression, ParametricDomain domain, out Interval result)
    {
        switch (expression)
        {
            case SurfaceScalarExpression.Constant c: result = Interval.Point(c.ConstantValue); return double.IsFinite(c.ConstantValue);
            case SurfaceScalarExpression.Parameter p: result = p.IsU ? new(domain.U.Minimum, domain.U.Maximum) : new(domain.V.Minimum, domain.V.Maximum); return true;
            case SurfaceScalarExpression.Sum s when TryEvaluate(s.Left, domain, out var sl) && TryEvaluate(s.Right, domain, out var sr): result = Interval.Add(sl, sr); return true;
            case SurfaceScalarExpression.Difference d when TryEvaluate(d.Left, domain, out var dl) && TryEvaluate(d.Right, domain, out var dr): result = Interval.Subtract(dl, dr); return true;
            case SurfaceScalarExpression.Product p when TryEvaluate(p.Left, domain, out var pl) && TryEvaluate(p.Right, domain, out var pr): result = Interval.Multiply(pl, pr); return Finite(result);
            case SurfaceScalarExpression.Quotient q when TryEvaluate(q.Left, domain, out var ql) && TryEvaluate(q.Right, domain, out var qr) && (qr.Lower > 0 || qr.Upper < 0):
                result = Interval.Multiply(ql, Interval.Out(1d / qr.Upper, 1d / qr.Lower)); return Finite(result);
            case SurfaceScalarExpression.IntegerPower p when TryEvaluate(p.Operand, domain, out var operand) && TryPower(operand, p.Exponent, out result): return true;
            case SurfaceScalarExpression.Sine s when TryEvaluate(s.Operand, domain, out var sinInput): result = Trig(sinInput, true); return true;
            case SurfaceScalarExpression.Cosine c when TryEvaluate(c.Operand, domain, out var cosInput): result = Trig(cosInput, false); return true;
            default: result = default; return false;
        }
    }

    private static bool TryPower(Interval value, int exponent, out Interval result)
    {
        if (exponent < 0 && value.Lower <= 0 && value.Upper >= 0) { result = default; return false; }
        if (exponent == 0) { result = Interval.Point(1); return true; }
        if (exponent < 0 && TryPower(value, -exponent, out var positive)) { result = Interval.Out(1d / positive.Upper, 1d / positive.Lower); return Finite(result); }
        var a = double.Pow(value.Lower, exponent); var b = double.Pow(value.Upper, exponent);
        var lower = double.Min(a, b); var upper = double.Max(a, b);
        if (exponent % 2 == 0 && value.Lower <= 0 && value.Upper >= 0) lower = 0;
        result = Interval.Out(lower, upper); return Finite(result);
    }

    private static Interval Trig(Interval value, bool sine)
    {
        if (value.Upper - value.Lower >= 2d * double.Pi) return new(-1, 1);
        var values = new List<double> { sine ? double.Sin(value.Lower) : double.Cos(value.Lower), sine ? double.Sin(value.Upper) : double.Cos(value.Upper) };
        var offset = sine ? double.Pi / 2d : 0d;
        var first = (int)double.Ceiling((value.Lower - offset) / double.Pi); var last = (int)double.Floor((value.Upper - offset) / double.Pi);
        for (var k = first; k <= last; k++) values.Add((k & 1) == 0 ? 1d : -1d);
        return Interval.Out(values.Min(), values.Max());
    }

    private static bool Finite(Interval value) => double.IsFinite(value.Lower) && double.IsFinite(value.Upper) && value.Lower <= value.Upper;
}
