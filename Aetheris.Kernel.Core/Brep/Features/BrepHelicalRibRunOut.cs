using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// A coaxial chamfer at the stock's far end: the stock radius falls linearly from
/// <see cref="ChamferStartMm"/> to <see cref="EndRadiusMm"/> at <see cref="EndMm"/>, where a flat end face closes it.
/// </summary>
public sealed record HelicalTipRunOut(double ChamferStartMm, double EndMm, double EndRadiusMm);

public static partial class BrepHelicalRib
{
    private sealed class RawEdge(int from, int to, Func<double, Point3D> evaluate, CurveGeometry curve,
        double start, double end, string role, int helixRole = -1)
    {
        public int From { get; } = from;
        public int To { get; } = to;
        public Func<double, Point3D> Evaluate { get; } = evaluate;
        public CurveGeometry Curve { get; } = curve;
        public double Start { get; } = start;
        public double End { get; } = end;
        public string Role { get; } = role;
        public int HelixRole { get; } = helixRole;
        public bool Resolved { get; set; }
        public EdgeId? Kept { get; set; }
        public int KeptFrom { get; set; }
        public int KeptTo { get; set; }
        public double KeptStart { get; set; }
        public double KeptEnd { get; set; }
        /// <summary>A straight edge kept from an interior point restarts its line there, at parameter zero.</summary>
        public double Shift { get; set; }
    }

    private readonly record struct RawUse(RawEdge Edge, bool Reverse, PcurveGeometry? Pcurve = null);

    private enum ClipKind { None, Land, Side }

    private sealed record ClipRecord(EdgeId Edge, int From, int To, double ThetaFrom, double ThetaTo,
        double AxialFrom, double AxialTo, ParameterInterval Trim, bool FaceReverse);

    /// <summary>
    /// A groove that runs out through a tip chamfer, the way a cut thread leaves the end of a bolt. Turns continue
    /// past the material; each groove and land face is clipped by the chamfer cone along a straight line of its own
    /// parameters (radius and axial position are linear in both the angle and the profile coordinate), and the cone
    /// carries the helical notch the groove leaves in it. The flat end face stays whole when the end radius lies
    /// inside the groove bottom.
    /// </summary>
    public static KernelResult<BrepHelicalRibResult> CreateWithTipRunOut(HelicalRibGeometry rib, HelicalTipRunOut tip)
    {
        ArgumentNullException.ThrowIfNull(rib);
        ArgumentNullException.ThrowIfNull(tip);
        var p = rib.Parameters;
        if (p.Intent != HelicalProfileIntent.RemoveGroove)
            return Failure("runout-groove-only", "A tip run-out is admitted only for a groove cut into stock.");
        var R = p.RootRadiusMm;
        var rc = p.CrestRadiusMm;
        if (!(double.IsFinite(tip.ChamferStartMm) && double.IsFinite(tip.EndMm) && double.IsFinite(tip.EndRadiusMm))
            || tip.EndMm <= tip.ChamferStartMm || tip.EndRadiusMm <= 0d || tip.EndRadiusMm >= rc - 1e-6d)
            return Failure("runout-tip-invalid", "The tip chamfer must end at a positive radius inside the groove bottom.");
        var m = (R - tip.EndRadiusMm) / (tip.EndMm - tip.ChamferStartMm);
        var bottomExit = tip.ChamferStartMm + (R - rc) / m;
        // Clear when the last section lies wholly beyond the cone and the last trailing root starts above the chamfer.
        var clear = double.Max(bottomExit + p.CrestWidthMm / 2d, tip.ChamferStartMm + p.PitchMm - p.RootWidthMm / 2d) + 1e-4d;
        var turns = (int)double.Ceiling((clear - p.AxialStartMm) / p.PitchMm);
        if (turns < 2 || turns > 400)
            return Failure("runout-turns", "A tip run-out admits two to four hundred turns.");
        var end = p.AxialStartMm + turns * p.PitchMm;
        var extended = HelicalRibGeometry.Create(p with
        {
            AxialEndMm = end,
            SupportAxialMaxMm = double.Max(p.SupportAxialMaxMm, end + p.RootWidthMm)
        });
        if (!extended.IsSuccess) return KernelResult<BrepHelicalRibResult>.Failure(extended.Diagnostics);
        if (p.AxialStartMm - p.RootWidthMm / 2d - p.SupportAxialMinMm <= 1e-6d)
            return Failure("support-end-contact-unsupported", "A run-out requires exposed support before the groove footprint.");
        try { return BuildRunOut(extended.Value, turns, tip, m); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Failure("runout-construction-failed", ex.Message);
        }
    }

