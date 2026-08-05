using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Geometry.Curves;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Classification of the complete, transverse blind drilling tool volume.</summary>
public enum BlindDrillToolCorridorClassification
{
    CorridorProven, MouthUnsupported, ShaftBoundaryCrossing, ShaftVoidIntersection,
    ConeBoundaryCrossing, ConeVoidIntersection, TipOutsideMaterial,
    DisconnectedHostCorridor, TangentialAmbiguity, Unsupported
}

public enum SectionRectangleCorridorClassification { FullyContained, CrossesOuterBoundary, IntersectsInnerVoid, Tangential, Ambiguous, UnsupportedBoundary }

public sealed record SectionRectangleCorridorProof(
    double ZFrom, double ZTo, double AxisFrom, double AxisTo, double CrossFrom, double CrossTo,
    string ToolPart, SectionRectangleCorridorClassification Classification, IReadOnlyList<string> Provenance,
    string Detail);

public sealed record BlindDrillToolCorridorEvidence(
    string HoleId, string HostId, string ConstructionPlaneId, double[] Mouth, double[] Axis,
    double Radius, double ShaftDepth, double TipLength, double TotalDepth, double PointAngle,
    IReadOnlyList<SectionRectangleCorridorProof> ShaftSliceProofs,
    IReadOnlyList<SectionRectangleCorridorProof> ConeSliceProofs,
    double? RemainingWall, BlindDrillToolCorridorClassification Classification,
    IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Provenance);

/// <summary>
/// Bounded transverse (+/-X, +/-Y) blind-drill proof. At each world-Z slab it
/// proves a tool envelope in the slab's native XY material region. The shaft
/// envelope follows the exact circular YZ/XZ chord; the cone uses the enclosing
/// analytic rectangle for that slab. This is conservative for the cone and is
/// intentionally rejected rather than approximated when its enclosure cannot
/// be proven.
/// </summary>
internal static class TransverseBlindDrillToolCorridor
{
    private const double Tol = 1e-8;
    private const double EntryInset = 1e-5;

