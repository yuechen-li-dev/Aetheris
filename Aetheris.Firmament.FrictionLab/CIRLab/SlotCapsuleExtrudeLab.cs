namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum SlotCapsuleOrientation { Horizontal, Vertical, Rotated }
public sealed record SlotCapsuleProfileCase(string Name, double RectWidth, double RectHeight, double Height, double CenterX, double CenterY, double Length, double Radius, SlotCapsuleOrientation Orientation, bool ReverseInput = false);
public sealed record SlotCapsuleTopologySummary(bool BodyProduced, int PlanarFaceCount, int CylindricalFaceCount, int FaceCount);
public sealed record SlotCapsuleStepSmokeSummary(bool Exported, IReadOnlyList<string> PresentMarkers, IReadOnlyList<string> MissingMarkers, bool ContainsBrepWithVoids);
public sealed record SlotCapsuleProfileRow(string CaseName, LabProfileStatus Status, bool ExtrusionAttempted, bool ExtrusionSucceeded, SlotCapsuleTopologySummary Topology, SlotCapsuleStepSmokeSummary StepSmoke, IReadOnlyList<string> Diagnostics, string Recommendation);

public static class SlotCapsuleExtrudeLab
{
    private const double Tol = 1e-6;
    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE", "CYLINDRICAL_SURFACE"];

    public static IReadOnlyList<SlotCapsuleProfileRow> RunAll() =>
    [
        Run(new("valid-slot-centered-horizontal", 30, 20, 8, 0, 0, 12, 2, SlotCapsuleOrientation.Horizontal)),
        Run(new("valid-slot-offcenter-horizontal", 30, 20, 8, 5, 2, 10, 1.5, SlotCapsuleOrientation.Horizontal)),
        Run(new("deferred-slot-vertical", 30, 20, 8, 0, 0, 10, 1.5, SlotCapsuleOrientation.Vertical)),
        Run(new("valid-slot-reversed-input", 30, 20, 8, 0, 0, 12, 2, SlotCapsuleOrientation.Horizontal, true)),
        Run(new("invalid-slot-outside", 30, 20, 8, 14, 0, 12, 2, SlotCapsuleOrientation.Horizontal)),
        Run(new("invalid-slot-touches-boundary", 30, 20, 8, 9, 0, 12, 2, SlotCapsuleOrientation.Horizontal)),
        Run(new("invalid-slot-crosses-boundary", 30, 20, 8, 10, 0, 14, 2, SlotCapsuleOrientation.Horizontal)),
        Run(new("invalid-slot-radius", 30, 20, 8, 0, 0, 12, 0, SlotCapsuleOrientation.Horizontal)),
        Run(new("invalid-slot-length", 30, 20, 8, 0, 0, 3, 2, SlotCapsuleOrientation.Horizontal)),
        Run(new("deferred-slot-degenerate-circle", 30, 20, 8, 0, 0, 4, 2, SlotCapsuleOrientation.Horizontal)),
        Run(new("deferred-slot-rotated", 30, 20, 8, 0, 0, 12, 2, SlotCapsuleOrientation.Rotated))
    ];

    public static SlotCapsuleProfileRow Run(SlotCapsuleProfileCase c)
    {
        var d = new List<string> { "v2-x6-slot-capsule-lab-started" };
        if (!double.IsFinite(c.RectWidth) || !double.IsFinite(c.RectHeight) || !double.IsFinite(c.Height) || c.RectWidth <= Tol || c.RectHeight <= Tol || c.Height <= Tol)
            return Reject(c.Name, d, "v2-x6-slot-profile-invalid:invalid-rectangle-or-height");
        if (!double.IsFinite(c.CenterX) || !double.IsFinite(c.CenterY) || !double.IsFinite(c.Length) || !double.IsFinite(c.Radius))
            return Reject(c.Name, d, "v2-x6-slot-profile-invalid:non-finite");
        if (c.Orientation == SlotCapsuleOrientation.Rotated)
            return Defer(c.Name, d, "v2-x6-slot-rotated-deferred");
        if (c.Orientation == SlotCapsuleOrientation.Vertical)
            return Defer(c.Name, d, "v2-x6-slot-vertical-deferred");
        if (c.Radius <= Tol)
            return Reject(c.Name, d, "v2-x6-slot-profile-invalid:radius<=0");
        if (c.Length < 2d * c.Radius - Tol)
            return Reject(c.Name, d, "v2-x6-slot-profile-invalid:length<2r");
        if (Math.Abs(c.Length - 2d * c.Radius) <= Tol)
            return Defer(c.Name, d, "v2-x6-slot-degenerate-circle-deferred");

        var profile = BuildProfile(c);
        d.Add("v2-x6-slot-profile-created");
        var validated = ResolvedProfile2DLab.Evaluate(c.Name, profile);
        d.AddRange(validated.Diagnostics);
        if (validated.Status != LabProfileStatus.Succeeded)
            return Reject(c.Name, d, "v2-x6-slot-profile-invalid:resolved-profile-validation-failed");
        d.Add("v2-x6-slot-profile-validated");

        if (!SlotInsideRect(c, out var boundaryDiag)) return Reject(c.Name, d, boundaryDiag);
        d.Add("v2-x6-slot-contained-in-rectangle");

        d.Add("v2-x6-slot-extrude-attempted");
        d.Add("v2-x6-slot-extrude-blocked:current-emitter-assumes-full-circle-hole-loops");
        return new(c.Name, LabProfileStatus.Deferred, true, false, EmptyTopology(), EmptyStep(), d.Distinct().OrderBy(x => x).ToArray(), "slot-capsule-extrude-needs-emitter-support");
    }

