using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Immutable realization authority for the admitted rounded-box family.  It is
/// constructed before the B-rep and its topology roles are consumed directly by
/// the materializer below.
/// </summary>
public sealed record RoundedBoxBRepPlan(
    double Width, double Depth, double Height, double CornerRadius, double? TopFilletRadius,
    IReadOnlyList<string> FaceRoles, IReadOnlyList<string> BoundaryRoles,
    string DeterministicSignature)
{
    public bool IsAuthoritative => true;
    public int ExpectedVertexCount => TopFilletRadius is null ? 16 : 24;
    public int ExpectedEdgeCount => TopFilletRadius is null ? 32 : 48;
    public int ExpectedFaceCount => TopFilletRadius is null ? 10 : 18;
    public int ExpectedLoopCount => ExpectedFaceCount;
    public int ExpectedCoedgeCount => TopFilletRadius is null ? 48 : 96;
}

public sealed record RoundedBoxRealization(RoundedBoxBRepPlan Plan, BrepBody Body);

public static class RoundedBoxBRepPlanner
{
    private const double Tol = 1e-8;
    private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1, 0, 0));
    private static readonly Direction3D PlusY = Direction3D.Create(new Vector3D(0, 1, 0));
    private static readonly Direction3D PlusZ = Direction3D.Create(new Vector3D(0, 0, 1));
    private static readonly Direction3D MinusZ = Direction3D.Create(new Vector3D(0, 0, -1));

    public static KernelResult<RoundedBoxRealization> Create(double width, double depth, double height, double cornerRadius, double? topFilletRadius = null)
    {
        var error = Validate(width, depth, height, cornerRadius, topFilletRadius);
        if (error is not null)
            return KernelResult<RoundedBoxRealization>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, error, "RoundedBox.Admission")]);

        var roles = new List<string> { "RoundedBoxBottom", "RoundedBoxTop" };
        roles.AddRange(new[] { "PlanarSide(+X)", "PlanarSide(-Y)", "PlanarSide(-X)", "PlanarSide(+Y)", "RoundedCornerWall(PX_NY)", "RoundedCornerWall(PX_PY)", "RoundedCornerWall(NX_PY)", "RoundedCornerWall(NX_NY)" });
        if (topFilletRadius is double)
        {
            roles.AddRange(Enumerable.Range(0, 4).Select(i => $"TopFilletStraight({i})"));
            roles.AddRange(Enumerable.Range(0, 4).Select(i => $"TopFilletToroidalCorner({i})"));
        }
        var boundaries = Enumerable.Range(0, 8).Select(i => $"TopBoundarySegment({i})").ToArray();
        var signatureSource = FormattableString.Invariant($"RoundedBox|{width:R}|{depth:R}|{height:R}|{cornerRadius:R}|{topFilletRadius?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "none"}|{string.Join(";", roles)}");
        var plan = new RoundedBoxBRepPlan(width, depth, height, cornerRadius, topFilletRadius, roles, boundaries, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureSource))).ToLowerInvariant());
        var body = Build(plan);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid)
            return KernelResult<RoundedBoxRealization>.Failure(preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, d.Code, d.Context)).ToArray());
        return KernelResult<RoundedBoxRealization>.Success(new RoundedBoxRealization(plan, body));
    }

    private static string? Validate(double w, double d, double h, double rc, double? rf)
    {
        if (!double.IsFinite(w) || !double.IsFinite(d) || !double.IsFinite(h) || w <= Tol || d <= Tol || h <= Tol) return "RoundedProfileDegenerate";
        if (!double.IsFinite(rc) || rc <= Tol) return "RoundedProfileDegenerate";
        if (rc >= double.Min(w, d) / 2d - Tol) return "RoundedBoxRadiusTooLarge";
        if (rf is null) return null;
        if (!double.IsFinite(rf.Value) || rf.Value <= Tol) return "TopFilletRadiusTooLarge";
        // r < Rc preserves a positive torus major radius; r < h preserves wall.
        if (rf.Value >= double.Min(rc, h) - Tol) return "TopFilletRadiusTooLarge";
        return null;
    }

    private static BrepBody Build(RoundedBoxBRepPlan plan)
    {
        var b = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var a = plan.Width / 2d; var c = plan.Depth / 2d; var z0 = -plan.Height / 2d; var z2 = plan.Height / 2d;
        var r = plan.CornerRadius; var rf = plan.TopFilletRadius ?? 0d; var z1 = z2 - rf;
        var outer = ProfilePoints(a, c, r, 0d);
        var inner = ProfilePoints(a, c, r, rf);
        var rings = plan.TopFilletRadius is null ? 2 : 3;
        var v = new VertexId[rings, 8];
        for (var ring = 0; ring < rings; ring++)
        {
            var z = ring == 0 ? z0 : ring == 1 ? z1 : z2;
            var profile = ring == 2 ? inner : outer;
            for (var i = 0; i < 8; i++) { v[ring, i] = b.AddVertex(); points[v[ring, i]] = new Point3D(profile[i].X, profile[i].Y, z); }
        }

        var bottom = new EdgeId[8]; var outerTop = new EdgeId[8]; var innerTop = plan.TopFilletRadius is null ? outerTop : new EdgeId[8];
        for (var i = 0; i < 8; i++)
        {
            bottom[i] = AddProfileEdge(0, i, v[0, i], v[0, (i + 1) % 8], outer, z0, r, b, geometry, bindings);
            outerTop[i] = AddProfileEdge(10, i, v[1, i], v[1, (i + 1) % 8], outer, z1, r, b, geometry, bindings);
            if (plan.TopFilletRadius is double)
                innerTop[i] = AddProfileEdge(20, i, v[2, i], v[2, (i + 1) % 8], inner, z2, r - rf, b, geometry, bindings);
        }
        var vertical = new EdgeId[8];
        for (var i = 0; i < 8; i++) vertical[i] = AddLine(v[0, i], v[1, i], points, b, geometry, bindings);
        var minor = plan.TopFilletRadius is double ? new EdgeId[8] : [];
        if (plan.TopFilletRadius is double)
            for (var i = 0; i < 8; i++) minor[i] = AddMinorArc(v[1, i], v[2, i], points, b, geometry, bindings);

        var bottomFace = AddLoopFace(b, Reverse(bottom));
        var topFace = AddLoopFace(b, Forward(innerTop));
        var faces = new List<FaceId> { bottomFace, topFace };
        BindSurface(bottomFace, SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, z0), MinusZ, PlusX)), geometry, bindings);
        BindSurface(topFace, SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, z2), PlusZ, PlusX)), geometry, bindings);

        for (var i = 0; i < 8; i++)
        {
            var side = AddLoopFace(b, [new(bottom[i], false), new(vertical[(i + 1) % 8], false), new(outerTop[i], true), new(vertical[i], true)]);
            faces.Add(side);
            BindSurface(side, SideSurface(i, outer[i], outer[(i + 1) % 8], r, a, c, z0), geometry, bindings);
        }
        if (plan.TopFilletRadius is double)
        {
            for (var i = 0; i < 8; i++)
            {
                var finish = AddLoopFace(b, [new(outerTop[i], false), new(minor[(i + 1) % 8], false), new(innerTop[i], true), new(minor[i], true)]);
                faces.Add(finish);
                BindSurface(finish, FinishSurface(i, outer[i], inner[i], r, rf, a, c, z1), geometry, bindings);
            }
        }
        var shell = b.AddShell(faces); b.AddBody([shell]);
        return new BrepBody(b.Model, geometry, bindings, points);
    }

    private static (double X, double Y)[] ProfilePoints(double a, double c, double r, double inset) =>
    [
        (a-r, c-inset), (a-inset, c-r), (a-inset, -c+r), (a-r, -c+inset),
        (-a+r, -c+inset), (-a+inset, -c+r), (-a+inset, c-r), (-a+r, c-inset)
    ];

    private static EdgeId AddProfileEdge(int family, int i, VertexId start, VertexId end, (double X, double Y)[] p, double z, double radius, TopologyBuilder b, BrepGeometryStore g, BrepBindingModel bindings)
    {
        var edge = b.AddEdge(start, end);
        if (i % 2 == 1)
        {
            var a = new Point3D(p[i].X, p[i].Y, z); var endP = new Point3D(p[(i + 1) % 8].X, p[(i + 1) % 8].Y, z);
            AddCurve(edge, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(endP - a))), 0, (endP - a).Length, g, bindings);
        }
        else
        {
            var center = CornerCenter(i, p, radius, z); var startP = new Point3D(p[i].X, p[i].Y, z);
            var circle = new Circle3Curve(center, MinusZ, radius, Direction3D.Create(startP - center));
            AddCurve(edge, CurveGeometry.FromCircle(circle), 0, double.Pi / 2d, g, bindings);
        }
        return edge;
    }

    private static EdgeId AddLine(VertexId start, VertexId end, Dictionary<VertexId, Point3D> points, TopologyBuilder b, BrepGeometryStore g, BrepBindingModel bindings)
    {
        var edge = b.AddEdge(start, end); var a = points[start]; var q = points[end];
        AddCurve(edge, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(q - a))), 0, (q - a).Length, g, bindings); return edge;
    }

    private static EdgeId AddMinorArc(VertexId start, VertexId end, Dictionary<VertexId, Point3D> points, TopologyBuilder b, BrepGeometryStore g, BrepBindingModel bindings)
    {
        var edge = b.AddEdge(start, end); var a = points[start]; var q = points[end];
        var center = new Point3D(q.X, q.Y, a.Z); var radial = Direction3D.Create(a - center);
        var circle = new Circle3Curve(center, Direction3D.Create(radial.ToVector().Cross(PlusZ.ToVector())), (a - center).Length, radial);
        AddCurve(edge, CurveGeometry.FromCircle(circle), 0, double.Pi / 2d, g, bindings); return edge;
    }

    private static SurfaceGeometry SideSurface(int i, (double X, double Y) start, (double X, double Y) end, double r, double a, double c, double z)
    {
        if (i % 2 == 0) return SurfaceGeometry.FromCylinder(new CylinderSurface(CornerCenter(i, ProfilePoints(a, c, r, 0), r, z), PlusZ, r, PlusX));
        var p = new Point3D(start.X, start.Y, z); var tangent = Direction3D.Create(new Vector3D(end.X - start.X, end.Y - start.Y, 0));
        // The profile runs clockwise when viewed from +Z, so +Z × tangent is
        // the material-outward planar normal. The previous tangent × +Z
        // convention inverted each straight wall and made its signed boundary
        // contribution cancel the caps/corner cylinders.
        var outward = Direction3D.Create(PlusZ.ToVector().Cross(tangent.ToVector()));
        return SurfaceGeometry.FromPlane(new PlaneSurface(p, outward, PlusZ));
    }

    private static SurfaceGeometry FinishSurface(int i, (double X, double Y) outer, (double X, double Y) inner, double rc, double rf, double a, double c, double z1)
    {
        if (i % 2 == 0)
            return SurfaceGeometry.FromTorus(new TorusSurface(CornerCenter(i, ProfilePoints(a, c, rc, 0), rc, z1), PlusZ, rc - rf, rf, PlusX));
        var tangent = Direction3D.Create(new Vector3D((i == 1 ? 0 : i == 3 ? -1 : i == 5 ? 0 : 1), (i == 1 ? -1 : i == 3 ? 0 : i == 5 ? 1 : 0), 0));
        return SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(inner.X, inner.Y, z1), tangent, rf, Direction3D.Create(new Point3D(outer.X, outer.Y, z1) - new Point3D(inner.X, inner.Y, z1))));
    }

    private static Point3D CornerCenter(int i, (double X, double Y)[] p, double radius, double z)
    {
        var s = p[i]; var e = p[(i + 1) % 8];
        // Arc end/start normals identify the unique axis-aligned corner center.
        return i switch
        {
            0 => new Point3D(s.X, e.Y, z), 2 => new Point3D(e.X, s.Y, z),
            4 => new Point3D(s.X, e.Y, z), _ => new Point3D(e.X, s.Y, z)
        };
    }

    private static void AddCurve(EdgeId edge, CurveGeometry curve, double start, double end, BrepGeometryStore g, BrepBindingModel bindings)
    { var id = new CurveGeometryId(g.Curves.Count() + 1); g.AddCurve(id, curve); bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, id, new ParameterInterval(start, end))); }
    private static void BindSurface(FaceId face, SurfaceGeometry surface, BrepGeometryStore g, BrepBindingModel bindings)
    { var id = new SurfaceGeometryId(g.Surfaces.Count() + 1); g.AddSurface(id, surface); bindings.AddFaceBinding(new FaceGeometryBinding(face, id)); }
    private static FaceId AddLoopFace(TopologyBuilder b, IReadOnlyList<Use> uses)
    { var loop = b.AllocateLoopId(); var ids = uses.Select(_ => b.AllocateCoedgeId()).ToArray(); for (var i = 0; i < ids.Length; i++) b.AddCoedge(new Coedge(ids[i], uses[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse)); b.AddLoop(new Loop(loop, ids)); return b.AddFace([loop]); }
    private static Use[] Forward(IReadOnlyList<EdgeId> edges) => edges.Select(e => new Use(e, false)).ToArray();
    private static Use[] Reverse(IReadOnlyList<EdgeId> edges) => edges.Reverse().Select(e => new Use(e, true)).ToArray();
    private readonly record struct Use(EdgeId Edge, bool Reverse);
}
