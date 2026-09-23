using System.Globalization;
using System.Text.RegularExpressions;
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
    private static readonly Regex Header = new(@"\bLoft(?:\s*<\s*(?<kind>Hollow)\s*>)?\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);

    public static readonly IReadOnlyList<FirmamentAuthoringField> SolidFields =
    [
        new("RearProfile", "Profile", true, "Closed rear section profile."),
        new("RearFrame", "ConstructionPlane", true, "Placement of the rear section."),
        new("FrontProfile", "Profile", true, "Closed front section profile."),
        new("FrontFrame", "ConstructionPlane", true, "Placement of the front section."),
        new("Rule", "Enum", true, "Section interpolation rule.", null, ["Ruled"]),
        new("Correspondence", "Enum", true, "Point correspondence between sections.", null, ["CommonRay"]),
        new("Reference", "Vector3", false, "Reference ray for section correspondence."),
        new("Twist", "Angle", false, "Relative section rotation.", "0deg")
    ];
    public static readonly IReadOnlyList<FirmamentAuthoringField> HollowFields =
    [.. SolidFields,
        new("Thickness", "Length", true, "Hollow wall thickness."),
        new("SeamGap", "Angle", false, "Angular seam clearance.", "0.1deg")];

    public static bool TryGetAuthoringFields(string source, int offset, out IReadOnlyList<FirmamentAuthoringField> fields)
    {
        fields = [];
        foreach (Match match in Header.Matches(source))
        {
            var open = match.Index + match.Length - 1;
            if (offset <= open) continue;
            var close = MatchingBrace(source, open);
            if (close >= 0 && offset > close) continue;
            fields = match.Groups["kind"].Success ? HollowFields : SolidFields;
            return true;
        }
        return false;
    }

    public static bool IsLoftSource(string source) => Header.IsMatch(source);

    public static SectionChainAuthoringResult Compile(string source, bool materialize = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<string>();
        if (!Regex.IsMatch(source, @"\bUnits\s*:\s*mm\b", RegexOptions.CultureInvariant))
            diagnostics.Add("loft-units-invalid:millimetres-required");
        var declarations = Header.Matches(source).Cast<Match>().ToArray();
        if (declarations.Length != 1)
            return Fail($"loft-count-invalid:expected=1:actual={declarations.Length}");
        if (Regex.IsMatch(source, @"\bSectionChain\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant))
            return Fail("loft-section-chain-ambiguous:one-surfacing-root-required");

        var declaration = declarations[0];
        var open = source.IndexOf('{', declaration.Index + declaration.Length - 1);
        var close = MatchingBrace(source, open);
        if (close < 0) return Fail("loft-block-unclosed");
        var body = source[(open + 1)..close];
        var name = declaration.Groups["name"].Value;
        var hollow = declaration.Groups["kind"].Success;
        var allowed = (hollow ? HollowFields : SolidFields).Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var fields = Regex.Matches(body, @"(?m)^\s*(?<key>[A-Za-z_]\w*)\s*:", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(match => match.Groups["key"].Value).ToArray();
        foreach (var field in fields.Where(field => !allowed.Contains(field)))
            diagnostics.Add($"loft-field-unknown:{field}");
        foreach (var field in fields.GroupBy(field => field, StringComparer.Ordinal).Where(group => group.Count() > 1))
            diagnostics.Add($"loft-field-duplicate:{field.Key}");
        var rearProfileName = Field(body, "RearProfile");
        var frontProfileName = Field(body, "FrontProfile");
        var rearFrameName = Field(body, "RearFrame");
        var frontFrameName = Field(body, "FrontFrame");
        var rule = Field(body, "Rule");
        var correspondence = Field(body, "Correspondence");
        if (rule != "Ruled") diagnostics.Add($"loft-rule-unsupported:{rule ?? "<missing>"}:expected=Ruled");
        if (correspondence != "CommonRay") diagnostics.Add($"loft-correspondence-unsupported:{correspondence ?? "<missing>"}:expected=CommonRay");
        if (rearProfileName is null || frontProfileName is null || rearFrameName is null || frontFrameName is null)
            diagnostics.Add("loft-section-input-missing:RearProfile,FrontProfile,RearFrame,FrontFrame-required");

        var thickness = 0d;
        var thicknessText = Scalar(body, "Thickness");
        if (hollow && (thicknessText is null || !ProfileAuthoringParser.TryMeasure(thicknessText, "mm", out thickness) || thickness <= 0))
            diagnostics.Add("loft-thickness-invalid:positive-length-required");
        var seamDegrees = 0.1d;
        var seamText = RawField(body, "SeamGap");
        if (seamText is not null && (!TryDegrees(seamText, out seamDegrees) || seamDegrees < 0.01 || seamDegrees > 5))
            diagnostics.Add("loft-seam-gap-invalid:range=0.01..5deg");
        var twistDegrees = 0d;
        var twistText = RawField(body, "Twist");
        if (twistText is not null && (!TryDegrees(twistText, out twistDegrees) || Math.Abs(twistDegrees) > 180))
            diagnostics.Add("loft-twist-invalid:range=-180..180deg");
        var referenceText = Regex.Match(body, @"(?m)^\s*Reference\s*:\s*\[\s*(?<x>[-+.\deE]+)\s*,\s*(?<y>[-+.\deE]+)\s*,\s*(?<z>[-+.\deE]+)\s*\]\s*;?\s*$", RegexOptions.CultureInvariant);
        Vector3D reference = default;
        if (!referenceText.Success || !TryVector(referenceText, out reference) || reference.Length < 1e-8)
            diagnostics.Add("loft-reference-invalid:nonzero-world-direction-required");
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
        if (!materialize) return new(true, chain, null, []);
        var result = SectionChainMaterializer.Materialize(chain);
        if (!result.IsSuccess) diagnostics.AddRange(result.Diagnostics.Select(item => $"{item.Code}:{item.Message}"));
        return new(result.IsSuccess, chain, result, diagnostics);

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

    private static string? Field(string body, string key)
    {
        var match = Regex.Match(body, $@"(?m)^\s*{Regex.Escape(key)}\s*:\s*(?<value>[A-Za-z_]\w*)\s*;?\s*$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? Scalar(string body, string key)
    {
        var match = Regex.Match(body, $@"(?m)^\s*{Regex.Escape(key)}\s*:\s*(?<value>[-+.\deE]+(?:mm|deg))\s*;?\s*$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? RawField(string body, string key)
    {
        var match = Regex.Match(body, $@"(?m)^\s*{Regex.Escape(key)}\s*:\s*(?<value>[^\r\n;]+)\s*;?\s*$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static bool TryDegrees(string text, out double degrees)
    {
        degrees = 0;
        return text.EndsWith("deg", StringComparison.Ordinal)
            && double.TryParse(text[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)
            && double.IsFinite(degrees);
    }

    private static bool TryVector(Match match, out Vector3D vector)
    {
        vector = default;
        if (!double.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !double.TryParse(match.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var z) ||
            !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) return false;
        vector = new(x,y,z);
        return true;
    }

    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }
}