    public static BlindDrillToolCorridorEvidence Prove(AirHoleFeature feature, PrismaticSectionStackConstruction stack, AirConstructionPlaneHolePlacement placement)
    {
        var diagnostics = new List<string>(); var mouth = placement.WorldMouthCenter; var axis = placement.AxisZ.ToVector();
        if (feature.Termination is not AirHoleTermination.DrillPoint point || feature.EndCondition is AirHoleEndCondition.ThroughAll)
            return Fail("BlindDrillToolCorridorRequiresBlindDrillPoint", BlindDrillToolCorridorClassification.Unsupported);
        if (!SignedTransverse(axis, placement)) return Fail("BlindDrillToolCorridorOrientationUnsupported: admitted axes are signed world +/-X and +/-Y.", BlindDrillToolCorridorClassification.Unsupported);
        var tip = feature.Shaft.Radius / Math.Tan(point.PointAngleDegrees * Math.PI / 360d);
        var shaft = feature.EndCondition switch
        {
            AirHoleEndCondition.ShaftDepth d => d.Value,
            AirHoleEndCondition.TotalDepth d => d.Value - tip,
            _ => double.NaN
        };
        if (!double.IsFinite(shaft) || shaft < -Tol || tip <= Tol)
            return Fail("BlindDrillToolCorridorDepthInvalid", BlindDrillToolCorridorClassification.Unsupported);

        var total = shaft + tip; var radius = feature.Shaft.Radius;
        var radialZMin = mouth.Z - radius; var radialZMax = mouth.Z + radius;
        var slabs = stack.Slabs.Where(s => s.To > radialZMin + Tol && s.From < radialZMax - Tol).OrderBy(s => s.From).ToArray();
        if (!Covers(slabs, radialZMin, radialZMax)) return Fail("DisconnectedHostCorridor: host material slabs do not cover the tool Z support.", BlindDrillToolCorridorClassification.DisconnectedHostCorridor);

        var positive = axis.X > 0d || axis.Y > 0d; var xAxis = Math.Abs(axis.X) > .5d;
        var axialMouth = xAxis ? mouth.X : mouth.Y; var crossCenter = xAxis ? mouth.Y : mouth.X;
        var shaftEnd = axialMouth + (positive ? shaft : -shaft); var tipEnd = axialMouth + (positive ? total : -total);
        var shaftProofs = new List<SectionRectangleCorridorProof>(); var coneProofs = new List<SectionRectangleCorridorProof>();
        foreach (var slab in slabs)
        {
            var z0 = Math.Max(slab.From, radialZMin); var z1 = Math.Min(slab.To, radialZMax);
            var maxChord = Chord(radius, Math.Abs(NearestTo(mouth.Z, z0, z1) - mouth.Z));
            // Exclude only the declared mouth boundary from the interior query;
            // closure supplies the exact circular Mouth support separately in a
            // host-integrated plan. No alternative mouth is searched for here.
            var insideMouth = axialMouth + (positive ? EntryInset : -EntryInset);
            var shaftProof = QueryRectangle(slab.Region, z0, z1, insideMouth, shaftEnd,
                crossCenter - maxChord, crossCenter + maxChord, xAxis, "Shaft");
            shaftProofs.Add(shaftProof);

            // At fixed Z the exact cone is curved in the axial/cross plane. The
            // rectangle below is its analytic enclosing envelope: s is bounded
            // by L(1-|dz|/r), and the maximum cross chord occurs at s=0. A host
            // containing this rectangle necessarily contains the full cone slice.
            var dz = Math.Abs(NearestTo(mouth.Z, z0, z1) - mouth.Z);
            var coneLength = tip * Math.Max(0d, 1d - dz / radius);
            var coneEnd = shaftEnd + (positive ? coneLength : -coneLength);
            var coneProof = QueryRectangle(slab.Region, z0, z1, shaftEnd, coneEnd,
                crossCenter - maxChord, crossCenter + maxChord, xAxis, "DrillPoint");
            coneProofs.Add(coneProof);
        }

        var all = shaftProofs.Concat(coneProofs).ToArray();
        var classification = Classify(all, diagnostics);
        double? remaining = null;
        // The physical RemainingWall is direction-specific distance beyond the
        // tip and needs host-boundary correspondence. This proof does not invent
        // it from a slab or an arbitrary nearest boundary.
        if (classification == BlindDrillToolCorridorClassification.CorridorProven)
            diagnostics.Add("RemainingWallRequiresHostBoundaryCorrespondence: corridor is proven but no final host boundary distance was inferred.");
        return new(feature.FeatureId, feature.TargetBodyId ?? stack.Feature.Name, placement.ConstructionPlaneId,
            [mouth.X, mouth.Y, mouth.Z], [axis.X, axis.Y, axis.Z], radius, shaft, tip, total, point.PointAngleDegrees,
            shaftProofs, coneProofs, remaining, classification, diagnostics, ["PrismaticSectionStackConstruction", "ProfileArrangement2D", "TransverseYZorXZChord", "ConservativeAnalyticConeEnvelope", "NoTessellation"]);

        BlindDrillToolCorridorEvidence Fail(string diagnostic, BlindDrillToolCorridorClassification kind)
        {
            diagnostics.Add(diagnostic);
            return new(feature.FeatureId, feature.TargetBodyId ?? stack.Feature.Name, placement.ConstructionPlaneId,
                [mouth.X, mouth.Y, mouth.Z], [axis.X, axis.Y, axis.Z], feature.Shaft.Radius, 0d, 0d, 0d,
                feature.Termination is AirHoleTermination.DrillPoint p ? p.PointAngleDegrees : 0d, [], [], null, kind, diagnostics,
                ["PrismaticSectionStackConstruction", "NoBoxIntervalFallback"]);
        }
    }

