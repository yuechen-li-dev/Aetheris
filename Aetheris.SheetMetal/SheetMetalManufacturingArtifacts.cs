using System.Globalization;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalFlatBodyResult(
    bool IsSuccess,
    BrepBody? Body,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics);

/// <summary>Manufacturing artifact lowering from one authoritative flat-pattern IR.</summary>
public static class SheetMetalManufacturingArtifacts
{
    public static SheetMetalFlatBodyResult BuildFlatBody(SheetMetalPartIr part, SheetMetalFlatPatternIr flat)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(flat);
        if (flat.Status is FlatPatternStatus.Unsupported or FlatPatternStatus.Overlapping)
            return Failure("Flat STEP requires a non-overlapping, supported flat pattern.");

        var profiles = new Dictionary<string, ResolvedProfile2D>(StringComparer.Ordinal);
        var operations = new List<PrismaticProfileOperation>();
        var shift = flat.Bounds is null ? new SheetPoint2(10, 10) : new SheetPoint2(10 - flat.Bounds.MinX, 10 - flat.Bounds.MinY);
        var materialRegions = flat.Regions2D.Where(r => r.Boundary.Count >= 3).OrderBy(r => r.StableId, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < materialRegions.Length; index++)
        {
            var region = materialRegions[index];
            var name = $"material_{index:D3}";
            profiles[name] = region.ExactContour is not null ? Profile(name,region.ExactContour,shift) : Profile(name, region.Boundary, shift);
            operations.Add(new(name, index == 0 ? PrismaticProfileIntent.Base : PrismaticProfileIntent.Add, name, 0, part.Thickness,
                region.Kind == SheetRegionKind.CylindricalBend ? "NeutralAxisBendStrip" : "FlatSheetRegion", region.SourceRegionId));
        }
        for (var index = 0; index < flat.CutLoops.Count; index++)
        {
            var cut = flat.CutLoops[index];
            if (cut.Boundary.Count < 3) continue;
            var name = $"cut_{index:D3}";
            var cutFeature=part.Features.FirstOrDefault(f=>f.StableId==cut.FeatureId);
            profiles[name] = cut.ExactContour is not null ? Profile(name,cut.ExactContour,shift) : cutFeature is { Kind:SheetFeatureKind.CircularHole,Diameter:not null }
                ? CircleProfile(name,new(cut.Boundary.Average(p=>p.X),cut.Boundary.Average(p=>p.Y)),cutFeature.Diameter.Value/2,shift)
                : Profile(name, cut.Boundary, shift);
            operations.Add(new(name, PrismaticProfileIntent.Remove, name, 0, part.Thickness, "ThroughCut", cut.FeatureId, cut.FeatureId, cut.Kind.ToString()));
        }
        foreach(var relief in (flat.ReliefLoops??[]).OrderBy(x=>x.ReliefId,StringComparer.Ordinal))
        {
            var name=$"relief_{Safe(relief.ReliefId)}";profiles[name]=Profile(name,relief.ExactContour,shift);
            operations.Add(new(name,PrismaticProfileIntent.Remove,name,0,part.Thickness,"CornerRelief",relief.ReliefId,relief.ReliefId,relief.Kind.ToString()));
        }
        if (materialRegions.Length == 0) return Failure("Flat pattern contains no bounded material regions.");

        var placement = new PrismaticProfilePlacement("FlatPatternPlacement", 0, 0, 0, "XY", "+Z", "+X", true);
        var feature = new PrismaticProfileCompositionFeature(
            $"flat_{Safe(part.StableId)}", "XY", "+Z", placement, operations, [0, part.Thickness],
            $"SheetMetalFlatPatternIr:{flat.DeterministicHash}");
        var parsed = new PrismaticProfileCompositionParseResult(feature, profiles, []);
        var stack = PrismaticSectionStackCompiler.Normalize(parsed, out var normalizeDiagnostics);
        if (stack is null)
            return Failure("Flat profile union could not be normalized: " + string.Join("; ", normalizeDiagnostics));
        var emission = PrismaticSectionStackEmitter.Emit(stack);
        if (emission.Body is null)
            return Failure("Flat profile BRep materialization failed: " + string.Join("; ", emission.Diagnostics));
        var preflight = BrepExportPreflight.Validate(emission.Body);
        if (!preflight.IsValid)
            return Failure("Flat profile BRep failed STEP preflight: " + string.Join("; ", preflight.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        return new(true, emission.Body, emission.Diagnostics.Select(d => new SheetMetalDiagnostic("sheetmetal-flat-step-note", SheetMetalDiagnosticSeverity.Information, d)).ToArray());
    }

    public static bool WriteFlatStep(string path, SheetMetalPartIr part, SheetMetalFlatPatternIr flat, out IReadOnlyList<SheetMetalDiagnostic> diagnostics)
    {
        var body = BuildFlatBody(part, flat);
        if (!body.IsSuccess || body.Body is null) { diagnostics = body.Diagnostics; return false; }
        var exported = Step242Exporter.ExportBody(body.Body, new Step242ExportOptions
        {
            ProductId = Safe(part.StableId) + "_flat",
            ProductName = part.StableId + " flat pattern",
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce
        });
        if (!exported.IsSuccess)
        {
            diagnostics = exported.Diagnostics.Select(d => new SheetMetalDiagnostic("sheetmetal-flat-step-export-failed", SheetMetalDiagnosticSeverity.Error, d.Message)).ToArray();
            return false;
        }
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, exported.Value);
        diagnostics = body.Diagnostics;
        return true;
    }

