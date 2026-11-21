using System.Globalization;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

namespace Aetheris.CLI;

internal static class SculptCompareCommand
{
    private const string Usage = "Usage: aetheris compare <sculpt.firmament> --before <state> --after <state> [--section-y <mm>] [--wheel <file.step> [--minimum-clearance <mm>] [--steering-lock <deg>] [--suspension-bump <mm>]] [--out <comparison.svg>] [--json]";

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            (args.Length == 0 ? stderr : stdout).WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }
        var json = args.Contains("--json");
        string? beforeName = null, afterName = null, output = null, wheelPath = null;
        double? sectionY = null, minimum = null, steering = null, bump = null;
        for (var i = 1; i < args.Length; i++)
        {
            var option = args[i];
            if (option == "--json") continue;
            if (i + 1 >= args.Length) return Fail($"Missing value for '{option}'.");
            var value = args[++i];
            switch (option)
            {
                case "--before": beforeName = value; break;
                case "--after": afterName = value; break;
                case "--out": output = value; break;
                case "--wheel": wheelPath = value; break;
                case "--section-y" when TryNumber(value, out var y): sectionY = y; break;
                case "--minimum-clearance" when TryNumber(value, out var gap) && gap >= 0: minimum = gap; break;
                case "--steering-lock" when TryNumber(value, out var degrees) && degrees is >= 0 and <= 65: steering = degrees; break;
                case "--suspension-bump" when TryNumber(value, out var travel) && travel >= 0: bump = travel; break;
                default: return Fail($"Unknown option or invalid value for '{option}'.");
            }
        }
        if (beforeName is null || afterName is null || ((minimum.HasValue || steering.HasValue || bump.HasValue) && wheelPath is null))
            return Fail(Usage);
        if (output is not null && !Path.GetExtension(output).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            return Fail("Comparison preview must have the .svg extension.");

        try
        {
            var compile = SculptingAuthoring.CompileFile(args[0], afterName);
            if (!compile.IsSuccess)
                return Fail(string.Join(" | ", compile.Diagnostics.Select(d => d.Message)));
            if (!compile.MeshStates.TryGetValue(beforeName, out var before) || !compile.MeshStates.TryGetValue(afterName, out var after))
                return Fail("Before and after must name admitted mesh states in the same model.");
            var movement = SubdivisionComparison.Measure(before, after);
            var a = SubdivisionSculptor.PrepareExport(before).EncodedMesh;
            var b = SubdivisionSculptor.PrepareExport(after).EncodedMesh;
            var wheel = wheelPath is null ? null : SectionWheel.Load(wheelPath);
            var y = sectionY ?? wheel?.Center.Y ?? 0;
            var clearanceBefore = wheel is null ? null : SculptCompareGeometry.Clearance(a, wheel);
            var clearanceAfter = wheel is null ? null : SculptCompareGeometry.Clearance(b, wheel);
            var wheelSweep = wheel is not null && (steering.HasValue || bump.HasValue)
                ? SculptCompareGeometry.BoundWheelSweep(b, wheel, steering ?? 0, bump ?? 0) : null;
            var success = (clearanceAfter is null || (clearanceAfter.CenterOutsideShell && clearanceAfter.LowerBoundMm >= (minimum ?? 0)))
                && (wheelSweep is null || (wheelSweep.LowerBoundMm >= (minimum ?? 0)
                    && wheelSweep.Poses.All(pose => pose.Clearance.CenterOutsideShell)));
            if (output is not null && success)
            {
                output = Path.GetFullPath(output);
                var svg = Render(beforeName, afterName, a, b, movement, y, wheel);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, svg);
            }

            var report = new
            {
                command = "compare", success, before = beforeName, after = afterName,
                movement, after.FairEffect, after.StrokeEffect, after.CutoutEffect, beforeRecess = before.RecessEffect, afterRim = after.SectionFit?.Rim,
                sectionYMm = y, wheel, minimumClearanceMm = minimum, clearanceBefore, clearanceAfter, wheelSweep,
                preview = success ? output : null
            };
            if (json) stdout.WriteLine(JsonSerializer.Serialize(report, CliRunner.JsonOptions));
            else
            {
                stdout.WriteLine(FormattableString.Invariant($"{beforeName} to {afterName}: max movement {movement.MaximumMovementMm:F4} mm; rim {movement.MaximumRimMovementMm:F4} mm."));
                if (clearanceAfter is not null)
                    stdout.WriteLine(FormattableString.Invariant($"Wheel clearance {clearanceAfter.LowerBoundMm:F3}..{clearanceAfter.SampledUpperBoundMm:F3} mm; center outside shell: {clearanceAfter.CenterOutsideShell}."));
                if (wheelSweep is not null)
                    stdout.WriteLine(FormattableString.Invariant($"Wheel sweep lower bound {wheelSweep.LowerBoundMm:F3} mm from {wheelSweep.Poses.Count} poses plus a between-pose allowance."));
                if (!success) stderr.WriteLine("The comparison did not qualify the required wheel clearance.");
            }
            return success ? 0 : 1;
        }
        catch (InvalidOperationException problem) { return Fail(problem.Message); }

        int Fail(string message)
        {
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "compare", success = false, diagnostics = new[] { message } }, CliRunner.JsonOptions));
            else stderr.WriteLine(message);
            return 1;
        }
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static string Render(string beforeName, string afterName, TriangleMesh before, TriangleMesh after,
        MeshComparisonEvidence movement, double y, SectionWheel? wheel)
    {
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1040' height='650' viewBox='0 0 1040 650'>");
        svg.Append("<rect width='1040' height='650' fill='#f3f6f8'/>");
        var forward = new Vector3D(-0.6, -1, 0.82);
        var right = new Vector3D(-forward.Y, forward.X, 0);
        right /= right.Length;
        var up = forward.Cross(right);
        up /= up.Length;
        var points = before.Positions.Concat(after.Positions).ToArray();
        double U(Point3D p) => (p - Point3D.Origin).Dot(right);
        double V(Point3D p) => (p - Point3D.Origin).Dot(up);
        var u0 = points.Min(U); var u1 = points.Max(U); var v0 = points.Min(V); var v1 = points.Max(V);
        var zoom = Math.Min(475 / Math.Max(1e-12, u1 - u0), 250 / Math.Max(1e-12, v1 - v0));
        var deltas = before.Positions.Zip(after.Positions, (a, b) => (b - a).Length).ToArray();

        Caption(24, 38, "SubD state comparison", 27);
        Caption(24, 72, SecurityElementEscape(beforeName), 18);
        Caption(544, 72, SecurityElementEscape(afterName) + " · sampled displacement", 18);
        View(before, 260, false);
        View(after, 780, true);
        Caption(24, 372, FormattableString.Invariant($"Center section Y = {y:G4} mm   •   Movement max {movement.MaximumMovementMm:F3} mm"), 18);
        var cuts = SculptCompareGeometry.Section(before, y);
        var edited = SculptCompareGeometry.Section(after, y);
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var maxZ = points.Max(p => p.Z); var minZ = points.Min(p => p.Z);
        var scale = Math.Min(940 / Math.Max(1e-12, maxX - minX), 200 / Math.Max(1e-12, maxZ - minZ));
        double X(double x) => 520 + (x - (maxX + minX) / 2) * scale;
        double Z(double z) => 612 - (z - minZ) * scale;
        SectionLines(cuts, "#68879a");
        SectionLines(edited, "#0f536b");
        if (wheel is not null && Math.Abs(y - wheel.Center.Y) <= wheel.WidthMm / 2)
            svg.Append(FormattableString.Invariant($"<circle cx='{X(wheel.Center.X):F2}' cy='{Z(wheel.Center.Z):F2}' r='{(wheel.RadiusMm * scale):F2}' fill='#efae74' fill-opacity='.72' stroke='#a84c23' stroke-width='2'/>"));
        Caption(24, 637, "Section  •  seed in slate blue  •  edited in teal  •  exact wheel in orange when supplied", 13);
        return svg.Append("</svg>").ToString();

        void Caption(double x, double yPosition, string text, int size) =>
            svg.Append(FormattableString.Invariant($"<text x='{x}' y='{yPosition}' font-family='system-ui,sans-serif' font-size='{size}' fill='#213748'>{text}</text>"));

        void SectionLines(IReadOnlyList<SculptSectionSegment> segments, string color)
        {
            svg.Append($"<path fill='none' stroke='{color}' stroke-width='2' d='");
            foreach (var (a, b) in segments)
                svg.Append(FormattableString.Invariant($"M{X(a.X):F2},{Z(a.Z):F2}L{X(b.X):F2},{Z(b.Z):F2}"));
            svg.Append("'/>");
        }

        void View(TriangleMesh mesh, double centerX, bool showMovement)
        {
            double PX(Point3D p) => centerX + (U(p) - (u0 + u1) / 2) * zoom;
            double PY(Point3D p) => 218 - (V(p) - (v0 + v1) / 2) * zoom;
            var indices = mesh.TriangleIndices;
            var faces = Enumerable.Range(0, indices.Count / 3).Select(i => 3 * i)
                .OrderBy(i => ((mesh.Positions[indices[i]] - Point3D.Origin)
                    + (mesh.Positions[indices[i + 1]] - Point3D.Origin)
                    + (mesh.Positions[indices[i + 2]] - Point3D.Origin)).Dot(forward));
            var light = new Vector3D(-0.4, -0.6, 0.9);
            light /= light.Length;
            foreach (var i in faces)
            {
                var a = mesh.Positions[indices[i]]; var b = mesh.Positions[indices[i + 1]]; var c = mesh.Positions[indices[i + 2]];
                var n = (b - a).Cross(c - a);
                if (n.Dot(forward) <= 0 || !n.TryNormalize(out var normal)) continue;
                var shade = 0.60 + 0.40 * Math.Max(0, normal.Dot(light));
                var effect = showMovement && movement.MaximumMovementMm > 0
                    ? (deltas[indices[i]] + deltas[indices[i + 1]] + deltas[indices[i + 2]]) / (3 * movement.MaximumMovementMm) : 0;
                var r = (int)((1 - effect) * 94 * shade + effect * 232);
                var g = (int)((1 - effect) * 126 * shade + effect * 108);
                var blue = (int)((1 - effect) * 145 * shade + effect * 54);
                var color = $"#{r:x2}{g:x2}{blue:x2}";
                svg.Append(FormattableString.Invariant($"<path d='M{PX(a):F2},{PY(a):F2}L{PX(b):F2},{PY(b):F2}L{PX(c):F2},{PY(c):F2}Z' fill='{color}' stroke='{color}' stroke-width='.35'/>"));
            }
        }

        static string SecurityElementEscape(string value) => System.Security.SecurityElement.Escape(value) ?? value;
    }
}
