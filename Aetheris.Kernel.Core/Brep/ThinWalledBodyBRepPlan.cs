using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Single topology authority for an admitted open thin-walled vessel.  The body is a closed
/// manifold boundary around material; the top is an opening into its cavity, closed by a rim.
/// It is intentionally built from paired supports, never inferred from occupancy subtraction.
/// </summary>
public sealed record ThinWalledBodyBRepPlan(
    string PrimitiveKind,
    IReadOnlyList<string> OuterFaces,
    IReadOnlyList<string> InnerFaces,
    IReadOnlyList<string> RimFaces,
    IReadOnlyList<string> ClosedFaces,
    IReadOnlyList<string> OpeningLoops,
    IReadOnlyList<ThinWallThicknessWitness> ThicknessWitnesses,
    string DeterministicSignature)
{
    public bool IsAuthoritative => true;
    public string Kind => "ThinWalledBody";
}

public sealed record ThinWalledBodyRealization(
    HollowBodyFeature Feature,
    ThinWalledBodyConstruction Construction,
    ThinWalledBodyBRepPlan Plan,
    BrepBody Body);

public static class ThinWalledBodyBRepPlanner
{
    private const double Tol = 1e-8;
    private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1, 0, 0));
    private static readonly Direction3D PlusY = Direction3D.Create(new Vector3D(0, 1, 0));
    private static readonly Direction3D PlusZ = Direction3D.Create(new Vector3D(0, 0, 1));
    private static readonly Direction3D MinusZ = Direction3D.Create(new Vector3D(0, 0, -1));

    public static KernelResult<ThinWalledBodyRealization> CreateRoundedBox(double width, double depth, double height, double cornerRadius, double wallThickness)
    {
        if (!FinitePositive(width, depth, height, cornerRadius, wallThickness)) return Failure("RoundedBoxInnerBoundaryDegenerate");
        if (width <= 2d * wallThickness + Tol || depth <= 2d * wallThickness + Tol || height <= wallThickness + Tol) return Failure("WallThicknessTooLarge");
        if (cornerRadius <= wallThickness + Tol) return Failure("RoundedBoxInnerRadiusDegenerate");
        if (cornerRadius >= double.Min(width, depth) / 2d - Tol) return Failure("RoundedBoxRadiusTooLarge");

        var witness = new HollowConstructionWitness("InsetRoundedProfile", true,
            "innerWidth=outerWidth-2T; innerDepth=outerDepth-2T; innerRadius=outerRadius-T; innerBottom=outerBottom+T; innerTop=outerTop",
            ["OuterPlanarWall -> InnerPlanarWall", "OuterCornerCylinder -> InnerCornerCylinder", "OuterBottomPlane -> InnerBottomPlane"],
            ["T>0", "width>2T", "depth>2T", "height>T", "cornerRadius>T"]);
        var feature = new HollowBodyFeature("RoundedBox", new Dictionary<string, double> { ["Width"] = width, ["Depth"] = depth, ["Height"] = height, ["CornerRadius"] = cornerRadius }, wallThickness, ["Top"], "ConstantNormalThickness", witness, "Firmament Primitive<Hollow>");
        var thickness = new List<ThinWallThicknessWitness>();
        thickness.AddRange(Enumerable.Range(0, 4).Select(i => new ThinWallThicknessWitness($"PlanarWall({i})", "Plane", "Plane", wallThickness, "inward normal")));
        thickness.AddRange(Enumerable.Range(0, 4).Select(i => new ThinWallThicknessWitness($"CornerWall({i})", "Cylinder", "Cylinder", wallThickness, "coaxial radial inward")));
        thickness.Add(new ThinWallThicknessWitness("Bottom", "Plane", "Plane", wallThickness, "+Z"));
        var construction = new ThinWalledBodyConstruction(feature,
            ["OuterBottom", .. Enumerable.Range(0, 8).Select(i => $"OuterWall({i})")],
            ["InnerBottom", .. Enumerable.Range(0, 8).Select(i => $"InnerWall({i})")],
            Enumerable.Range(0, 8).Select(i => $"Rim({i})").ToArray(), ["OuterBottom", "InnerBottom"], thickness);
        var plan = MakePlan("RoundedBox", construction, 8, 8, 8, 2, $"{width:R}|{depth:R}|{height:R}|{cornerRadius:R}|{wallThickness:R}");
        var body = BuildRoundedBox(width, depth, height, cornerRadius, wallThickness);
        return Validate(feature, construction, plan, body);
    }

    public static KernelResult<ThinWalledBodyRealization> CreateFrustum(double bottomRadius, double topRadius, double height, double wallThickness)
    {
        if (!FinitePositive(bottomRadius, topRadius, height, wallThickness) || double.Abs(topRadius - bottomRadius) <= Tol) return Failure("FrustumOffsetConeDegenerate");
        var k = (topRadius - bottomRadius) / height;
        var normalScale = double.Sqrt(1d + k * k);
        // Parallel-line form at fixed z.  The horizontal decrement is larger than T for a sloped cone,
        // which is precisely why this does not use naive radial shrinking.
        var innerBottom = bottomRadius + k * wallThickness - wallThickness * normalScale;
        var innerTop = topRadius - wallThickness * normalScale;
        if (innerBottom <= Tol) return Failure("FrustumInnerBottomInvalid");
        if (innerTop <= Tol) return Failure("FrustumInnerTopInvalid");
        var witness = new HollowConstructionWitness("ParallelConicalOffset", true,
            "rInner(z)=Rb+kz-T*sqrt(1+k^2), trimmed at z=T and z=H",
            ["OuterConicalWall -> InnerConicalWall", "OuterBottomPlane -> InnerBottomPlane"],
            ["T>0", "frustum is non-cylindrical", "inner bottom radius>0", "inner top radius>0"]);
        var feature = new HollowBodyFeature("Frustum", new Dictionary<string, double> { ["BottomRadius"] = bottomRadius, ["TopRadius"] = topRadius, ["Height"] = height }, wallThickness, ["Top"], "ConstantNormalThickness", witness, "Firmament Primitive<Hollow>");
        var thickness = new[]
        {
            new ThinWallThicknessWitness("ConicalWall", "Cone", "Cone", wallThickness, "inward support normal"),
            new ThinWallThicknessWitness("Bottom", "Plane", "Plane", wallThickness, "+Z")
        };
        var construction = new ThinWalledBodyConstruction(feature, ["OuterConicalWall", "OuterBottom"], ["InnerConicalWall", "InnerBottom"], ["TopAnnularRim"], ["OuterBottom", "InnerBottom"], thickness);
        var plan = MakePlan("Frustum", construction, 2, 2, 1, 2, $"{bottomRadius:R}|{topRadius:R}|{height:R}|{wallThickness:R}|{innerBottom:R}|{innerTop:R}");
        var body = BuildFrustum(bottomRadius, topRadius, height, wallThickness, innerBottom, innerTop, k);
        return Validate(feature, construction, plan, body);
    }

    private static KernelResult<ThinWalledBodyRealization> Validate(HollowBodyFeature feature, ThinWalledBodyConstruction construction, ThinWalledBodyBRepPlan plan, BrepBody body)
    {
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return KernelResult<ThinWalledBodyRealization>.Failure(preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, d.Code, d.Context)).ToArray());
        return KernelResult<ThinWalledBodyRealization>.Success(new ThinWalledBodyRealization(feature, construction, plan, body));
    }

    private static ThinWalledBodyBRepPlan MakePlan(string primitive, ThinWalledBodyConstruction construction, int outer, int inner, int rim, int closed, string parameters)
    {
        var outerFaces = construction.OuterBoundaryRoles; var innerFaces = construction.InnerBoundaryRoles; var rimFaces = construction.RimRoles; var closedFaces = construction.ClosedRegionRoles;
        var source = $"ThinWalledBody|{primitive}|{parameters}|{string.Join(';', outerFaces)}|{string.Join(';', innerFaces)}|{string.Join(';', rimFaces)}";
        return new(primitive, outerFaces, innerFaces, rimFaces, closedFaces, ["Opening(Top)"], construction.ThicknessWitnesses, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant());
    }

    private static BrepBody BuildRoundedBox(double width, double depth, double height, double radius, double t)
    {
        var b = new TopologyBuilder(); var g = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var points = new Dictionary<VertexId, Point3D>();
        var outer = ProfilePoints(width / 2d, depth / 2d, radius); var inner = ProfilePoints(width / 2d - t, depth / 2d - t, radius - t);
        var v = new VertexId[4, 8]; var profiles = new[] { outer, outer, inner, inner }; var zs = new[] { 0d, height, t, height };
        for (var ring = 0; ring < 4; ring++) for (var i = 0; i < 8; i++) { v[ring, i] = b.AddVertex(); points[v[ring, i]] = new Point3D(profiles[ring][i].X, profiles[ring][i].Y, zs[ring]); }
        var edges = new EdgeId[4, 8];
        for (var ring = 0; ring < 4; ring++) for (var i = 0; i < 8; i++) edges[ring, i] = AddProfileEdge(v[ring, i], v[ring, (i + 1) % 8], profiles[ring], zs[ring], ring < 2 ? radius : radius - t, i, b, g, bindings);
        var outerVertical = new EdgeId[8]; var innerVertical = new EdgeId[8]; var rim = new EdgeId[8];
        for (var i = 0; i < 8; i++) { outerVertical[i] = AddLine(v[0, i], v[1, i], points, b, g, bindings); innerVertical[i] = AddLine(v[2, i], v[3, i], points, b, g, bindings); rim[i] = AddLine(v[1, i], v[3, i], points, b, g, bindings); }
        var faces = new List<FaceId>();
        var outerBottom = AddFace(b, [Reverse(Row(edges, 0))]); BindPlane(outerBottom, new Point3D(0, 0, 0), MinusZ, g, bindings); faces.Add(outerBottom);
        var innerBottom = AddFace(b, [Forward(Row(edges, 2))]); BindPlane(innerBottom, new Point3D(0, 0, t), PlusZ, g, bindings, false); faces.Add(innerBottom);
        for (var i = 0; i < 8; i++) { var face = AddFace(b, [[new(edges[0, i], false), new(outerVertical[(i + 1) % 8], false), new(edges[1, i], true), new(outerVertical[i], true)]]); BindSurface(face, SideSurface(i, outer, radius, 0d), g, bindings); faces.Add(face); }
        for (var i = 0; i < 8; i++) { var face = AddFace(b, [[new(edges[2, i], false), new(innerVertical[(i + 1) % 8], false), new(edges[3, i], true), new(innerVertical[i], true)]]); BindSurface(face, SideSurface(i, inner, radius - t, t), g, bindings, false); faces.Add(face); }
        for (var i = 0; i < 8; i++) { var face = AddFace(b, [[new(edges[1, i], false), new(rim[(i + 1) % 8], false), new(edges[3, i], true), new(rim[i], true)]]); BindPlane(face, new Point3D(0, 0, height), PlusZ, g, bindings); faces.Add(face); }
        var shell = b.AddShell(faces); b.AddBody([shell]); return new BrepBody(b.Model, g, bindings, points);
    }

    private static BrepBody BuildFrustum(double rb, double rt, double h, double t, double innerBottom, double innerTop, double k)
    {
        var b = new TopologyBuilder(); var g = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var points = new Dictionary<VertexId, Point3D>();
        var radii = new[] { rb, rt, innerBottom, innerTop }; var zs = new[] { 0d, h, t, h }; var vertices = new VertexId[4]; var circles = new EdgeId[4];
        for (var i = 0; i < 4; i++) { vertices[i] = b.AddVertex(); points[vertices[i]] = new Point3D(radii[i], 0, zs[i]); circles[i] = AddCircle(vertices[i], radii[i], zs[i], b, g, bindings); }
        var outerCone = AddFace(b, [Forward([circles[0]]), Reverse([circles[1]])]);
        var innerCone = AddFace(b, [Forward([circles[2]]), Reverse([circles[3]])]);
        var outerBottom = AddFace(b, [Reverse([circles[0]])]);
        var innerBottomFace = AddFace(b, [Forward([circles[2]])]);
        var rim = AddFace(b, [Forward([circles[1]]), Reverse([circles[3]])]);
        var outerSupport = Cone(rb, k); var innerSupport = Cone(rb - t * double.Sqrt(1d + k * k), k);
        BindSurface(outerCone, SurfaceGeometry.FromCone(outerSupport), g, bindings);
        BindSurface(innerCone, SurfaceGeometry.FromCone(innerSupport), g, bindings, false);
        BindPlane(outerBottom, new Point3D(0, 0, 0), MinusZ, g, bindings);
        BindPlane(innerBottomFace, new Point3D(0, 0, t), PlusZ, g, bindings, false);
        BindPlane(rim, new Point3D(0, 0, h), PlusZ, g, bindings);
        var shell = b.AddShell([outerCone, innerCone, outerBottom, innerBottomFace, rim]); b.AddBody([shell]); return new BrepBody(b.Model, g, bindings, points);
    }

    private static ConeSurface Cone(double intercept, double slope)
    {
        var apex = new Point3D(0, 0, -intercept / slope); var axis = slope > 0 ? PlusZ : MinusZ;
        return new ConeSurface(apex, axis, double.Atan(double.Abs(slope)), PlusX);
    }

    private static bool FinitePositive(params double[] values) => values.All(v => double.IsFinite(v) && v > Tol);
    private static KernelResult<ThinWalledBodyRealization> Failure(string code) => KernelResult<ThinWalledBodyRealization>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, code, "ThinWalledBody.Admission")]);
    private static (double X, double Y)[] ProfilePoints(double a, double c, double r) => [(a-r,c), (a,c-r), (a,-c+r), (a-r,-c), (-a+r,-c), (-a,-c+r), (-a,c-r), (-a+r,c)];
    private static EdgeId[] Row(EdgeId[,] edges, int row) => Enumerable.Range(0, 8).Select(i => edges[row, i]).ToArray();
    private static EdgeId AddProfileEdge(VertexId start, VertexId end, (double X, double Y)[] p, double z, double radius, int i, TopologyBuilder b, BrepGeometryStore g, BrepBindingModel bindings)
    {
        var edge = b.AddEdge(start, end);
        if (i % 2 == 1) { var a = new Point3D(p[i].X, p[i].Y, z); var q = new Point3D(p[(i + 1) % 8].X, p[(i + 1) % 8].Y, z); AddCurve(edge, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(q - a))), 0, (q - a).Length, g, bindings); }
        else { var center = CornerCenter(i, p, z); var a = new Point3D(p[i].X, p[i].Y, z); AddCurve(edge, CurveGeometry.FromCircle(new Circle3Curve(center, MinusZ, radius, Direction3D.Create(a - center))), 0, double.Pi / 2d, g, bindings); }
        return edge;
    }
    private static EdgeId AddCircle(VertexId vertex, double radius, double z, TopologyBuilder b, BrepGeometryStore g, BrepBindingModel bindings) { var edge = b.AddEdge(vertex, vertex); AddCurve(edge, CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, z), PlusZ, radius, PlusX)), 0, 2d * double.Pi, g, bindings); return edge; }
    private static EdgeId AddLine(VertexId start, VertexId end, IReadOnlyDictionary<VertexId, Point3D> points, TopologyBuilder b, BrepGeometryStore g, BrepBindingModel bindings) { var edge=b.AddEdge(start,end); var a=points[start]; var q=points[end]; AddCurve(edge,CurveGeometry.FromLine(new Line3Curve(a,Direction3D.Create(q-a))),0,(q-a).Length,g,bindings); return edge; }
    private static Point3D CornerCenter(int i, (double X, double Y)[] p, double z) => i switch { 0 => new(p[i].X,p[(i+1)%8].Y,z), 2 => new(p[(i+1)%8].X,p[i].Y,z), 4 => new(p[i].X,p[(i+1)%8].Y,z), _ => new(p[(i+1)%8].X,p[i].Y,z) };
    private static SurfaceGeometry SideSurface(int i, (double X, double Y)[] profile, double radius, double z) { if (i % 2 == 0) return SurfaceGeometry.FromCylinder(new CylinderSurface(CornerCenter(i, profile, z), PlusZ, radius, PlusX)); var a=new Point3D(profile[i].X,profile[i].Y,z); var q=new Point3D(profile[(i+1)%8].X,profile[(i+1)%8].Y,z); var tangent=Direction3D.Create(q-a); return SurfaceGeometry.FromPlane(new PlaneSurface(a,Direction3D.Create(tangent.ToVector().Cross(PlusZ.ToVector())),PlusZ)); }
    private static void AddCurve(EdgeId edge, CurveGeometry curve, double start, double end, BrepGeometryStore g, BrepBindingModel bindings) { var id=new CurveGeometryId(g.Curves.Count()+1); g.AddCurve(id,curve); bindings.AddEdgeBinding(new EdgeGeometryBinding(edge,id,new ParameterInterval(start,end))); }
    private static void BindSurface(FaceId face, SurfaceGeometry surface, BrepGeometryStore g, BrepBindingModel bindings, bool sameSense=true) { var id=new SurfaceGeometryId(g.Surfaces.Count()+1); g.AddSurface(id,surface); bindings.AddFaceBinding(new FaceGeometryBinding(face,id,sameSense)); }
    private static void BindPlane(FaceId face, Point3D origin, Direction3D normal, BrepGeometryStore g, BrepBindingModel bindings, bool sameSense=true) => BindSurface(face, SurfaceGeometry.FromPlane(new PlaneSurface(origin,normal,PlusX)),g,bindings,sameSense);
    private static FaceId AddFace(TopologyBuilder b, IReadOnlyList<IReadOnlyList<Use>> loops) { var ids=new List<LoopId>(); foreach(var uses in loops) { var loop=b.AllocateLoopId(); var coedges=uses.Select(_=>b.AllocateCoedgeId()).ToArray(); for(var i=0;i<coedges.Length;i++) b.AddCoedge(new Coedge(coedges[i],uses[i].Edge,loop,coedges[(i+1)%coedges.Length],coedges[(i+coedges.Length-1)%coedges.Length],uses[i].Reverse)); b.AddLoop(new Loop(loop,coedges)); ids.Add(loop); } return b.AddFace(ids); }
    private static Use[] Forward(IReadOnlyList<EdgeId> edges) => edges.Select(e=>new Use(e,false)).ToArray();
    private static Use[] Reverse(IReadOnlyList<EdgeId> edges) => edges.Reverse().Select(e=>new Use(e,true)).ToArray();
    private readonly record struct Use(EdgeId Edge, bool Reverse);
}