    private static BlindDrillToolCorridorClassification Classify(IReadOnlyList<SectionRectangleCorridorProof> proofs, List<string> diagnostics)
    {
        var failure = proofs.FirstOrDefault(x => x.Classification != SectionRectangleCorridorClassification.FullyContained);
        if (failure is null) return BlindDrillToolCorridorClassification.CorridorProven;
        diagnostics.Add($"ToolCorridorFailure: part={failure.ToolPart}; z=[{failure.ZFrom:R},{failure.ZTo:R}]; axial=[{failure.AxisFrom:R},{failure.AxisTo:R}]; cross=[{failure.CrossFrom:R},{failure.CrossTo:R}]; detail={failure.Detail}.");
        return failure.Classification switch
        {
            SectionRectangleCorridorClassification.IntersectsInnerVoid => failure.ToolPart == "Shaft" ? BlindDrillToolCorridorClassification.ShaftVoidIntersection : BlindDrillToolCorridorClassification.ConeVoidIntersection,
            SectionRectangleCorridorClassification.CrossesOuterBoundary => failure.ToolPart == "Shaft" ? BlindDrillToolCorridorClassification.ShaftBoundaryCrossing : BlindDrillToolCorridorClassification.ConeBoundaryCrossing,
            SectionRectangleCorridorClassification.Tangential => BlindDrillToolCorridorClassification.TangentialAmbiguity,
            _ => BlindDrillToolCorridorClassification.Unsupported
        };
    }

    private static SectionRectangleCorridorProof QueryRectangle(PrismaticSectionRegion region, double z0, double z1, double a, double b, double c, double d, bool xAxis, string part)
    {
        var xmin = xAxis ? Math.Min(a, b) : Math.Min(c, d); var xmax = xAxis ? Math.Max(a, b) : Math.Max(c, d);
        var ymin = xAxis ? Math.Min(c, d) : Math.Min(a, b); var ymax = xAxis ? Math.Max(c, d) : Math.Max(a, b);
        var corners = new[] { (xmin, ymin), (xmax, ymin), (xmax, ymax), (xmin, ymax) };
        var outer = corners.Select(p => ProfileArrangementBuilder.PointInProfile(region.Outer, p)).ToArray();
        if (outer.Any(x => x == ArrangementPointLocation.OnBoundary)) return Proof(SectionRectangleCorridorClassification.Tangential, "outer-corner-on-boundary");
        if (outer.Any(x => x != ArrangementPointLocation.Inside)) return Proof(SectionRectangleCorridorClassification.CrossesOuterBoundary, "outer-corner-outside");
        foreach (var hole in region.Holes)
        {
            var locations = corners.Select(p => ProfileArrangementBuilder.PointInProfile(hole, p)).ToArray();
            if (locations.Any(x => x is ArrangementPointLocation.Inside or ArrangementPointLocation.OnBoundary)) return Proof(SectionRectangleCorridorClassification.IntersectsInnerVoid, "corner-in-inner-void");
        }
        foreach (var curve in Curves(region.Outer)) if (IntersectsOrLiesInside(curve, xmin, xmax, ymin, ymax, out var tangent)) return Proof(tangent ? SectionRectangleCorridorClassification.Tangential : SectionRectangleCorridorClassification.CrossesOuterBoundary, "outer-boundary-intersects-corridor");
        foreach (var hole in region.Holes)
            foreach (var curve in Curves(hole)) if (IntersectsOrLiesInside(curve, xmin, xmax, ymin, ymax, out var tangent)) return Proof(tangent ? SectionRectangleCorridorClassification.Tangential : SectionRectangleCorridorClassification.IntersectsInnerVoid, "inner-void-intersects-corridor");
        return Proof(SectionRectangleCorridorClassification.FullyContained, "exact-line-arc-boundary-clear");

        SectionRectangleCorridorProof Proof(SectionRectangleCorridorClassification classification, string detail) => new(z0, z1, a, b, c, d, part, classification, region.Provenance, detail);
    }

    private static IEnumerable<LineArcProfileCurve2D> Curves(ResolvedProfile2D profile) => profile.Loops.Single().Segments.Select(x => x.Geometry);
    private static bool IntersectsOrLiesInside(LineArcProfileCurve2D curve, double xmin, double xmax, double ymin, double ymax, out bool tangent)
    {
        tangent = false;
        foreach (var p in Candidates(curve)) if (p.X > xmin + Tol && p.X < xmax - Tol && p.Y > ymin + Tol && p.Y < ymax - Tol) return true;
        foreach (var edge in new[] { ((xmin, ymin), (xmax, ymin)), ((xmax, ymin), (xmax, ymax)), ((xmax, ymax), (xmin, ymax)), ((xmin, ymax), (xmin, ymin)) })
            if (Intersects(curve, edge.Item1, edge.Item2, out var isTangent)) { tangent |= isTangent; return true; }
        return false;
    }

