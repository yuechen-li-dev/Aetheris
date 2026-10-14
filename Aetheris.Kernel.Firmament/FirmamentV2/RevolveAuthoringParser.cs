using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record ResolvedRevolve(
    string Name,
    ResolvedProfile2D Profile,
    string AxisName,
    ConceptIrAxisValue Axis,
    double SweepRadians,
    string? AuthoredAlias,
    string StableId);

/// <summary>Finite semantic binder for Profile + explicit Axis + bounded Angle.</summary>
public static class RevolveAuthoringParser
{
    public const string Prefix = "firmament-revolve-";
    private const double Tol = 1e-9;
    public static bool IsRevolveSource(string source) => FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements)
        .Any(d => d.Type.Name.Text == "Revolve");

    public static ResolvedRevolve? Parse(string source, out IReadOnlyList<string> reportedDiagnostics) => Bind(source, out reportedDiagnostics, expand: true);
    internal static ResolvedRevolve? BindExpanded(string source, SyntaxParseResult syntax, out IReadOnlyList<string> reportedDiagnostics) => Bind(source, out reportedDiagnostics, expand: false, syntax);
    private static ResolvedRevolve? Bind(string source, out IReadOnlyList<string> reportedDiagnostics, bool expand, SyntaxParseResult? parsedSyntax = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<string>();
        var normalized = expand ? NormalizeSemanticSource(source, diagnostics) : source;
        if (normalized is null)
        {
            reportedDiagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToArray();
            return null;
        }
        source = normalized;
        var syntax = parsedSyntax ?? FirmamentSyntaxParser.Parse(source);
        FirmamentV2Parser.BindMaterialDomainScope(syntax.Document, "Revolve", diagnostics);
        diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code + ": " + d.Message));
        var declarations = FirmamentV2Parser.MechanicalDeclarations(syntax.Document.Statements).ToArray();
        var features = declarations.Where(d => d.Type.Name.Text == "Revolve").ToArray();
        if (features.Length != 1)
        { reportedDiagnostics = diagnostics.Append(Prefix + (features.Length == 0 ? "missing" : "multiple-material-features-unsupported")).ToArray(); return null; }
        var feature = features[0];
        var name = feature.Name?.Text ?? "";
        var fields = new FirmamentSyntaxFields(feature, diagnostics);
        var profileName = fields.Word("Profile");
        var axisName = fields.Word("About");
        var angleExpression = fields.Take("Angle");
        fields.Complete();
        if (feature.Type.Arguments.Count > 0 || feature.Modifiers.Count > 0 || name.Length == 0)
            diagnostics.Add(Prefix + "header-invalid:" + name);
        if (profileName is null) diagnostics.Add(Prefix + "profile-not-found:" + name);
        if (axisName is null) diagnostics.Add(Prefix + "axis-not-found:" + name);
        if (angleExpression is null) diagnostics.Add(Prefix + "angle-missing:" + name);

        IReadOnlyList<string> profileDiagnostics = [];
        var profile = profileName is null ? null : ProfileAuthoringParser.ResolveNamedProfile(source, profileName, out profileDiagnostics);
        if (profileName is not null)
        {
            diagnostics.AddRange(profileDiagnostics);
            if (profile is null)
            {
                if (profileDiagnostics.Any(message => message.Contains("nonzero area", StringComparison.Ordinal))) diagnostics.Add(Prefix + "profile-zero-area:" + name);
                else if (profileDiagnostics.Any(message => message.Contains("self-intersection", StringComparison.Ordinal))) diagnostics.Add(Prefix + "self-intersection:" + name);
                else if (profileDiagnostics.Any(message => message.StartsWith("profile-source-missing-profile:", StringComparison.Ordinal))) diagnostics.Add(Prefix + "profile-not-found:" + name);
            }
        }

        ConceptIrAxisValue? axis = null;
        if (axisName is not null)
        {
            var axes = declarations.Where(d => d.Type.Name.Text == "Axis" && d.Name?.Text == axisName).ToArray();
            if (axes.Length == 0) diagnostics.Add(Prefix + "axis-not-found:" + name);
            else if (axes.Length > 1) diagnostics.Add(Prefix + "axis-duplicate:" + axisName);
            else
            {
                if (axes[0].Type.Arguments.Count > 0 || axes[0].Modifiers.Count > 0) diagnostics.Add(Prefix + "axis-header-invalid:" + axisName);
                var axisFields = new FirmamentSyntaxFields(axes[0], diagnostics);
                var originValid = axisFields.Vector("Origin", "mm", out var origin);
                var directionValid = axisFields.Vector("Direction", "", out var components);
                axisFields.Complete();
                if (!originValid || origin.Length != 3 || !directionValid || components.Length != 3)
                    diagnostics.Add(Prefix + "axis-invalid:" + name);
                else
                {
                    var direction = new Vector3D(components[0], components[1], components[2]);
                    if (!direction.TryNormalize(out _)) diagnostics.Add(Prefix + "axis-zero-direction:" + name);
                    else axis = new("axis:" + axisName, new(origin[0], origin[1], origin[2]),
                        new(direction.X, direction.Y, direction.Z), "Axis:" + axisName);
                }
            }
        }

        var sweep = 0d;
        string? alias = null;
        if (angleExpression is not null && !TryAngle(angleExpression, out sweep, out alias)) diagnostics.Add(Prefix + "angle-invalid:" + name);
        else if (angleExpression is not null && Math.Abs(sweep) <= Tol) diagnostics.Add(Prefix + "zero-angle:" + name);
        else if (angleExpression is not null && Math.Abs(sweep) > 2d * Math.PI + Tol) diagnostics.Add(Prefix + "angle-out-of-range:" + name);

        if (profile is not null && axis is not null)
        {
            var frame = profile.EffectiveConstructionPlane;
            var origin = new Point3D(axis.Origin.X, axis.Origin.Y, axis.Origin.Z);
            var direction = new Vector3D(axis.Direction.X, axis.Direction.Y, axis.Direction.Z);
            var localOrigin = frame.ToLocal(origin);
            var normalComponent = direction.Dot(frame.AxisZ.ToVector());
            if (Math.Abs(localOrigin.Z) > 1e-7 || Math.Abs(normalComponent) > 1e-7)
                diagnostics.Add(Prefix + "axis-not-in-profile-plane:" + name);
            else if (CrossesAxis(profile, localOrigin, direction, frame))
                diagnostics.Add(Prefix + "profile-crosses-axis:" + name);
        }

        reportedDiagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToArray();
        return reportedDiagnostics.Count == 0 && profile is not null && axis is not null
            ? new(name, profile, axisName!, axis, sweep, alias, "revolve:" + name)
            : null;
    }

    private static string? NormalizeSemanticSource(string source, List<string> diagnostics)
    {
        // Revolve is inspected and validated through a dedicated CLI lane. Keep that lane
        // aligned with the production build ordering so semantic boundaries and Mirror are
        // resolved before the profile binder, just as they are for actual materialization.
        var boundaries = ClosedBoundary2Authoring.Expand(source, diagnostics);
        if (boundaries is null) return null;
        var templates = FirmamentV2TemplateExpansion.Expand(boundaries.Source, diagnostics);
        if (templates is null) return null;
        var features = FirmamentV2FeatureExpansion.Expand(templates.Source, diagnostics);
        if (features is null) return null;
        var specializedBoundaries = ClosedBoundary2Authoring.Expand(features.Source, diagnostics);
        if (specializedBoundaries is null) return null;
        var staticAuthoring = CanonicalStaticAuthoring.Expand(specializedBoundaries.Source, diagnostics);
        return staticAuthoring?.Source;
    }

    private static bool TryAngle(ExpressionSyntax expression, out double radians, out string? alias)
    {
        var sign = 1d;
        while (expression is UnarySyntax { Operator: UnaryOperator.Positive or UnaryOperator.Negative } unary)
        { if (unary.Operator == UnaryOperator.Negative) sign = -sign; expression = unary.Operand; }
        alias = FirmamentSyntaxValues.Reference(expression)?.ToLowerInvariant();
        radians = sign * (alias switch { "quarter" => Math.PI / 2d, "half" => Math.PI, "full" => 2d * Math.PI, _ => double.NaN });
        if (double.IsFinite(radians)) return true;
        alias = null;
        if (FirmamentSyntaxValues.TryMeasure(expression, "deg", out var degrees)) radians = sign * degrees * Math.PI / 180d;
        else if (FirmamentSyntaxValues.TryMeasure(expression, "", out var numeric)) radians = sign * numeric;
        return double.IsFinite(radians);
    }

    internal static bool TryAngle(string text, out double radians, out string? alias)
    {
        var syntax = FirmamentSyntaxParser.ParseExpression(text);
        radians = double.NaN; alias = null;
        return syntax.Diagnostics.Count == 0 && syntax.Expression is not null && TryAngle(syntax.Expression, out radians, out alias);
    }

    private static bool CrossesAxis(ResolvedProfile2D profile, (double X, double Y, double Z) axisOrigin, Vector3D worldDirection, ConstructionPlane frame)
    {
        var dx = worldDirection.Dot(frame.AxisX.ToVector());
        var dy = worldDirection.Dot(frame.AxisY.ToVector());
        var length = Math.Sqrt(dx * dx + dy * dy);
        dx /= length; dy /= length;
        var signs = new HashSet<int>();
        foreach (var loop in profile.Loops)
        foreach (var segment in loop.Segments)
        foreach (var point in Samples(segment.Geometry))
        {
            var signed = dx * (point.Y - axisOrigin.Y) - dy * (point.X - axisOrigin.X);
            if (Math.Abs(signed) > 1e-7) signs.Add(Math.Sign(signed));
        }
        return signs.Count > 1;
    }

    private static IEnumerable<(double X, double Y)> Samples(LineArcProfileCurve2D curve)
    {
        switch (curve)
        {
            case LineArcLineSegment2D line: yield return line.Start; yield return line.End; break;
            case LineArcCircularArc2D arc:
                for (var i = 0; i <= 32; i++) { var a = arc.StartAngleRadians + arc.SweepAngleRadians * i / 32d; yield return (arc.Center.X + arc.Radius * Math.Cos(a), arc.Center.Y + arc.Radius * Math.Sin(a)); }
                break;
            case LineArcFullCircle2D circle:
                for (var i = 0; i < 64; i++) { var a = 2d * Math.PI * i / 64d; yield return (circle.Center.X + circle.Radius * Math.Cos(a), circle.Center.Y + circle.Radius * Math.Sin(a)); }
                break;
            case LineArcFullEllipse2D ellipse:
                for (var i = 0; i < 64; i++) { var a = 2d * Math.PI * i / 64d; var x = ellipse.MajorRadius * Math.Cos(a); var y = ellipse.MinorRadius * Math.Sin(a); var c = Math.Cos(ellipse.RotationRadians); var s = Math.Sin(ellipse.RotationRadians); yield return (ellipse.Center.X + c * x - s * y, ellipse.Center.Y + s * x + c * y); }
                break;
        }
    }
}