    public static string WriteRecoveredFirmament(SheetMetalPartIr part, string sourceStepPath)
    {
        ArgumentNullException.ThrowIfNull(part);
        var c = CultureInfo.InvariantCulture;
        var b = new StringBuilder();
        b.AppendLine("// Generated recovered Sheet Metal intent. Source STEP geometry is referenced, not embedded or mutated.");
        b.Append("SheetMetal ").Append(Safe(part.StableId)).AppendLine(" {");
        b.Append("  Thickness: ").Append(part.Thickness.ToString("R", c)).AppendLine("mm;");
        b.Append("  KFactor: ").Append(part.FlatPatternPolicy.KFactor.ToString("R", c)).AppendLine(";");
        b.Append("  RecoveryStatus: ").Append(part.RecognitionStatus).AppendLine(";");
        b.Append("  RecoverySource: \"").Append(Escape(sourceStepPath.Replace('\\', '/'))).AppendLine("\";");
        b.Append("  BaseRegion: \"").Append(Escape(part.BaseRegionId)).AppendLine("\";");
        foreach (var region in part.Regions.OrderBy(r => r.StableId, StringComparer.Ordinal))
        {
            b.Append("  RecoveredRegion \"").Append(Escape(region.StableId)).AppendLine("\" {");
            b.Append("    Kind: ").Append(region.Kind).AppendLine(";");
            b.Append("    Area: ").Append(region.ApproximateArea.ToString("R", c)).AppendLine("mm2;");
            if (region.Plane is { } p)
            {
                Field("PlaneOrigin", p.Origin); VectorField("PlaneNormal", p.Normal); VectorField("PlaneU", p.UAxis); VectorField("PlaneV", p.VAxis);
                b.Append("    MaterialPositiveSide: ").Append(p.MaterialPositiveSide.ToString().ToLowerInvariant()).AppendLine(";");
            }
            if (region.Cylinder is { } cylinder)
            {
                Field("AxisOrigin", cylinder.AxisOrigin); VectorField("AxisDirection", cylinder.AxisDirection);
                Scalar("MidRadius", cylinder.GeometricMidRadius, "mm"); Scalar("InsideRadius", cylinder.InsideRadius, "mm");
                Scalar("AngularSpan", cylinder.AngularSpanRadians * 180 / Math.PI, "deg"); Scalar("AxisLength", cylinder.AxisLength, "mm");
                b.Append("    MaterialOutside: ").Append(cylinder.MaterialOutside.ToString().ToLowerInvariant()).AppendLine(";");
            }
            Points("Boundary", region.Boundary3D); Ints("SourceFaces", region.Source.FaceIds); b.AppendLine("  }");
        }
        foreach (var bend in part.Bends.OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            b.Append("  RecoveredBend \"").Append(Escape(bend.StableId)).AppendLine("\" {");
            Field("AxisOrigin", bend.AxisOrigin); VectorField("AxisDirection", bend.AxisDirection); Scalar("Angle", bend.BendAngleRadians * 180 / Math.PI, "deg");
            Scalar("InsideRadius", bend.InsideRadius, "mm"); b.Append("    Direction: ").Append(bend.Direction).AppendLine(";");
            b.Append("    Between: [\"").Append(Escape(bend.AdjacentRegionA)).Append("\", \"").Append(Escape(bend.AdjacentRegionB)).AppendLine("\"];");
            Ints("SourceFaces", bend.Source.FaceIds); b.AppendLine("  }");
        }
        foreach (var cut in part.Features.OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            b.Append("  RecoveredCut \"").Append(Escape(cut.StableId)).AppendLine("\" {");
            b.Append("    Kind: ").Append(cut.Kind).AppendLine(";"); b.Append("    On: \"").Append(Escape(cut.OwningRegionId)).AppendLine("\";"); Field("Center", cut.Center);
            if (cut.Diameter is { } diameter) Scalar("Diameter", diameter, "mm"); Points("Boundary", cut.Boundary3D); Ints("SourceFaces", cut.Source.FaceIds); b.AppendLine("  }");
        }
        b.AppendLine("}"); return b.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);