    private static KernelResult<BrepHelicalRibResult> BuildRunOut(HelicalRibGeometry rib, int turns, HelicalTipRunOut tip, double m)
    {
        var p = rib.Parameters;
        var R = p.RootRadiusMm;
        var rc = p.CrestRadiusMm;
        var sC = tip.ChamferStartMm;
        var sB = sC + (R - rc) / m;
        var axis = p.Axis.ToVector();
        var radial0 = p.StartRadial.ToVector();
        var circumferential = axis.Cross(radial0);
        Vector3D Radial(double theta) => radial0 * double.Cos(theta) + circumferential * double.Sin(theta);
        Vector3D RadialDerivative(double theta) => circumferential * double.Cos(theta) - radial0 * double.Sin(theta);
        double AxialOf(Point3D x) => (x - p.AxisOrigin).Dot(axis);
        double G(Point3D x)
        {
            var d = x - p.AxisOrigin;
            var s = d.Dot(axis);
            var r = (d - axis * s).Length;
            return m * (s - sC) - (R - r);
        }

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var faceRoles = new Dictionary<FaceId, string>();
        var edgeRoles = new Dictionary<EdgeId, string>();
        var qualifiedSideFaces = new Dictionary<FaceId, QualifiedHelicalRibSurface>();
        var faces = new List<FaceId>();
        var nextCurve = 1;
        var nextSurface = 1;

        // Vertices materialize only when a kept edge uses them.
        var rawPoints = new List<Point3D>();
        var rawG = new List<double>();
        var materialized = new Dictionary<int, VertexId>();
        var clipHelixRole = new Dictionary<int, int>();
        var clips = new List<ClipRecord>();
        int RawVertex(Point3D point, double? g = null)
        {
            rawPoints.Add(point);
            rawG.Add(g ?? G(point));
            return rawPoints.Count - 1;
        }
        VertexId Vertex(int raw)
        {
            if (materialized.TryGetValue(raw, out var id)) return id;
            id = builder.AddVertex();
            points.Add(id, rawPoints[raw]);
            materialized.Add(raw, id);
            return id;
        }
        EdgeId AddEdge(int from, int to, CurveGeometry curve, ParameterInterval trim, string role)
        {
            var id = builder.AddEdge(Vertex(from), Vertex(to));
            var curveId = new CurveGeometryId(nextCurve++);
            geometry.AddCurve(curveId, curve);
            bindings.AddEdgeBinding(new(id, curveId, trim));
            edgeRoles.Add(id, role);
            return id;
        }

        RawEdge Straight(int from, int to, string role)
        {
            var a = rawPoints[from];
            var b = rawPoints[to];
            var line = new Line3Curve(a, Direction3D.Create(b - a));
            return new RawEdge(from, to, line.Evaluate, CurveGeometry.FromLine(line), 0d, (b - a).Length, role);
        }

        // An edge keeps the part inside the material. The material test is linear along every edge here: straight
        // edges lie in a meridian half-plane or along a generator, and helical edges keep a constant radius.
        void Resolve(RawEdge edge)
        {
            if (edge.Resolved) return;
            edge.Resolved = true;
            var ga = rawG[edge.From];
            var gb = rawG[edge.To];
            if (ga > 0d && gb > 0d) return;
            if (ga <= 0d && gb <= 0d)
            {
                edge.KeptFrom = edge.From; edge.KeptTo = edge.To;
                edge.KeptStart = edge.Start; edge.KeptEnd = edge.End;
            }
            else
            {
                var t = edge.Start + (edge.End - edge.Start) * ga / (ga - gb);
                var clip = RawVertex(edge.Evaluate(t), 0d);
                clipHelixRole[clip] = edge.HelixRole;
                if (ga <= 0d) { edge.KeptFrom = edge.From; edge.KeptTo = clip; edge.KeptStart = edge.Start; edge.KeptEnd = t; }
                else { edge.KeptFrom = clip; edge.KeptTo = edge.To; edge.KeptStart = t; edge.KeptEnd = edge.End; }
            }
            var curve = edge.Curve;
            if (curve.Line3 is { } line && edge.KeptStart != edge.Start)
            {
                curve = CurveGeometry.FromLine(new Line3Curve(line.Evaluate(edge.KeptStart), line.Direction));
                edge.Shift = edge.KeptStart;
            }
            edge.Kept = AddEdge(edge.KeptFrom, edge.KeptTo, curve, new(edge.KeptStart - edge.Shift, edge.KeptEnd - edge.Shift), edge.Role);
        }

        var seamRadial = Direction3D.Create(Radial(p.StartAngleRadians));
        var cylinderId = new SurfaceGeometryId(nextSurface++);
        var cylinder = new CylinderSurface(p.AxisOrigin, p.Axis, R, seamRadial);
        geometry.AddSurface(cylinderId, SurfaceGeometry.FromCylinder(cylinder));
        var section = new int[turns + 1, 4];
        for (var k = 0; k <= turns; k++)
        {
            var angle = p.StartAngleRadians + 2d * double.Pi * k;
            for (var role = 0; role < 4; role++)
            {
                section[k, role] = RawVertex(rib.Boundary((HelicalRibBoundaryRole)role).Evaluate(angle));
                if (double.Abs(rawG[section[k, role]]) < 1e-7d)
                    throw new InvalidOperationException("A thread section corner lies on the tip chamfer; shift the start offset.");
            }
        }
        var bottom = RawVertex(cylinder.Evaluate(0d, p.SupportAxialMinMm));

        var sideSurfaces = new SurfaceGeometryId[turns, 3];
        var sideRealizations = new QualifiedHelicalRibSurface[turns, 3];
        var helical = new RawEdge[turns, 4];
        for (var k = 0; k < turns; k++)
        {
            var sub = HelicalRibGeometry.Create(p with
            {
                AxialStartMm = p.AxialStartMm + k * p.PitchMm,
                AxialEndMm = p.AxialStartMm + (k + 1) * p.PitchMm,
                StartAngleRadians = p.StartAngleRadians + 2d * double.Pi * k
            });
            if (!sub.IsSuccess) return KernelResult<BrepHelicalRibResult>.Failure(sub.Diagnostics);
            var realized = new QualifiedHelicalRibSurface[3];
            for (var role = 0; role < 3; role++)
            {
                // Turns wholly beyond the material carry no face.
                var result = HelicalRibSurfaceRealizer.Realize(sub.Value, (HelicalRibSideRole)role);
                if (!result.IsSuccess) return KernelResult<BrepHelicalRibResult>.Failure(result.Diagnostics);
                realized[role] = result.Value;
                sideRealizations[k, role] = result.Value;
                var surfaceId = new SurfaceGeometryId(nextSurface++);
                geometry.AddSurface(surfaceId, SurfaceGeometry.FromBSplineSurfaceWithKnots(result.Value.Surface));
                sideSurfaces[k, role] = surfaceId;
            }
            for (var role = 0; role < 4; role++)
            {
                var source = role switch { 0 => realized[0].Surface, 1 => realized[0].Surface,
                    2 => realized[1].Surface, _ => realized[2].Surface };
                var column = role is 0 ? 0 : 1;
                var curve = new BSpline3Curve(source.DegreeU,
                    source.ControlPoints.Select(row => row[column]).ToArray(),
                    source.KnotMultiplicitiesU, source.KnotValuesU, "UNSPECIFIED", false, false, "UNSPECIFIED");
                helical[k, role] = new RawEdge(section[k, role], section[k + 1, role], curve.Evaluate,
                    CurveGeometry.FromBSpline(curve), curve.DomainStart, curve.DomainEnd,
                    $"{(HelicalRibBoundaryRole)role}.Segment[{k}]", role);
            }
        }

        var cross = new RawEdge[turns + 1, 3];
        for (var k = 0; k <= turns; k++)
            for (var role = 0; role < 3; role++)
                cross[k, role] = Straight(section[k, role], section[k, role + 1], $"Section[{k}].{(HelicalRibSideRole)role}");
        var startRoot = Straight(section[0, 0], section[0, 3], "Start.Root");
        var gaps = new RawEdge[turns + 1];
        for (var k = 1; k <= turns; k++)
            gaps[k] = Straight(section[k - 1, 3], section[k, 0], $"RootGap.Seam[{k}]");
        var lowerSeam = Straight(bottom, section[0, 0], "RootLower.Seam");
        var bottomUv = PcurveGeometry.Line(new(0d, 2d * double.Pi),
            new(0d, p.SupportAxialMinMm), new(2d * double.Pi, p.SupportAxialMinMm));

        PcurveGeometry CylinderVertical(RawEdge edge, double u) => PcurveGeometry.Line(new(edge.Start, edge.End),
            new(u, AxialOf(rawPoints[edge.From])), new(u, AxialOf(rawPoints[edge.To])));
        PcurveGeometry CylinderHelix(RawEdge edge, int turn, bool trailing)
        {
            var z0 = p.AxialStartMm + turn * p.PitchMm + (trailing ? p.RootWidthMm / 2d : -p.RootWidthMm / 2d);
            return PcurveGeometry.Line(new(edge.Start, edge.End), new(0d, z0), new(2d * double.Pi, z0 + p.PitchMm));
        }
        static RawUse[] Reversed(params RawUse[] uses) => uses.Reverse()
            .Select(use => use with { Reverse = !use.Reverse }).ToArray();

        FaceId MakeFace(string role, SurfaceGeometryId surfaceId, IReadOnlyList<(EdgeId Edge, bool Reverse, PcurveGeometry? Uv)> uses)
        {
            var loop = builder.AllocateLoopId();
            var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < uses.Count; i++)
                builder.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop,
                    coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].Reverse));
            builder.AddLoop(new Loop(loop, coedges));
            var face = builder.AddFace([loop]);
            bindings.AddFaceBinding(new(face, surfaceId));
            bindings.AddFaceBoundaryRoleBinding(new(face, loop, FaceBoundaryRole.Outer));
            for (var i = 0; i < uses.Count; i++)
                if (uses[i].Uv is { } uv)
                    bindings.AddPcurveBinding(new(coedges[i], face, surfaceId, uv));
            faces.Add(face);
            faceRoles.Add(face, role);
            return face;
        }

        // The clip edge where a face leaves the material: a circle on the stock rim or the groove bottom, or a
        // conical spiral across a flank. Its parameter increases with the angle.
        (EdgeId Edge, bool Reverse, PcurveGeometry Uv) ClipEdge(ClipKind kind, int sideRole,
            int v1, int v2, SurfaceParameterPoint uv1, SurfaceParameterPoint uv2)
        {
            double Theta(SurfaceParameterPoint uv) => kind == ClipKind.Land ? p.StartAngleRadians + uv.U : uv.U;
            (double Radius, double Axial) Section(SurfaceParameterPoint uv)
            {
                if (kind == ClipKind.Land) return (R, uv.V);
                var (r0, z0, r1, z1) = sideRole switch
                {
                    0 => (R, -p.RootWidthMm / 2d, rc, -p.CrestWidthMm / 2d),
                    1 => (rc, -p.CrestWidthMm / 2d, rc, p.CrestWidthMm / 2d),
                    _ => (rc, p.CrestWidthMm / 2d, R, p.RootWidthMm / 2d)
                };
                var helixAxial = p.AxialStartMm + p.PitchMm * (uv.U - p.StartAngleRadians) / (2d * double.Pi);
                return (r0 + (r1 - r0) * uv.V, helixAxial + z0 + (z1 - z0) * uv.V);
            }
            var reverse = Theta(uv1) > Theta(uv2);
            var (va, vb) = reverse ? (v2, v1) : (v1, v2);
            var (ua, ub) = reverse ? (uv2, uv1) : (uv1, uv2);
            var (ta, tb) = (Theta(ua), Theta(ub));
            var (ra, sa) = Section(ua);
            var (rb, sb) = Section(ub);
            CurveGeometry curve;
            ParameterInterval trim;
            if (kind == ClipKind.Land || sideRole == 1)
            {
                var radius = kind == ClipKind.Land ? R : rc;
                var axial = kind == ClipKind.Land ? sC : sB;
                curve = CurveGeometry.FromCircle(new Circle3Curve(p.AxisOrigin + axis * axial, p.Axis, radius,
                    Direction3D.Create(Radial(ta))));
                trim = new(0d, tb - ta);
            }
            else
            {
                curve = CurveGeometry.FromBSpline(ConicalSpiral(ta, tb, ra, rb, sa, sb));
                trim = new(ta, tb);
            }
            var edge = AddEdge(va, vb, curve, trim, $"RunOut.Clip[{clips.Count}]");
            clips.Add(new(edge, va, vb, ta, tb, sa, sb, trim, reverse));
            return (edge, reverse, PcurveGeometry.Line(trim, ua, ub));
        }

        BSpline3Curve ConicalSpiral(double ta, double tb, double ra, double rb, double sa, double sb)
        {
            var dr = (rb - ra) / (tb - ta);
            var ds = (sb - sa) / (tb - ta);
            Point3D At(double t) => p.AxisOrigin + axis * (sa + ds * (t - ta)) + Radial(t) * (ra + dr * (t - ta));
            Vector3D Derivative(double t) => axis * ds + Radial(t) * dr + RadialDerivative(t) * (ra + dr * (t - ta));
            // r(t)·e^{it} has fourth derivative (r - 4i·r')·e^{it}; the axial part is linear.
            var bound = double.Max(ra, rb) + 4d * double.Abs(dr);
            var budget = ToleranceContext.Default.Linear / 2d;
            var count = (int)double.Max(1d, double.Ceiling((tb - ta) / double.Pow(384d * budget / bound, 0.25d)));
            var controls = new List<Point3D> { At(ta) };
            var knots = new List<double> { ta };
            var multiplicities = new List<int> { 4 };
            for (var i = 0; i < count; i++)
            {
                var a = ta + (tb - ta) * i / count;
                var b = i == count - 1 ? tb : ta + (tb - ta) * (i + 1) / count;
                var h = b - a;
                controls.Add(At(a) + Derivative(a) * (h / 3d));
                controls.Add(At(b) - Derivative(b) * (h / 3d));
                controls.Add(At(b));
                knots.Add(b);
                multiplicities.Add(i == count - 1 ? 4 : 3);
            }
            return new BSpline3Curve(3, controls, multiplicities, knots, "UNSPECIFIED", false, false, "UNSPECIFIED");
        }

        FaceId? ClippedFace(string role, SurfaceGeometryId surfaceId, ClipKind kind, int sideRole, params RawUse[] uses)
        {
            if (uses.All(use => rawG[use.Edge.From] > 0d && rawG[use.Edge.To] > 0d)) return null;
            var emitted = new List<(EdgeId Edge, bool Reverse, PcurveGeometry? Uv, int Start, int End,
                SurfaceParameterPoint UvStart, SurfaceParameterPoint UvEnd)>();
            foreach (var use in uses)
            {
                Resolve(use.Edge);
                if (use.Edge.Kept is not { } kept) continue;
                PcurveGeometry? uv = null;
                SurfaceParameterPoint a = default, b = default;
                if (use.Pcurve is { } original)
                {
                    a = original.Evaluate(use.Edge.KeptStart);
                    b = original.Evaluate(use.Edge.KeptEnd);
                    uv = PcurveGeometry.Line(new(use.Edge.KeptStart - use.Edge.Shift, use.Edge.KeptEnd - use.Edge.Shift), a, b);
                }
                emitted.Add(use.Reverse
                    ? (kept, true, uv, use.Edge.KeptTo, use.Edge.KeptFrom, b, a)
                    : (kept, false, uv, use.Edge.KeptFrom, use.Edge.KeptTo, a, b));
            }
            var loop = new List<(EdgeId Edge, bool Reverse, PcurveGeometry? Uv)>();
            var inserted = 0;
            for (var i = 0; i < emitted.Count; i++)
            {
                var current = emitted[i];
                loop.Add((current.Edge, current.Reverse, current.Uv));
                var next = emitted[(i + 1) % emitted.Count];
                if (current.End == next.Start) continue;
                if (kind == ClipKind.None) throw new InvalidOperationException($"Face {role} crosses the tip chamfer.");
                loop.Add(ClipEdge(kind, sideRole, current.End, next.Start, current.UvEnd, next.UvStart));
                inserted++;
            }
            if (inserted > 1) throw new InvalidOperationException($"Face {role} leaves the material more than once.");
            return MakeFace(role, surfaceId, loop);
        }

        var bottomCircle = new Circle3Curve(p.AxisOrigin + axis * p.SupportAxialMinMm, p.Axis, R, seamRadial);
        var bottomMiddle = RawVertex(bottomCircle.Evaluate(double.Pi));
        var rimHalf0 = new RawEdge(bottom, bottomMiddle, bottomCircle.Evaluate, CurveGeometry.FromCircle(bottomCircle), 0d, double.Pi, "Support.BottomRim.Half[0]");
        var rimHalf1 = new RawEdge(bottomMiddle, bottom, bottomCircle.Evaluate, CurveGeometry.FromCircle(bottomCircle), double.Pi, 2d * double.Pi, "Support.BottomRim.Half[1]");
        RawUse[] bottomRim = [new(rimHalf0, false, PcurveGeometry.Line(new(0d, double.Pi), bottomUv.Evaluate(0d), bottomUv.Evaluate(double.Pi))),
            new(rimHalf1, false, PcurveGeometry.Line(new(double.Pi, 2d * double.Pi), bottomUv.Evaluate(double.Pi), bottomUv.Evaluate(2d * double.Pi)))];

        if (new[] { bottom, section[0, 0], section[0, 1], section[0, 2], section[0, 3], section[1, 0] }.Any(v => rawG[v] > 0d))
            throw new InvalidOperationException("The thread is too short to run out through the tip; it must start before the chamfer.");
        ClippedFace("RootCylinderSkin.Lower", cylinderId, ClipKind.None, -1, Reversed([
            new RawUse(lowerSeam, false, CylinderVertical(lowerSeam, 0d)),
            new RawUse(helical[0, 0], false, CylinderHelix(helical[0, 0], 0, false)),
            new RawUse(gaps[1], true, CylinderVertical(gaps[1], 2d * double.Pi)),
            new RawUse(startRoot, true, CylinderVertical(startRoot, 2d * double.Pi)),
            new RawUse(lowerSeam, true, CylinderVertical(lowerSeam, 2d * double.Pi)),
            .. Reversed(bottomRim)]));

        for (var k = 1; k < turns; k++)
            ClippedFace($"RootCylinderSkin.Gap[{k}]", cylinderId, ClipKind.Land, -1, Reversed(
                new RawUse(gaps[k], false, CylinderVertical(gaps[k], 0d)),
                new RawUse(helical[k, 0], false, CylinderHelix(helical[k, 0], k, false)),
                new RawUse(gaps[k + 1], true, CylinderVertical(gaps[k + 1], 2d * double.Pi)),
                new RawUse(helical[k - 1, 3], true, CylinderHelix(helical[k - 1, 3], k - 1, true))));
        if (rawG[section[turns - 1, 3]] <= 0d)
            throw new InvalidOperationException("The last turn's trailing root must start beyond the chamfer.");

        for (var k = 0; k < turns; k++)
            for (var role = 0; role < 3; role++)
            {
                var u0 = p.StartAngleRadians + 2d * double.Pi * k;
                var u1 = u0 + 2d * double.Pi;
                var boundary0 = helical[k, role];
                var boundary1 = helical[k, role + 1];
                var interval = new ParameterInterval(boundary0.Start, boundary0.End);
                var face = ClippedFace($"{(HelicalRibSideRole)role}.Segment[{k}]", sideSurfaces[k, role], ClipKind.Side, role,
                    new RawUse(boundary0, false, PcurveGeometry.Line(interval, new(u0, 0d), new(u1, 0d))),
                    new RawUse(cross[k + 1, role], false, PcurveGeometry.Line(new(cross[k + 1, role].Start, cross[k + 1, role].End), new(u1, 0d), new(u1, 1d))),
                    new RawUse(boundary1, true, PcurveGeometry.Line(interval, new(u0, 1d), new(u1, 1d))),
                    new RawUse(cross[k, role], true, PcurveGeometry.Line(new(cross[k, role].Start, cross[k, role].End), new(u0, 0d), new(u0, 1d))));
                if (face is { } id) qualifiedSideFaces.Add(id, sideRealizations[k, role]);
            }

        var radialNormal = Direction3D.Create(axis.Cross(seamRadial.ToVector()) * -1d);
        var startPlaneId = new SurfaceGeometryId(nextSurface++);
        geometry.AddSurface(startPlaneId, SurfaceGeometry.FromPlane(new PlaneSurface(rawPoints[section[0, 0]],
            Direction3D.Create(-radialNormal.ToVector()), seamRadial)));
        ClippedFace("StartCap", startPlaneId, ClipKind.None, -1,
            new RawUse(cross[0, 0], false), new RawUse(cross[0, 1], false),
            new RawUse(cross[0, 2], false), new RawUse(startRoot, true));
        var bottomPlaneId = new SurfaceGeometryId(nextSurface++);
        geometry.AddSurface(bottomPlaneId, SurfaceGeometry.FromPlane(new PlaneSurface(
            p.AxisOrigin + axis * p.SupportAxialMinMm, Direction3D.Create(-axis), seamRadial)));
        ClippedFace("Support.BottomCap", bottomPlaneId, ClipKind.None, -1, Reversed(bottomRim.Select(use => use with { Pcurve = null }).ToArray()));

        // The chamfer cone: every clip edge, walked the other way, closes one loop around the axis. A generator from
        // the groove bottom's leading corner to the end rim cuts it open, as the cylinder skins are cut at their seams.
        var apexAxial = sC + R / m;
        var cone = new ConeSurface(p.AxisOrigin + axis * apexAxial, Direction3D.Create(-axis), double.Atan(m), seamRadial);
        var coneId = new SurfaceGeometryId(nextSurface++);
        geometry.AddSurface(coneId, SurfaceGeometry.FromCone(cone));
        SurfaceParameterPoint ConeUv(double theta, double axial) => new(-(theta - p.StartAngleRadians), apexAxial - axial);
        var byStart = new Dictionary<int, ClipRecord>();
        foreach (var clip in clips)
            byStart.Add(clip.FaceReverse ? clip.From : clip.To, clip);
        var corner = clipHelixRole.Single(pair => pair.Value == (int)HelicalRibBoundaryRole.LeadingCrest
            && materialized.ContainsKey(pair.Key)).Key;
        var coneUses = new List<(EdgeId Edge, bool Reverse, PcurveGeometry? Uv)>();
        var cornerRecord = byStart[corner];
        var cornerTheta = cornerRecord.FaceReverse ? cornerRecord.ThetaFrom : cornerRecord.ThetaTo;
        var theta = cornerTheta;
        var at = corner;
        do
        {
            var clip = byStart[at];
            var forward = clip.FaceReverse; // the cone walks against the groove face
            var span = clip.ThetaTo - clip.ThetaFrom;
            var thetaFrom = forward ? theta : theta - span;
            coneUses.Add((clip.Edge, !forward, PcurveGeometry.Line(clip.Trim,
                ConeUv(thetaFrom, clip.AxialFrom), ConeUv(thetaFrom + span, clip.AxialTo))));
            theta += forward ? span : -span;
            at = forward ? clip.To : clip.From;
        } while (at != corner && coneUses.Count <= clips.Count);
        if (coneUses.Count != clips.Count)
            throw new InvalidOperationException($"The groove's notch in the chamfer is not one loop ({coneUses.Count} of {clips.Count} edges).");
        var winding = theta - cornerTheta;
        if (double.Abs(winding - 2d * double.Pi) > 1e-6d)
            throw new InvalidOperationException($"The chamfer loop winds {winding:R} radians, not once forward around the axis.");
        var cornerPoint = rawPoints[corner];
        var rimStart = RawVertex(p.AxisOrigin + axis * tip.EndMm + Radial(cornerTheta) * tip.EndRadiusMm);
        var seamLength = (rawPoints[rimStart] - cornerPoint).Length;
        var seam = AddEdge(corner, rimStart, CurveGeometry.FromLine(new Line3Curve(cornerPoint,
            Direction3D.Create(rawPoints[rimStart] - cornerPoint))), new(0d, seamLength), "RunOut.ConeSeam");
        var rimCircle = new Circle3Curve(p.AxisOrigin + axis * tip.EndMm, p.Axis, tip.EndRadiusMm, Direction3D.Create(Radial(cornerTheta)));
        var rim = AddEdge(rimStart, rimStart, CurveGeometry.FromCircle(rimCircle), new(0d, 2d * double.Pi), "RunOut.EndRim");
        var cornerAxial = AxialOf(cornerPoint);
        coneUses.Add((seam, false, PcurveGeometry.Line(new(0d, seamLength),
            ConeUv(cornerTheta + winding, cornerAxial), ConeUv(cornerTheta + winding, tip.EndMm))));
        coneUses.Add((rim, true, PcurveGeometry.Line(new(0d, 2d * double.Pi),
            ConeUv(cornerTheta, tip.EndMm), ConeUv(cornerTheta + 2d * double.Pi, tip.EndMm))));
        coneUses.Add((seam, true, PcurveGeometry.Line(new(0d, seamLength),
            ConeUv(cornerTheta, cornerAxial), ConeUv(cornerTheta, tip.EndMm))));
        MakeFace("TipRunOut.Cone", coneId, coneUses);
        var endPlaneId = new SurfaceGeometryId(nextSurface++);
        geometry.AddSurface(endPlaneId, SurfaceGeometry.FromPlane(new PlaneSurface(
            p.AxisOrigin + axis * tip.EndMm, p.Axis, seamRadial)));
        MakeFace("TipRunOut.EndFace", endPlaneId, [(rim, false, null)]);

        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var validation = BrepBindingValidator.Validate(body, true);
        if (!validation.IsSuccess) return KernelResult<BrepHelicalRibResult>.Failure(validation.Diagnostics);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid)
            return Failure("preflight", string.Join("; ", preflight.Diagnostics
                .Where(d => d.Severity == BrepExportPreflightSeverity.Error)
                .Take(8).Select(d => d.Code + ": " + d.Message)));
        return KernelResult<BrepHelicalRibResult>.Success(new(rib, body, faceRoles, edgeRoles, qualifiedSideFaces, turns - 1));
    }
}
