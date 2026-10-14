using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Bounded two-section solid or hollow loft. The two named closed 2D outlines and their
/// construction planes are authoring inputs; SectionChain remains the sole
/// ruled BRep materializer. CommonRay pairs equal polar directions in the two
/// section frames, independently of each ellipse's parameter origin.
/// </summary>
public static class LoftAuthoringParser
{
    public static readonly IReadOnlyList<FirmamentAuthoringField> SolidFields = FirmamentSchemaAuthoringFields.For("Loft");
    public static readonly IReadOnlyList<FirmamentAuthoringField> HollowFields =
    [.. SolidFields,
        new("Thickness", "Length", true, "Hollow wall thickness."),
        new("SeamGap", "Angle", false, "Angular seam clearance.", "0.1deg")];

    public static bool TryGetAuthoringFields(string source, int offset, out IReadOnlyList<FirmamentAuthoringField> fields)
    {
        fields = [];
        foreach (var declaration in FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements).Where(d => d.Type.Name.Text == "Loft"))
        {
            var span = declaration.Body?.Span ?? declaration.Span;
            if (offset <= span.Start || offset > span.Start + span.Length) continue;
            fields = declaration.Type.Arguments.Count > 0 ? HollowFields : SolidFields;
            return true;
        }
        return false;
    }

    public static bool IsLoftSource(string source) => FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements).Any(d => d.Type.Name.Text == "Loft");

    public static SectionChainAuthoringResult Compile(string source, bool materialize = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        var frontend = FirmamentFrontendSchemas.Select(source);
        if (!frontend.IsSuccess) return new(false, null, null, frontend.Diagnostics);
        source = frontend.Source;
        source = FirmamentSourceSpelling.Normalize(source);
        var diagnostics = new List<string>();
        var syntax = FirmamentSyntaxParser.Parse(source);
        FirmamentV2Parser.BindMaterialDomainScope(syntax.Document, "Loft", diagnostics);
        diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code + ": " + d.Message));
        if (!FirmamentSyntaxTraversal.Statements(syntax.Document).OfType<FieldSyntax>().Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units") && FirmamentSyntaxValues.Reference(f.Value) == "mm"))
            diagnostics.Add("loft-units-invalid:millimetres-required");
        var allDeclarations = FirmamentV2Parser.MechanicalDeclarations(syntax.Document.Statements).ToArray();
        var declarations = allDeclarations.Where(d => d.Type.Name.Text == "Loft").ToArray();
        if (declarations.Length != 1) return Fail($"loft-count-invalid:expected=1:actual={declarations.Length}");
        if (allDeclarations.Any(d => d.Type.Name.Text == "SectionChain")) return Fail("loft-section-chain-ambiguous:one-surfacing-root-required");
        var declaration = declarations[0];
        var name = declaration.Name?.Text ?? "";
        var hollow = declaration.Type.Arguments.Count > 0;
        if (name.Length == 0 || declaration.Modifiers.Count > 0) diagnostics.Add("loft-header-invalid");
        if (hollow && (declaration.Type.Arguments.Count != 1 || declaration.Type.Arguments[0].Name is not null || declaration.Type.Arguments[0].Default is not null
            || FirmamentSyntaxValues.Reference(declaration.Type.Arguments[0].Value) != "Hollow"))
            diagnostics.Add("loft-construction-policy-unsupported");
        var fields = new FirmamentSyntaxFields(declaration, diagnostics);
        var rearProfileName = fields.Word("RearProfile"); var frontProfileName = fields.Word("FrontProfile");
        var rearFrameName = fields.Word("RearFrame"); var frontFrameName = fields.Word("FrontFrame");
        var rule = fields.Word("Rule"); var correspondence = fields.Word("Correspondence");
        if (rule != "Ruled") diagnostics.Add($"loft-rule-unsupported:{rule ?? "<missing>"}:expected=Ruled");
        if (correspondence != "CommonRay") diagnostics.Add($"loft-correspondence-unsupported:{correspondence ?? "<missing>"}:expected=CommonRay");
        if (rearProfileName is null || frontProfileName is null || rearFrameName is null || frontFrameName is null)
            diagnostics.Add("loft-section-input-missing:RearProfile,FrontProfile,RearFrame,FrontFrame-required");

        var thickness = hollow ? fields.Number("Thickness", "mm") : 0d;
        if (hollow && !(thickness > 0)) diagnostics.Add("loft-thickness-invalid:positive-length-required");
        var seamDegrees = hollow ? fields.Number("SeamGap", "deg", 0.1d) : 0.1d;
        if (!double.IsFinite(seamDegrees) || seamDegrees < 0.01 || seamDegrees > 5) diagnostics.Add("loft-seam-gap-invalid:range=0.01..5deg");
        var twistDegrees = fields.Number("Twist", "deg", 0d);
        if (!double.IsFinite(twistDegrees) || Math.Abs(twistDegrees) > 180) diagnostics.Add("loft-twist-invalid:range=-180..180deg");
        Vector3D reference = default;
        if (!fields.Vector("Reference", "", out var coordinates) || coordinates.Length != 3 || (reference = new(coordinates[0], coordinates[1], coordinates[2])).Length < 1e-8)
            diagnostics.Add("loft-reference-invalid:nonzero-world-direction-required");
        fields.Complete();
        if (diagnostics.Count > 0) return Fail(diagnostics.ToArray());

        var rear = ProfileAuthoringParser.ResolveNamedProfile(source, rearProfileName!, out var rearDiagnostics);
        var front = ProfileAuthoringParser.ResolveNamedProfile(source, frontProfileName!, out var frontDiagnostics);
        if (rear is null) diagnostics.AddRange(rearDiagnostics);
        if (front is null) diagnostics.AddRange(frontDiagnostics);
        var rearFrame = ProfileAuthoringParser.ResolveNamedConstructionPlane(source, rearFrameName!, diagnostics);
        var frontFrame = ProfileAuthoringParser.ResolveNamedConstructionPlane(source, frontFrameName!, diagnostics);
        if (rearFrame is null) diagnostics.Add($"loft-frame-unresolved:Rear:{rearFrameName}");
        if (frontFrame is null) diagnostics.Add($"loft-frame-unresolved:Front:{frontFrameName}");
        var rearOutline = rear is null ? null : Outline(rear, "Rear", thickness, diagnostics);
        var frontOutline = front is null ? null : Outline(front, "Front", thickness, diagnostics);
        if (diagnostics.Count > 0 || rearOutline is null || frontOutline is null || rearFrame is null || frontFrame is null)
            return Fail(diagnostics.ToArray());

        var rearSectionFrame = new SectionFrame(rearFrame.Origin, rearFrame.AxisX, rearFrame.AxisY, rearFrame.AxisZ);
        var frontSectionFrame = new SectionFrame(frontFrame.Origin, frontFrame.AxisX, frontFrame.AxisY, frontFrame.AxisZ);
        var rearProfile = MakeProfile("Rear", rearOutline, rearSectionFrame, reference, hollow, seamDegrees, thickness, diagnostics);
        var frontProfile = MakeProfile("Front", frontOutline, frontSectionFrame, reference, hollow, seamDegrees, thickness, diagnostics,
            twistDegrees * Math.PI / 180d);
        if (diagnostics.Count > 0 || rearProfile is null || frontProfile is null) return Fail(diagnostics.ToArray());

        var chain = new SectionChain(name,
            [new Section("Rear", rearSectionFrame, rearProfile), new Section("Front", frontSectionFrame, frontProfile)],
            [], SectionTransitionPolicy.Ruled, SectionTermination.Cap, SectionTermination.Cap, SectionChainContinuity.G0);
        var binding = new LoftAuthoredBinding(name, rearProfileName!, rearFrameName!, frontProfileName!, frontFrameName!,
            [reference.X, reference.Y, reference.Z], twistDegrees,
            declaration.Span, fields.AuthoredFields);
        if (!materialize) return new(true, chain, null, [], binding);
        var result = SectionChainMaterializer.Materialize(chain);
        if (!result.IsSuccess) diagnostics.AddRange(result.Diagnostics.Select(item => $"{item.Code}:{item.Message}"));
        return new(result.IsSuccess, chain, result, diagnostics, result.IsSuccess ? binding : null);

        SectionChainAuthoringResult Fail(params string[] messages) => new(false, null, null,
            diagnostics.Concat(messages).Distinct(StringComparer.Ordinal).ToArray());
    }

    private sealed record Outline2((double X, double Y) Center, double Major, double Minor, double Rotation);

    private static Outline2? Outline(ResolvedProfile2D profile, string section, double thickness, List<string> diagnostics)
    {
        if (profile.Loops.Count != 1 || !profile.Loops[0].IsOuter || profile.Loops[0].Segments.Count != 1)
        {
            diagnostics.Add($"loft-profile-unsupported:{section}:one-closed-circle-or-ellipse-required");
            return null;
        }
        var outline = profile.Loops[0].Segments[0].Geometry switch
        {
            LineArcFullCircle2D circle => new Outline2(circle.Center, circle.Radius, circle.Radius, 0),
            LineArcFullEllipse2D ellipse => new Outline2(ellipse.Center, ellipse.MajorRadius, ellipse.MinorRadius, ellipse.RotationRadians),
            _ => null
        };
        if (outline is null) diagnostics.Add($"loft-profile-unsupported:{section}:one-closed-circle-or-ellipse-required");
        else if (outline.Minor <= thickness + 1e-6)
            diagnostics.Add($"loft-thickness-exceeds-section:{section}");
        return outline;
    }

    private static SectionProfile? MakeProfile(string name, Outline2 outline, SectionFrame frame,
        Vector3D worldReference, bool hollow, double gapDegrees, double thickness, List<string> diagnostics, double twistRadians = 0)
    {
        var rx = worldReference.Dot(frame.XAxis.ToVector());
        var ry = worldReference.Dot(frame.YAxis.ToVector());
        var length = Math.Sqrt(rx * rx + ry * ry);
        if (length < 1e-8)
        {
            diagnostics.Add($"loft-reference-parallel-to-frame:{name}");
            return null;
        }
        var referenceAngle = Math.Atan2(ry, rx) + twistRadians;
        var gap = gapDegrees * Math.PI / 180d;
        var outerAngles = new[] { 0d, Math.PI/2, Math.PI, 3*Math.PI/2, hollow ? 2*Math.PI-gap : 2*Math.PI };
        var innerAngles = outerAngles.Reverse().ToArray();
        var spans = new List<SectionProfileSpan>(hollow ? 10 : 4);
        for (var i = 0; i < 4; i++)
            spans.Add(new($"O{i}", Cubic(outline, 0, referenceAngle + outerAngles[i], referenceAngle + outerAngles[i+1])));
        if (!hollow) return new(name + "Outline", spans, "O0");
        spans.Add(new("BridgeEnd", new SectionProfileCurve.Line(
            PointAt(outline, 0, referenceAngle + outerAngles[^1]),
            PointAt(outline, thickness, referenceAngle + innerAngles[0]))));
        for (var i = 0; i < 4; i++)
            spans.Add(new($"I{i}", Cubic(outline, thickness, referenceAngle + innerAngles[i], referenceAngle + innerAngles[i+1])));
        spans.Add(new("BridgeStart", new SectionProfileCurve.Line(
            PointAt(outline, thickness, referenceAngle), PointAt(outline, 0, referenceAngle))));
        return new(name + "Wall", spans, "O0");
    }

    private static SectionProfileCurve.PolynomialBSpline Cubic(Outline2 outline, double inset, double startRay, double endRay)
    {
        var start = EllipseParameter(outline, startRay, inset);
        var end = EllipseParameter(outline, endRay, inset);
        var clockwise = endRay < startRay;
        while (clockwise && end >= start) end -= 2*Math.PI;
        while (!clockwise && end <= start) end += 2*Math.PI;
        var k = 4d/3d * Math.Tan((end-start)/4d);
        var p0 = AtParameter(outline, inset, start);
        var p3 = AtParameter(outline, inset, end);
        var t0 = Tangent(outline, inset, start);
        var t3 = Tangent(outline, inset, end);
        var p1 = new SectionPoint2D(p0.X+k*t0.X, p0.Y+k*t0.Y);
        var p2 = new SectionPoint2D(p3.X-k*t3.X, p3.Y-k*t3.Y);
        return new(3, [p0,p1,p2,p3], [4,4], [0,1]);
    }

    private static SectionPoint2D PointAt(Outline2 outline, double inset, double ray)
        => AtParameter(outline, inset, EllipseParameter(outline, ray, inset));

    private static double EllipseParameter(Outline2 outline, double ray, double inset)
    {
        var relative = ray-outline.Rotation;
        return Math.Atan2(Math.Sin(relative)/(outline.Minor-inset), Math.Cos(relative)/(outline.Major-inset));
    }

    private static SectionPoint2D AtParameter(Outline2 outline, double inset, double parameter)
    {
        var x = (outline.Major-inset)*Math.Cos(parameter);
        var y = (outline.Minor-inset)*Math.Sin(parameter);
        var c = Math.Cos(outline.Rotation); var s = Math.Sin(outline.Rotation);
        return new(outline.Center.X+c*x-s*y, outline.Center.Y+s*x+c*y);
    }

    private static SectionPoint2D Tangent(Outline2 outline, double inset, double parameter)
    {
        var x = -(outline.Major-inset)*Math.Sin(parameter);
        var y = (outline.Minor-inset)*Math.Cos(parameter);
        var c = Math.Cos(outline.Rotation); var s = Math.Sin(outline.Rotation);
        return new(c*x-s*y, s*x+c*y);
    }

}
