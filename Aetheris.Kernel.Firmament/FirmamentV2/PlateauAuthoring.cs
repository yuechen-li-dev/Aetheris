using System.Diagnostics;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentPlateauReport(PlanarPlateauContactPlan Contacts,
    IReadOnlyList<PlanarPlateauFaceIdentity> Faces, BrepPcurveEvidence Pcurves,
    int FaceCount, int EdgeCount, int VertexCount, bool ReimportManifold,
    double ContactGenerationMs, double TrimAndBrepGenerationMs, double TotalGenerationMs,
    string WidthLaw = "ExactHomotheticShortAxisInset");

/// <summary>One bounded semantic Plateau on an existing extruded planar support.</summary>
public static class PlateauAuthoring
{
    private static readonly Regex Header = new(@"\bPlateau\s+(?<name>[A-Za-z_]\w*)\s*\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant);
    public static bool IsSource(string source) => Regex.IsMatch(source, @"\bPlateau\s+[A-Za-z_]\w*\s*\{");

    internal static KernelResult<FirmamentStepExportResult> Compile(string source)
    {
        KernelResult<FirmamentStepExportResult> Fail(IEnumerable<string> messages) => KernelResult<FirmamentStepExportResult>.Failure(
            messages.Select(m => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, m, "FirmamentV2.Plateau")).ToArray());
        var watch = Stopwatch.StartNew(); var declarations = Header.Matches(source).Cast<Match>().ToArray();
        if (declarations.Length != 1) return Fail(["plateau-count-invalid"]);
        var declaration = declarations[0]; var name = declaration.Groups["name"].Value; var body = declaration.Groups["body"].Value;
        var fields = Regex.Matches(body, @"\b(?<key>[A-Za-z_]\w*)\s*:").Select(m => m.Groups["key"].Value).ToArray();
        var allowed = new[] { "Base", "Footprint", "Height", "Width", "Inset", "Continuity", "Seam" };
        if (fields.Any(f => !allowed.Contains(f)) || fields.Distinct().Count() != fields.Length)
            return Fail(["plateau-unknown-or-duplicate-property"]);
        string? Field(string field) => ProfileAuthoringParser.Property(body, field);
        if (Field("Continuity") != "G2" || Field("Inset") != "Homothetic") return Fail(["plateau-requires-G2-and-Homothetic-inset"]);
        var extrusions = Regex.Matches(source, @"\bExtrude\s+(?<name>[A-Za-z_]\w*)\s*\{");
        if (extrusions.Count != 1 || Field("Base") != extrusions[0].Groups["name"].Value ||
            Regex.IsMatch(source, @"\b(?:Modify|Compose|Boss|Pocket|Revolve|EdgeFinish)\s+")) return Fail(["plateau-base-requires-one-unmodified-extrusion"]);
        if (!ProfileAuthoringParser.TryMeasure(Field("Height") ?? "", "mm", out var height) ||
            !ProfileAuthoringParser.TryMeasure(Field("Width") ?? "", "mm", out var width)) return Fail(["plateau-dimensions-invalid"]);
        var parsed = ProfileAuthoringParser.Parse(source);
        if (parsed.Profile is null || parsed.Diagnostics.Count != 0) return Fail(parsed.Diagnostics);
        var footprint = ProfileAuthoringParser.ResolveNamedProfile(source, Field("Footprint") ?? "", out var diagnostics);
        if (footprint is null || diagnostics.Count != 0) return Fail(diagnostics);
        if (footprint.Loops.Count != 1 || footprint.PlaneFrame != parsed.Profile.PlaneFrame) return Fail(["plateau-footprint-frame-or-loops-unsupported"]);
        var baseBody = LineArcProfileExtrudeEmitter.TryEmit(parsed.Profile, parsed.Height);
        if (baseBody.Body is null) return Fail(baseBody.Diagnostics);
        var frame = parsed.Profile.EffectiveConstructionPlane;
        var origin = frame.Origin + frame.AxisZ.ToVector() * (parsed.Profile.LocalEndDepth ?? parsed.Height);
        var candidates = baseBody.Body.Bindings.FaceBindings.Where(b => b.SameSense && baseBody.Body.Geometry.GetSurface(b.SurfaceGeometryId).Plane is { } p &&
            (p.Normal.ToVector() - frame.AxisZ.ToVector()).Length < 1e-10 && Math.Abs((p.Origin - origin).Dot(frame.AxisZ.ToVector())) < 1e-8).ToArray();
        if (candidates.Length != 1) return Fail(["plateau-base-face-unresolved"]);
        var spans = new List<SectionProfileSpan>();
        foreach (var segment in footprint.Loops[0].Segments)
        {
            SectionProfileCurve? curve = segment.Geometry switch
            {
                LineArcLineSegment2D line => new SectionProfileCurve.Line(new(line.Start.X, line.Start.Y), new(line.End.X, line.End.Y)),
                LineArcCubicBezier2D c => new SectionProfileCurve.PolynomialBSpline(3,
                    [new(c.Start.X, c.Start.Y), new(c.Control1.X, c.Control1.Y), new(c.Control2.X, c.Control2.Y), new(c.End.X, c.End.Y)], [4, 4], [0, 1]),
                _ => null
            };
            if (curve is null) return Fail(["plateau-footprint-curve-unsupported:" + segment.Name]);
            spans.Add(new(segment.Name, curve));
        }
        var seam = Field("Seam") ?? spans.FirstOrDefault(s => s.Curve is SectionProfileCurve.Line)?.SpanId ?? spans[0].SpanId;
        var contactWatch = Stopwatch.StartNew();
        var contacts = PlanarPlateauContacts.Build(name, new(footprint.Name, spans, seam), new(origin, frame.AxisX, frame.AxisY, frame.AxisZ),
            height, width, Field("Base") + ".Top", name + ".Top");
        contactWatch.Stop();
        if (!contacts.IsSuccess) return Fail(contacts.Diagnostics);
        var trimWatch = Stopwatch.StartNew();
        var materialized = PlanarPlateauMaterializer.Apply(baseBody.Body, candidates[0].FaceId, contacts.Plan!);
        trimWatch.Stop();
        if (!materialized.IsSuccess) return Fail(materialized.Diagnostics);
        var step = Step242Exporter.ExportBody(materialized.Body!, new Step242ExportOptions { ProductName = name });
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var imported = Step242Importer.ImportBody(step.Value);
        if (!imported.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(imported.Diagnostics);
        var manifold = imported.Value.Topology.Edges.All(e => imported.Value.Topology.Coedges.Count(c => c.EdgeId == e.Id) == 2);
        if (!manifold) return Fail(["plateau-step-reimport-not-manifold"]);
        var report = new FirmamentPlateauReport(contacts.Plan!, materialized.Faces, materialized.Pcurves!,
            materialized.Body!.Topology.Faces.Count(), materialized.Body.Topology.Edges.Count(), materialized.Body.Topology.Vertices.Count(), manifold,
            contactWatch.Elapsed.TotalMilliseconds, trimWatch.Elapsed.TotalMilliseconds, watch.Elapsed.TotalMilliseconds);
        return KernelResult<FirmamentStepExportResult>.Success(new(step.Value, name, 0, "plateau", "Plateau", Plateau: report));
    }
}