    private static SlotCapsuleProfileRow Reject(string name, List<string> d, string why)
    {
        d.Add(why);
        return new(name, LabProfileStatus.Failed, false, false, EmptyTopology(), EmptyStep(), d.Distinct().OrderBy(x => x).ToArray(), "slot-capsule-invalid-rejected");
    }
    private static SlotCapsuleProfileRow Defer(string name, List<string> d, string why)
    {
        d.Add(why);
        return new(name, LabProfileStatus.Deferred, false, false, EmptyTopology(), EmptyStep(), d.Distinct().OrderBy(x => x).ToArray(), "slot-capsule-deferred-topology");
    }

    private static bool SlotInsideRect(SlotCapsuleProfileCase c, out string diagnostic)
    {
        var hx = c.Orientation == SlotCapsuleOrientation.Horizontal ? c.Length / 2d : c.Radius;
        var hy = c.Orientation == SlotCapsuleOrientation.Horizontal ? c.Radius : c.Length / 2d;
        var x0 = -c.RectWidth / 2d; var x1 = c.RectWidth / 2d; var y0 = -c.RectHeight / 2d; var y1 = c.RectHeight / 2d;
        var sx0 = c.CenterX - hx; var sx1 = c.CenterX + hx; var sy0 = c.CenterY - hy; var sy1 = c.CenterY + hy;
        if (sx0 < x0 - Tol || sx1 > x1 + Tol || sy0 < y0 - Tol || sy1 > y1 + Tol) { diagnostic = "v2-x6-slot-crosses-boundary"; return false; }
        if (Math.Abs(sx0 - x0) <= Tol || Math.Abs(sx1 - x1) <= Tol || Math.Abs(sy0 - y0) <= Tol || Math.Abs(sy1 - y1) <= Tol) { diagnostic = "v2-x6-slot-touches-boundary"; return false; }
        diagnostic = ""; return true;
    }

    private static LabResolvedProfile2D BuildProfile(SlotCapsuleProfileCase c)
    {
        var x0 = -c.RectWidth / 2d; var x1 = c.RectWidth / 2d; var y0 = -c.RectHeight / 2d; var y1 = c.RectHeight / 2d;
        var outer = c.ReverseInput
            ? new LabAirLoop2D([new LabAirLineSegment2D((x0, y0), (x0, y1)), new LabAirLineSegment2D((x0, y1), (x1, y1)), new LabAirLineSegment2D((x1, y1), (x1, y0)), new LabAirLineSegment2D((x1, y0), (x0, y0))], "outer")
            : new LabAirLoop2D([new LabAirLineSegment2D((x0, y0), (x1, y0)), new LabAirLineSegment2D((x1, y0), (x1, y1)), new LabAirLineSegment2D((x1, y1), (x0, y1)), new LabAirLineSegment2D((x0, y1), (x0, y0))], "outer");
        var hole = c.Orientation == SlotCapsuleOrientation.Horizontal ? HorizontalSlot(c.CenterX, c.CenterY, c.Length, c.Radius) : VerticalSlot(c.CenterX, c.CenterY, c.Length, c.Radius);
        return new([outer, hole]);
    }


    public static LabResolvedProfile2D BuildProfileForX7(double rectWidth, double rectHeight, double centerX, double centerY, double length, double radius)
    {
        var c = new SlotCapsuleProfileCase("x7", rectWidth, rectHeight, 1, centerX, centerY, length, radius, SlotCapsuleOrientation.Horizontal);
        return BuildProfile(c);
    }

    private static LabAirLoop2D HorizontalSlot(double cx, double cy, double len, double r)
    {
        var half = len / 2d - r;
        var left = (cx - half, cy); var right = (cx + half, cy);
        var topY = cy + r; var botY = cy - r;
        return new LabAirLoop2D([
            new LabAirLineSegment2D((left.Item1, topY), (right.Item1, topY)),
            new LabAirCircularArc2D(right, r, Math.PI / 2d, -Math.PI),
            new LabAirLineSegment2D((right.Item1, botY), (left.Item1, botY)),
            new LabAirCircularArc2D(left, r, -Math.PI / 2d, -Math.PI)
        ], "hole");
    }
    private static LabAirLoop2D VerticalSlot(double cx, double cy, double len, double r)
    {
        var half = len / 2d - r;
        var bot = (cx, cy - half); var top = (cx, cy + half);
        var lx = cx - r; var rx = cx + r;
        return new LabAirLoop2D([
            new LabAirLineSegment2D((rx, bot.Item2), (rx, top.Item2)),
            new LabAirCircularArc2D(top, r, 0d, -Math.PI),
            new LabAirLineSegment2D((lx, top.Item2), (lx, bot.Item2)),
            new LabAirCircularArc2D(bot, r, Math.PI, -Math.PI)
        ], "hole");
    }

    private static SlotCapsuleTopologySummary EmptyTopology() => new(false, 0, 0, 0);
    private static SlotCapsuleStepSmokeSummary EmptyStep() => new(false, [], RequiredStepMarkers, false);
}
