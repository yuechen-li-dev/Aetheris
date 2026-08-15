using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// First-class ownership for the semantic vertex shared by two directed profile edges.
/// Edge A ends at the corner and edge B starts there.  Operations consume a suffix of A
/// and a prefix of B; exact curve ordinals are descendants, never the corner identity.
/// </summary>
public sealed record SemanticCornerProfileIr(
    string CornerPath,
    string StableId,
    string EdgeAPath,
    string EdgeBPath,
    SemanticProfilePoint EdgeAStart,
    SemanticProfilePoint Corner,
    SemanticProfilePoint EdgeBEnd,
    SemanticCornerOperationIr Operation,
    string LocalFrame,
    string Provenance);

public abstract record SemanticCornerOperationIr(
    string Name, string StableId, string Kind, double SetbackA, double SetbackB, string SourceSpan);

public sealed record SemanticCornerChamferIr(
    string Name, string StableId, double SetbackA, double SetbackB, string SourceSpan)
    : SemanticCornerOperationIr(Name, StableId, "Chamfer", SetbackA, SetbackB, SourceSpan);

public sealed record SemanticCornerCutbackIr(
    string Name, string StableId, double SetbackA, double SetbackB, string SourceSpan)
    : SemanticCornerOperationIr(Name, StableId, "Cutback", SetbackA, SetbackB, SourceSpan);

public sealed record SemanticCornerTaperIr(
    string Name, string StableId, double SetbackA, double SetbackB, string SourceSpan)
    : SemanticCornerOperationIr(Name, StableId, "Taper", SetbackA, SetbackB, SourceSpan);

/// <summary>A rectangular material-removal step in the bounded corner u/v frame.</summary>
public sealed record SemanticCornerNotchIr(
    string Name, string StableId, double SetbackA, double SetbackB, string SourceSpan)
    : SemanticCornerOperationIr(Name, StableId, "NotchCorner", SetbackA, SetbackB, SourceSpan);

public sealed record ResolvedSemanticCornerProfileIr(
    SemanticCornerProfileIr Source,
    SemanticProfilePoint EdgeAEndpoint,
    SemanticProfilePoint EdgeBEndpoint,
    IReadOnlyList<ResolvedSemanticProfileCurveIr> CurveDescendants,
    string DeterministicHash,
    TimeSpan ResolutionTime)
{
    public double EdgeAConsumption => Source.Operation.SetbackA;
    public double EdgeBConsumption => Source.Operation.SetbackB;
    public IReadOnlyList<LineArcProfileCurve2D> ExactReplacementChain => CurveDescendants.Select(x => x.Geometry).ToArray();
}

public sealed record SemanticCornerProfileResolution(ResolvedSemanticCornerProfileIr? Corner, IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess => Corner is not null && Diagnostics.Count == 0;
}

/// <summary>
/// Deterministic lowering for fully specified authored corner topology.  There is no
/// candidate selection here: hard semantic and geometric constraints either admit the
/// single authored construction or reject it before contour assembly.
/// </summary>
public static class SemanticCornerProfileResolver
{
    private const double Tolerance = 1e-8;

    public static SemanticCornerProfileResolution Resolve(SemanticCornerProfileIr source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var clock = Stopwatch.StartNew();
        var diagnostics = new List<string>();
        var aVector = new SemanticProfilePoint(source.EdgeAStart.X - source.Corner.X, source.EdgeAStart.Y - source.Corner.Y);
        var bVector = new SemanticProfilePoint(source.EdgeBEnd.X - source.Corner.X, source.EdgeBEnd.Y - source.Corner.Y);
        var aLength = Length(aVector); var bLength = Length(bVector);
        if (!Positive(aLength) || !Positive(bLength)) diagnostics.Add($"semantic-corner-edge-degenerate:{source.CornerPath}:{source.EdgeAPath}:{source.EdgeBPath}");
        var cross = aVector.X * bVector.Y - aVector.Y * bVector.X;
        if (double.IsFinite(aLength) && double.IsFinite(bLength) && Math.Abs(cross) <= Tolerance * aLength * bLength)
            diagnostics.Add($"semantic-corner-edges-collinear:{source.CornerPath}:{source.EdgeAPath}:{source.EdgeBPath}");
        var operation = source.Operation;
        if (!Positive(operation.SetbackA) || !Positive(operation.SetbackB))
            diagnostics.Add($"semantic-corner-invalid-setback:{operation.StableId}:SetbackA={operation.SetbackA:R}:SetbackB={operation.SetbackB:R}");
        if (operation.SetbackA >= aLength - Tolerance || operation.SetbackB >= bLength - Tolerance)
            diagnostics.Add($"semantic-corner-consumption-out-of-bounds:{operation.StableId}:{source.EdgeAPath}={operation.SetbackA:R}/{aLength:R}:{source.EdgeBPath}={operation.SetbackB:R}/{bLength:R}");
        if (diagnostics.Count > 0) return new(null, diagnostics);

        var u = new SemanticProfilePoint(aVector.X / aLength, aVector.Y / aLength);
        var v = new SemanticProfilePoint(bVector.X / bLength, bVector.Y / bLength);
        var a = Add(source.Corner, u, operation.SetbackA);
        var b = Add(source.Corner, v, operation.SetbackB);
        IReadOnlyList<LineArcProfileCurve2D> curves = operation switch
        {
            SemanticCornerChamferIr or SemanticCornerCutbackIr or SemanticCornerTaperIr =>
                [new LineArcLineSegment2D(ToTuple(a), ToTuple(b))],
            SemanticCornerNotchIr =>
                [new LineArcLineSegment2D(ToTuple(a), ToTuple(new(a.X + v.X * operation.SetbackB, a.Y + v.Y * operation.SetbackB))),
                 new LineArcLineSegment2D(ToTuple(new(a.X + v.X * operation.SetbackB, a.Y + v.Y * operation.SetbackB)), ToTuple(b))],
            _ => []
        };
        if (curves.Count == 0) return new(null, [$"semantic-corner-operation-unsupported:{operation.StableId}:{operation.Kind}"]);

        var descendants = curves.Select((geometry, ordinal) => new ResolvedSemanticProfileCurveIr(
            $"{operation.StableId}.curve{ordinal:D2}", ordinal, geometry, $"lowered-from:{operation.StableId};corner:{source.CornerPath};edges:{source.EdgeAPath},{source.EdgeBPath}")).ToArray();
        clock.Stop();
        var hashText = string.Join("|", descendants.Select(x => $"{x.StableId}:{x.Geometry}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashText))).ToLowerInvariant();
        return new(new(source, a, b, descendants, hash, clock.Elapsed), []);
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > Tolerance;
    private static double Length(SemanticProfilePoint value) => Math.Sqrt(value.X * value.X + value.Y * value.Y);
    private static SemanticProfilePoint Add(SemanticProfilePoint origin, SemanticProfilePoint direction, double distance) => new(origin.X + direction.X * distance, origin.Y + direction.Y * distance);
    private static (double X, double Y) ToTuple(SemanticProfilePoint point) => (point.X, point.Y);
}
