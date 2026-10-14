using System.Diagnostics;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
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

internal sealed record FirmamentPlateauRequest(string Name, string Base, string Footprint, double Height, double Width, string? Seam);

/// <summary>One bounded semantic Plateau on an existing extruded planar support.</summary>
public static class PlateauAuthoring
{
    public static bool IsSource(string source) => FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements).Any(d => d.Type.Name.Text == "Plateau");

    internal static FirmamentPlateauRequest? Bind(string source, List<string> diagnostics)
    {
        var syntax = FirmamentSyntaxParser.Parse(source);
        diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code + ": " + d.Message));
        var declarations = FirmamentV2Parser.MechanicalDeclarations(syntax.Document.Statements).ToArray();
        var plateaus = declarations.Where(d => d.Type.Name.Text == "Plateau").ToArray();
        if (plateaus.Length != 1) { diagnostics.Add("plateau-count-invalid"); return null; }
        var declaration = plateaus[0];
        var fields = new FirmamentSyntaxFields(declaration, diagnostics);
        var name = declaration.Name?.Text;
        var baseName = fields.Reference("Base"); var footprint = fields.Reference("Footprint");
        var height = fields.Number("Height", "mm"); var width = fields.Number("Width", "mm");
        var seam = fields.Reference("Seam");
        if (fields.Reference("Continuity") != "G2" || fields.Reference("Inset") != "Homothetic")
            diagnostics.Add("plateau-requires-G2-and-Homothetic-inset");
        var extrusions = declarations.Where(d => d.Type.Name.Text == "Extrude").ToArray();
        if (extrusions.Length != 1 || baseName != extrusions[0].Name?.Text || declarations.Any(d => d.Type.Name.Text is "Modify" or "Compose" or "Boss" or "Pocket" or "Revolve" or "EdgeFinish"))
            diagnostics.Add("plateau-base-requires-one-unmodified-extrusion");
        if (!double.IsFinite(height) || !double.IsFinite(width)) diagnostics.Add("plateau-dimensions-invalid");
        if (name is null || footprint is null || declaration.Type.Arguments.Count > 0 || declaration.Modifiers.Count > 0) diagnostics.Add("plateau-header-or-footprint-invalid");
        fields.Complete();
        return diagnostics.Count == 0 ? new(name!, baseName!, footprint!, height, width, seam) : null;
    }

    internal static KernelResult<FirmamentStepExportResult> Compile(string source, FirmamentPlateauRequest? admitted = null, ResolvedProfile2D? admittedBase = null, double admittedHeight = 0)
    {
        KernelResult<FirmamentStepExportResult> Fail(IEnumerable<string> messages) => KernelResult<FirmamentStepExportResult>.Failure(
            messages.Select(m => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, m, "FirmamentV2.Plateau")).ToArray());
        var watch = Stopwatch.StartNew();
        var bindingDiagnostics = new List<string>();
        var request = admitted ?? Bind(source, bindingDiagnostics);
        if (request is null) return Fail(bindingDiagnostics);
        var name = request.Name; var height = request.Height; var width = request.Width;
        var baseProfile = admittedBase; var extrusionHeight = admittedHeight;
        if (baseProfile is null)
        {
            var parsed = ProfileAuthoringParser.Parse(source);
            if (parsed.Profile is null || parsed.Diagnostics.Count != 0) return Fail(parsed.Diagnostics);
            baseProfile = parsed.Profile; extrusionHeight = parsed.Height;
        }
        var footprint = ProfileAuthoringParser.ResolveNamedProfile(source, request.Footprint, out var diagnostics);
        if (footprint is null || diagnostics.Count != 0) return Fail(diagnostics);
        if (footprint.Loops.Count != 1 || footprint.PlaneFrame != baseProfile.PlaneFrame) return Fail(["plateau-footprint-frame-or-loops-unsupported"]);
        var baseBody = LineArcProfileExtrudeEmitter.TryEmit(baseProfile, extrusionHeight);
        if (baseBody.Body is null) return Fail(baseBody.Diagnostics);
        var frame = baseProfile.EffectiveConstructionPlane;
        var origin = frame.Origin + frame.AxisZ.ToVector() * (baseProfile.LocalEndDepth ?? extrusionHeight);
        var candidates = baseBody.Body.Bindings.FaceBindings.Where(b => b.Orientation.IsAlignedWithSurface && baseBody.Body.Geometry.GetSurface(b.SurfaceGeometryId).Plane is { } p &&
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
        var seam = request.Seam ?? spans.FirstOrDefault(s => s.Curve is SectionProfileCurve.Line)?.SpanId ?? spans[0].SpanId;
        var contactWatch = Stopwatch.StartNew();
        var contacts = PlanarPlateauContacts.Build(name, new(footprint.Name, spans, seam), new(origin, frame.AxisX, frame.AxisY, frame.AxisZ),
            height, width, request.Base + ".Top", name + ".Top");
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