        void Scalar(string name, double value, string unit) => b.Append("    ").Append(name).Append(": ").Append(value.ToString("R", c)).Append(unit).AppendLine(";");
        void Field(string name, Point3D p) => b.Append("    ").Append(name).Append(": (").Append(p.X.ToString("R", c)).Append("mm, ").Append(p.Y.ToString("R", c)).Append("mm, ").Append(p.Z.ToString("R", c)).AppendLine("mm);");
        void VectorField(string name, Aetheris.Kernel.Core.Math.Vector3D v) => b.Append("    ").Append(name).Append(": (").Append(v.X.ToString("R", c)).Append(", ").Append(v.Y.ToString("R", c)).Append(", ").Append(v.Z.ToString("R", c)).AppendLine(");");
        void Points(string name, IReadOnlyList<Aetheris.Kernel.Core.Math.Point3D> points) { b.Append("    ").Append(name).Append(": ["); b.Append(string.Join(", ", points.Select(p => $"({p.X.ToString("R", c)}mm, {p.Y.ToString("R", c)}mm, {p.Z.ToString("R", c)}mm)"))); b.AppendLine("];"); }
        void Ints(string name, IReadOnlyList<int> values) => b.Append("    ").Append(name).Append(": [").Append(string.Join(", ", values)).AppendLine("];");
    }

    private static ResolvedProfile2D Profile(string name, IReadOnlyList<SheetPoint2> source, SheetPoint2 shift)
    {
        var cleaned = source.Select(p => new SheetPoint2(Clean(p.X + shift.X), Clean(p.Y + shift.Y))).ToArray();
        var points = SignedArea(cleaned) >= 0 ? cleaned : cleaned.Reverse().ToArray();
        var segments = points.Select((point, index) => new ResolvedProfileSegment2D($"s{index:D3}", new LineArcLineSegment2D((point.X, point.Y), (points[(index + 1) % points.Length].X, points[(index + 1) % points.Length].Y)), new($"{name}:s{index:D3}", name, name, "SheetMetalFlatPatternIr", "XY"))).ToArray();
        return new(name, "XY", [new("Outer", true, segments)]);
    }

    private static ResolvedProfile2D Profile(string name,PlanarContour2 contour,SheetPoint2 shift)
    {
        LineArcProfileCurve2D Shift(LineArcProfileCurve2D curve)=>curve switch
        {
            LineArcLineSegment2D line=>new LineArcLineSegment2D((Clean(line.Start.X+shift.X),Clean(line.Start.Y+shift.Y)),(Clean(line.End.X+shift.X),Clean(line.End.Y+shift.Y))),
            LineArcCircularArc2D arc=>arc with { Center=(Clean(arc.Center.X+shift.X),Clean(arc.Center.Y+shift.Y)) },
            LineArcFullCircle2D circle=>circle with { Center=(Clean(circle.Center.X+shift.X),Clean(circle.Center.Y+shift.Y)) },
            _=>throw new NotSupportedException("Flat manufacturing profiles support exact lines, arcs, and circles.")
        };
        ResolvedProfileLoop2D Loop(PlanarContourLoop2 loop,int loopIndex)=>new(loop.StableId,loop.IsOuter,loop.Segments.Select((segment,index)=>new ResolvedProfileSegment2D($"l{loopIndex:D2}s{index:D3}",Shift(segment.Geometry),segment.Provenance with { StableId=$"{name}:l{loopIndex:D2}s{index:D3}",Derivation=$"{segment.Provenance.Derivation}; flat manufacturing shift" })).ToArray());
        return new(name,contour.PlaneFrame,contour.Loops.Select(Loop).ToArray());
    }

    private static ResolvedProfile2D CircleProfile(string name,SheetPoint2 center,double radius,SheetPoint2 shift)
    {
        var c=(Clean(center.X+shift.X),Clean(center.Y+shift.Y));
        ResolvedProfileSegment2D Segment(string id,double start)=>new(id,new LineArcCircularArc2D(c,radius,start,Math.PI),new($"{name}:{id}",name,name,"SheetMetalFlatPatternIr analytic circle","XY"));
        return new(name,"XY",[new("Outer",true,[Segment("semicircle_0",0),Segment("semicircle_1",Math.PI)])]);
    }

    private static double SignedArea(IReadOnlyList<SheetPoint2> points) { var sum = 0d; for (var i = 0; i < points.Count; i++) { var q = points[(i + 1) % points.Count]; sum += points[i].X * q.Y - q.X * points[i].Y; } return sum / 2d; }
    private static double Clean(double value) => Math.Abs(value) < 1e-9 ? 0d : value;
    private static string Safe(string value) { var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray(); var result = new string(chars); return result.Length > 0 && (char.IsLetter(result[0]) || result[0] == '_') ? result : "Recovered_" + result; }
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    private static SheetMetalFlatBodyResult Failure(string message) => new(false, null, [new("sheetmetal-flat-step-failed", SheetMetalDiagnosticSeverity.Error, message)]);
}