    private static IEnumerable<(double X, double Y)> Candidates(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => [line.Start, line.End],
        LineArcCircularArc2D arc => new[] { arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians, 0d, Math.PI / 2d, Math.PI, 3d * Math.PI / 2d }
            .Where(angle => OnArc(arc, angle)).Select(angle => (arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle))),
        _ => []
    };

    private static bool Intersects(LineArcProfileCurve2D curve, (double X, double Y) a, (double X, double Y) b, out bool tangent)
    {
        tangent = false;
        if (curve is LineArcLineSegment2D line)
        {
            var r = (X: line.End.X - line.Start.X, Y: line.End.Y - line.Start.Y); var s = (X: b.X - a.X, Y: b.Y - a.Y); var cross = Cross(r, s);
            if (Math.Abs(cross) <= Tol) { tangent = Math.Abs(Cross((a.X - line.Start.X, a.Y - line.Start.Y), r)) <= Tol; return tangent; }
            var t = Cross((a.X - line.Start.X, a.Y - line.Start.Y), s) / cross; var u = Cross((a.X - line.Start.X, a.Y - line.Start.Y), r) / cross;
            return t >= -Tol && t <= 1d + Tol && u >= -Tol && u <= 1d + Tol;
        }
        if (curve is not LineArcCircularArc2D arc) return false;
        var dx = b.X - a.X; var dy = b.Y - a.Y; var fx = a.X - arc.Center.X; var fy = a.Y - arc.Center.Y;
        var aa = dx * dx + dy * dy; var bb = 2d * (fx * dx + fy * dy); var cc = fx * fx + fy * fy - arc.Radius * arc.Radius; var discriminant = bb * bb - 4d * aa * cc;
        if (discriminant < -Tol) return false; tangent = Math.Abs(discriminant) <= Tol; var root = Math.Sqrt(Math.Max(0d, discriminant));
        foreach (var t in root <= Tol ? new[] { -bb / (2d * aa) } : new[] { (-bb - root) / (2d * aa), (-bb + root) / (2d * aa) })
            if (t >= -Tol && t <= 1d + Tol && OnArc(arc, Math.Atan2(a.Y + t * dy - arc.Center.Y, a.X + t * dx - arc.Center.X))) return true;
        return false;
    }

    private static bool OnArc(LineArcCircularArc2D arc, double angle)
    {
        var delta = angle - arc.StartAngleRadians;
        if (arc.SweepAngleRadians >= 0d) while (delta < 0d) delta += 2d * Math.PI; else while (delta > 0d) delta -= 2d * Math.PI;
        var t = delta / arc.SweepAngleRadians; return t >= -Tol && t <= 1d + Tol;
    }
    private static bool Covers(IReadOnlyList<PrismaticSectionSlab> slabs, double from, double to)
    {
        var current = from; foreach (var slab in slabs) { if (slab.From > current + Tol) return false; current = Math.Max(current, slab.To); } return current >= to - Tol;
    }
    private static bool SignedTransverse(Aetheris.Kernel.Core.Math.Vector3D axis, AirConstructionPlaneHolePlacement placement)
    {
        var values = new[] { Math.Abs(axis.X), Math.Abs(axis.Y), Math.Abs(axis.Z) };
        return values.Count(x => Math.Abs(x - 1d) <= Tol) == 1 && values.Count(x => x <= Tol) == 2 && Math.Abs(axis.Z) <= Tol;
    }
    private static double NearestTo(double value, double low, double high) => Math.Clamp(value, low, high);
    private static double Chord(double radius, double zDistance) => Math.Sqrt(Math.Max(0d, radius * radius - zDistance * zDistance));
    private static double Cross((double X, double Y) a, (double X, double Y) b) => a.X * b.Y - a.Y * b.X;
}
