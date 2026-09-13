using System.Globalization;
using System.Text.RegularExpressions;
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
    private static readonly Regex Revolve = new(@"\bRevolve\s+(?<name>[A-Za-z_]\w*)\s*\{(?<body>[\s\S]*?)\}", RegexOptions.CultureInvariant);
    private static readonly Regex Axis = new(@"\bAxis\s+(?<name>[A-Za-z_]\w*)\s*\{\s*Origin\s*:\s*\[\s*(?<x>[-+.\deE]+)mm\s*,\s*(?<y>[-+.\deE]+)mm\s*,\s*(?<z>[-+.\deE]+)mm\s*\]\s*;?\s*Direction\s*:\s*\[\s*(?<dx>[-+.\deE]+)\s*,\s*(?<dy>[-+.\deE]+)\s*,\s*(?<dz>[-+.\deE]+)\s*\]", RegexOptions.CultureInvariant);

    public static bool IsRevolveSource(string source) => Revolve.IsMatch(source);

    public static ResolvedRevolve? Parse(string source, out IReadOnlyList<string> reportedDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<string>();
        var feature = Revolve.Match(source);
        if (!feature.Success) { reportedDiagnostics = [Prefix + "missing"]; return null; }
        var name = feature.Groups["name"].Value;
        var body = feature.Groups["body"].Value;
        var profileName = Field(body, "Profile");
        var axisName = Field(body, "About");
        var angleText = Field(body, "Angle");
        if (profileName is null) diagnostics.Add(Prefix + "profile-not-found:" + name);
        if (axisName is null) diagnostics.Add(Prefix + "axis-not-found:" + name);
        if (angleText is null) diagnostics.Add(Prefix + "angle-missing:" + name);

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
            var axisMatch = Axis.Matches(source).Cast<Match>().FirstOrDefault(match => match.Groups["name"].Value == axisName);
            if (axisMatch is null) diagnostics.Add(Prefix + "axis-not-found:" + name);
            else if (!Numbers(axisMatch, ["x", "y", "z", "dx", "dy", "dz"], out var values)) diagnostics.Add(Prefix + "axis-invalid:" + name);
            else
            {
                var direction = new Vector3D(values[3], values[4], values[5]);
                if (!direction.TryNormalize(out _)) diagnostics.Add(Prefix + "axis-zero-direction:" + name);
                else axis = new("axis:" + axisName,
                    new(values[0], values[1], values[2]),
                    new(direction.X, direction.Y, direction.Z),
                    "Axis:" + axisName);
            }
        }

        var sweep = 0d;
        string? alias = null;
        if (angleText is not null && !TryAngle(angleText, out sweep, out alias)) diagnostics.Add(Prefix + "angle-invalid:" + name);
        else if (angleText is not null && Math.Abs(sweep) <= Tol) diagnostics.Add(Prefix + "zero-angle:" + name);
        else if (angleText is not null && Math.Abs(sweep) > 2d * Math.PI + Tol) diagnostics.Add(Prefix + "angle-out-of-range:" + name);

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

    private static string? Field(string body, string name)
    {
        var match = Regex.Match(body, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>[^;\r\n}}]+)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static bool Numbers(Match match, IReadOnlyList<string> groups, out double[] values)
    {
        values = new double[groups.Count];
        for (var i = 0; i < groups.Count; i++)
            if (!double.TryParse(match.Groups[groups[i]].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i])) return false;
        return true;
    }

    private static bool TryAngle(string text, out double radians, out string? alias)
    {
        radians = 0d; alias = null;
        var normalized = text.Trim().ToLowerInvariant();
        var sign = 1d;
        if (normalized.StartsWith('+')) normalized = normalized[1..].Trim();
        else if (normalized.StartsWith('-')) { sign = -1d; normalized = normalized[1..].Trim(); }
        var factor = normalized switch { "quarter" => Math.PI / 2d, "half" => Math.PI, "full" => 2d * Math.PI, _ => double.NaN };
        if (double.IsFinite(factor)) { radians = sign * factor; alias = normalized; return true; }
        if (normalized.EndsWith("deg", StringComparison.Ordinal))
        {
            if (!double.TryParse(normalized[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) || !double.IsFinite(degrees)) return false;
            radians = sign * degrees * Math.PI / 180d; return true;
        }
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) || !double.IsFinite(numeric)) return false;
        radians = sign * numeric; return true;
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
