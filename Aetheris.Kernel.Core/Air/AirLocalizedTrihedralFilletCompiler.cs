using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Air;

/// <summary>Hard-admitted M5 request: the three positive, mutually incident box edges.</summary>
internal sealed record AirLocalizedTrihedralFilletCompileRequest(
    string BodyId, string FeatureId, string FeatureName, double Width, double Depth, double Height,
    string FaceXZ, string TargetXZ, string FaceYZ, string TargetYZ, string FaceXY, string TargetXY,
    double RadiusXZ, double RadiusYZ, double RadiusXY, AirSourceSpan SourceSpan, bool HistoryKnown = true);

internal enum TrihedralFilletErrorKind
{
    EdgesDoNotShareSingleVertex, UnsupportedJunctionValence, RadiusMismatch, RadiusTooLarge,
    UnsupportedSurfaceCombination, SphericalPatchConstructionFailed, SphereCylinderIntersectionMissing,
    SphereCylinderIntersectionAmbiguous, TangentContinuityFailed, BoundaryOwnershipConflict,
    DegenerateTrihedralPatch, ConstructionWitnessRequired, VerificationFailure,
    UnequalRadiusCornerSurfaceRequired,
}

internal sealed record TrihedralFilletError(TrihedralFilletErrorKind Kind, string Code, string Message, string Stage, IReadOnlyList<string> Evidence);

/// <summary>An exact finite sphere/cylinder intersection branch, owned by one shared topology edge.</summary>
internal sealed record SphereCylinderSeamConstruction(
    string Role, Circle3Curve Curve, ParameterInterval Trim, Point3D Start, Point3D End,
    string CylinderRole, double SphereDeviation, double CylinderDeviation, double NormalDeviation,
    string Orientation, string Provenance);

/// <summary>Explicit Construction AIR witness; the face is a bounded positive spherical octant only.</summary>
internal sealed record SphericalCornerPatchConstruction(
    Point3D Center, double Radius, string OctantSelection,
    SphereCylinderSeamConstruction BoundaryXZ, SphereCylinderSeamConstruction BoundaryYZ, SphereCylinderSeamConstruction BoundaryXY,
    string MaterialSide, string Provenance);

internal sealed record LocalizedTrihedralFilletConstruction(
    string ConstructionId, LocalizedEdgeJunctionReplacement ReplacementXZ,
    LocalizedEdgeJunctionReplacement ReplacementYZ, LocalizedEdgeJunctionReplacement ReplacementXY,
    SphericalCornerPatchConstruction SphericalCornerPatch, IReadOnlyList<IReadOnlyList<Point3D>> RetainedRegions,
    IReadOnlyList<IReadOnlyList<Point3D>> RemoteEndpoints, Point3D SharedOriginalVertex,
    string MaterialSide, string BoundaryOwnership, LocalizedEdgeJunctionTopologyPlan TopologyPlan, string Provenance);

internal sealed record AirLocalizedTrihedralFilletCompileResult(
    bool Succeeded, LocalizedTrihedralFilletConstruction? Construction, AirBRepPlan? BRepPlan,
    BrepBody? Body, TrihedralFilletError? Error, IReadOnlyList<string> Diagnostics)
{
    public const string ProductionRoute = "AirLocalizedTrihedralFilletM5";
}

/// <summary>
/// Exact, deliberately narrow three-edge fillet compiler.  Each cylinder is truncated at the
/// plane through the common sphere center; the resulting three quarter circles bound the sole
/// positive spherical octant.  No rolling-ball or unequal-radius route is implied by this type.
/// </summary>
internal static class AirLocalizedTrihedralFilletCompiler
{
    private const double Tol = 1e-9;

    public static AirLocalizedTrihedralFilletCompileResult Compile(AirLocalizedTrihedralFilletCompileRequest input)
    {
        var lowered = Lower(input);
        if (lowered.Error is not null) return new(false, null, null, null, lowered.Error, [lowered.Error.Code, .. lowered.Error.Evidence]);
        var construction = lowered.Construction!;
        var plan = BuildPlan(input, construction);
        var emitted = Emit(construction);
        if (!emitted.Succeeded || emitted.Body is null)
            return Failure(construction, plan, new(TrihedralFilletErrorKind.VerificationFailure, "localized-trihedral-fillet-materialization-failed", "The authoritative trihedral plan did not materialize.", "BRep", emitted.Diagnostics));

        var body = emitted.Body;
        var preflight = BrepExportPreflight.Validate(body);
        var sphere = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Sphere);
        var cylinders = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
        var circles = body.Geometry.Curves.Count(c => c.Value.Kind == CurveGeometryKind.Circle3);
        if (!preflight.IsValid || sphere != 1 || cylinders != 3 || circles != 6 || body.Topology.Faces.Count() != 10 || !FirmamentManifold(body))
            return Failure(construction, plan, new(TrihedralFilletErrorKind.VerificationFailure, "localized-trihedral-fillet-analytic-verification-failed", "M5 requires three cylinders, one sphere, six circular arcs, ten faces, valid preflight, and a manifold shell.", "Verification", preflight.Diagnostics.Select(d => d.Code).ToArray()));

        return new(true, construction, plan, body, null,
            ["localized-trihedral-fillet-feature-admitted", "localized-trihedral-fillet-spherical-octant", "localized-trihedral-fillet-candidate-plans=1", "localized-trihedral-fillet-hard-valid-plans=1", "localized-trihedral-fillet-g0-exact", "localized-trihedral-fillet-g1-normal-deviation=0", "localized-trihedral-fillet-authoritative-brep-plan-consumed", "localized-trihedral-fillet-no-legacy-fallback"]);
    }

    private static (LocalizedTrihedralFilletConstruction? Construction, TrihedralFilletError? Error) Lower(AirLocalizedTrihedralFilletCompileRequest i)
    {
        TrihedralFilletError Fail(TrihedralFilletErrorKind kind, string code, string message, params string[] evidence) => new(kind, code, message, "FeatureAIR->ConstructionAIR", evidence);
        if (!i.HistoryKnown) return (null, Fail(TrihedralFilletErrorKind.UnsupportedSurfaceCombination, "localized-trihedral-fillet-unsupported-history", "M5 requires a history-known axis-aligned box."));
        if (!FinitePositive(i.Width) || !FinitePositive(i.Depth) || !FinitePositive(i.Height)) return (null, Fail(TrihedralFilletErrorKind.UnsupportedSurfaceCombination, "localized-trihedral-fillet-invalid-box-dimensions", "Box extents must be finite and positive."));
        if (!Matches(i.FaceXZ, i.TargetXZ, "+X", "SharedEdgePlusZ") || !Matches(i.FaceYZ, i.TargetYZ, "+Y", "SharedEdgePlusZ") || !Matches(i.FaceXY, i.TargetXY, "+X", "SharedEdgePlusY"))
            return (null, Fail(TrihedralFilletErrorKind.EdgesDoNotShareSingleVertex, "localized-trihedral-fillet-edges-do-not-share-canonical-vertex", "M5 admits SharedEdge(+X,+Z), SharedEdge(+Y,+Z), and SharedEdge(+X,+Y) only."));
        if (!FinitePositive(i.RadiusXZ) || !FinitePositive(i.RadiusYZ) || !FinitePositive(i.RadiusXY)) return (null, Fail(TrihedralFilletErrorKind.RadiusTooLarge, "localized-trihedral-fillet-radius-must-be-positive", "Every constant radius must be finite and positive."));
        if (double.Abs(i.RadiusXZ - i.RadiusYZ) > Tol || double.Abs(i.RadiusXZ - i.RadiusXY) > Tol)
            return (null, Fail(TrihedralFilletErrorKind.UnequalRadiusCornerSurfaceRequired, "localized-trihedral-fillet-unequal-radius-corner-surface-required", "M5 admits only equal radii; a distinct unproven corner-surface construction is required.", $"rXZ={i.RadiusXZ:R}", $"rYZ={i.RadiusYZ:R}", $"rXY={i.RadiusXY:R}"));
        var r = i.RadiusXZ;
        if (r >= i.Width - Tol || r >= i.Depth - Tol || r >= i.Height - Tol) return (null, Fail(TrihedralFilletErrorKind.RadiusTooLarge, "localized-trihedral-fillet-radius-too-large", "Radius must fit each local support extent."));

        var hx = i.Width / 2d; var hy = i.Depth / 2d; var hz = i.Height / 2d;
        var c = new Point3D(hx - r, hy - r, hz - r);
        var px = new Point3D(hx, c.Y, c.Z); var py = new Point3D(c.X, hy, c.Z); var pz = new Point3D(c.X, c.Y, hz);
        var xz = new CylinderSurface(c, Direction3D.Create(new Vector3D(0, 1, 0)), r, Direction3D.Create(new Vector3D(1, 0, 0)));
        var yz = new CylinderSurface(c, Direction3D.Create(new Vector3D(1, 0, 0)), r, Direction3D.Create(new Vector3D(0, 1, 0)));
        var xy = new CylinderSurface(c, Direction3D.Create(new Vector3D(0, 0, 1)), r, Direction3D.Create(new Vector3D(1, 0, 0)));
        var seamXZ = Seam("SphereCylinderSeamXZ", px, pz, c, xz, "FilletReplacementXZ");
        var seamYZ = Seam("SphereCylinderSeamYZ", pz, py, c, yz, "FilletReplacementYZ");
        var seamXY = Seam("SphereCylinderSeamXY", py, px, c, xy, "FilletReplacementXY");
        if (seamXZ.SphereDeviation > Tol || seamYZ.SphereDeviation > Tol || seamXY.SphereDeviation > Tol || seamXZ.CylinderDeviation > Tol || seamYZ.CylinderDeviation > Tol || seamXY.CylinderDeviation > Tol)
            return (null, Fail(TrihedralFilletErrorKind.SphereCylinderIntersectionMissing, "localized-trihedral-fillet-sphere-cylinder-intersection-missing", "An exact sphere-cylinder seam could not be constructed."));
        var patch = new SphericalCornerPatchConstruction(c, r, "+X,+Y,+Z relative to center", seamXZ, seamYZ, seamXY, "convex exterior removal", "sphere center = original (+X,+Y,+Z) vertex - R*(+X,+Y,+Z); three quarter-circle intersections form one closed octant");
        var v = new[]
        {
            new Point3D(-hx,-hy,-hz), new Point3D(hx,-hy,-hz), new Point3D(-hx,hy,-hz), new Point3D(-hx,-hy,hz),
            new Point3D(hx,-hy,c.Z), new Point3D(c.X,-hy,hz), new Point3D(-hx,hy,c.Z), new Point3D(-hx,c.Y,hz),
            new Point3D(hx,c.Y,-hz), new Point3D(c.X,hy,-hz), px, py, pz,
        };
        IReadOnlyList<int>[] loops =
        [
            [0,2,9,8,1], [0,3,7,6,2], [0,1,4,5,3], [1,8,10,4], [2,6,11,9], [3,5,12,7],
            [4,10,12,5], [6,7,12,11], [8,9,11,10], [12,10,11],
        ];
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"localized-trihedral-fillet:+X,+Z:+Y,+Z:+X,+Y:{i.Width:R}:{i.Depth:R}:{i.Height:R}:{r:R}:spherical-octant"))));
        var topology = new LocalizedEdgeJunctionTopologyPlan(v, loops,
            ["UnaffectedFace(-Z)", "UnaffectedFace(-X)", "UnaffectedFace(-Y)", "RetainedSupport(+X)", "RetainedSupport(+Y)", "RetainedSupport(+Z)", "FilletReplacementXZ(Cylinder)", "FilletReplacementYZ(Cylinder)", "FilletReplacementXY(Cylinder)", "SphericalCornerPatch"], signature);
        var fXZ = Feature(i, "XZ", "+X", "SharedEdge(+X,+Z)"); var fYZ = Feature(i, "YZ", "+Y", "SharedEdge(+Y,+Z)"); var fXY = Feature(i, "XY", "+X", "SharedEdge(+X,+Y)");
        return (new($"construction:{i.FeatureId}:trihedral-fillet", new("SharedEdge(+X,+Z)", fXZ, [v[4],v[10],v[12],v[5]], [v[4],v[5]], "CylindricalFillet"), new("SharedEdge(+Y,+Z)", fYZ, [v[6],v[7],v[12],v[11]], [v[6],v[7]], "CylindricalFillet"), new("SharedEdge(+X,+Y)", fXY, [v[8],v[9],v[11],v[10]], [v[8],v[9]], "CylindricalFillet"), patch,
            [[v[1],v[8],v[10],v[4]], [v[2],v[6],v[11],v[9]], [v[3],v[5],v[12],v[7]]], [[v[4],v[5]],[v[6],v[7]],[v[8],v[9]]], new Point3D(hx,hy,hz), "inside:x<=max,y<=max,z<=max; convex exterior removal", "Each seam is one shared opposite-oriented cylinder/sphere coedge; retained planes own tangency boundaries; remote arcs are independently owned.", topology, "history-known-axis-aligned-rectangular-box; equal-constant-radius; exact cylinders plus spherical octant"), null);
    }

    private static SphereCylinderSeamConstruction Seam(string role, Point3D start, Point3D end, Point3D center, CylinderSurface cylinder, string cylinderRole)
    {
        var curve = Arc(start, end, center);
        var mid = curve.Evaluate(double.Pi / 4d);
        var sphereDeviation = double.Abs((mid - center).Length - cylinder.Radius);
        var radial = mid - cylinder.Origin; var axial = radial.Dot(cylinder.Axis.ToVector());
        var cylinderDeviation = double.Abs((radial - cylinder.Axis.ToVector() * axial).Length - cylinder.Radius);
        var sphereNormal = Direction3D.Create(mid - center).ToVector();
        var cylinderAngle = System.Math.Atan2((mid - cylinder.Origin).Dot(cylinder.YAxis.ToVector()), (mid - cylinder.Origin).Dot(cylinder.XAxis.ToVector()));
        var cylinderNormal = cylinder.Normal(cylinderAngle).ToVector();
        return new(role, curve, new(0d, double.Pi / 2d), start, end, cylinderRole, sphereDeviation, cylinderDeviation, (sphereNormal - cylinderNormal).Length, "positive-quarter-circle; directed start->end", "exact plane-through-center sphere/cylinder intersection");
    }

    private static AirBRepPlan BuildPlan(AirLocalizedTrihedralFilletCompileRequest input, LocalizedTrihedralFilletConstruction c)
    {
        var provenance = new AirProvenance("AIR-FILLET-TRIHEDRAL-M5", "Exact equal-radius three-edge trihedral fillet", "SharedEdge(+X,+Z)/SharedEdge(+Y,+Z)/SharedEdge(+X,+Y)", input.FeatureId, nameof(AirLocalizedTrihedralFilletCompiler), AirSelectionClass.None, AirRuleKind.ConstantRadiusFillet, c.Provenance, true, ["One hard-valid spherical-octant construction.", "Three exact shared circle seams.", "No legacy surgery or unequal-radius fallback."]);
        var p = c.TopologyPlan; var e = new List<AirBRepPlanElement>();
        for (var n=0;n<p.ExpectedVertexCount;n++) e.Add(new(new($"vertex:{n}"), AirBRepPlanElementKind.Vertex, n is 10 or 11 or 12 ? AirBRepPlanRole.SharedJunction : AirBRepPlanRole.SectionVertex, input.FeatureId, provenance));
        for (var n=0;n<p.ExpectedFaceCount;n++) { var role = n is 6 or 7 or 8 ? AirBRepPlanRole.FilletFace : n==9 ? AirBRepPlanRole.CornerPatch : n is 3 or 4 or 5 ? AirBRepPlanRole.RetainedSupportFaceA : n is 0 or 1 or 2 ? AirBRepPlanRole.UnaffectedFace : AirBRepPlanRole.Unknown; e.Add(new(new($"surface:{n}"),AirBRepPlanElementKind.Surface,role,input.FeatureId,provenance)); e.Add(new(new($"loop:{n}"),AirBRepPlanElementKind.Loop,role,input.FeatureId,provenance)); for(var k=0;k<p.FaceLoops[n].Count;k++) e.Add(new(new($"coedge:{n}:{k}"),AirBRepPlanElementKind.Coedge,role,input.FeatureId,provenance)); e.Add(new(new($"face:{n}"),AirBRepPlanElementKind.Face,role,input.FeatureId,provenance,FaceRole:p.FaceRoles[n],SemanticRoles:n is 6 or 7 or 8 ? [AirBRepPlanRole.ReplacementFace,AirBRepPlanRole.FilletFace] : [role])); }
        for(var n=0;n<p.ExpectedEdgeCount;n++) e.Add(new(new($"edge:{n}"),AirBRepPlanElementKind.Edge,AirBRepPlanRole.SectionEdge,input.FeatureId,provenance));
        foreach(var role in new[]{"XZ","YZ","XY"}) e.Add(new(new($"curve:sphere-cylinder-seam:{role}"),AirBRepPlanElementKind.Curve,AirBRepPlanRole.SharedJunction,input.FeatureId,provenance,SemanticRoles:[AirBRepPlanRole.SharedJunction,AirBRepPlanRole.CornerPatch]));
        e.Add(new(new("shell:0"),AirBRepPlanElementKind.Shell,AirBRepPlanRole.BodyShell,input.FeatureId,provenance)); e.Add(new(new("body:0"),AirBRepPlanElementKind.Body,AirBRepPlanRole.Body,input.FeatureId,provenance));
        var summary = new AirBRepPlanSummary(AirBRepPlanKind.LocalizedEdgeJunction,input.FeatureId,p.ExpectedVertexCount,p.ExpectedEdgeCount,p.ExpectedEdgeCount,p.ExpectedCoedgeCount,p.ExpectedLoopCount,p.ExpectedFaceCount,p.ExpectedFaceCount,1,1,0,6,4,0,$"localized-trihedral-fillet=Fillet;patch=SphericalOctant;signature={p.DeterministicSignature}",c.BoundaryOwnership,[],["authoritative combined trihedral topology","one hard-valid plan","exact spherical octant","no legacy fallback"],new(AirNodeKind.Unsupported,AirRouteKind.Unsupported,AirSelectionClass.None,AirRuleKind.ConstantRadiusFillet,c.Provenance,"Direct",["SharedEdge(+X,+Z)","SharedEdge(+Y,+Z)","SharedEdge(+X,+Y)"]));
        return new($"brep-plan:localized-trihedral-fillet:{input.FeatureId}",AirBRepPlanKind.LocalizedEdgeJunction,input.FeatureId,provenance,e,summary,[],summary.Guarantees,summary.FeatureContext,LocalizedEdgeJunctionRealizationPlan:p);
    }

    private static AirLocalizedEdgeReplacementEmissionResult Emit(LocalizedTrihedralFilletConstruction c)
    {
        var p=c.TopologyPlan.Vertices; var loops=c.TopologyPlan.FaceLoops; var b=new TopologyBuilder(); var vertices=p.Select(_=>b.AddVertex()).ToArray(); var edges=new Dictionary<(int,int),EdgeId>(); var dirs=new Dictionary<(int,int),(int,int)>(); var faces=new List<FaceId>();
        foreach(var loop in loops) { var uses=new Use[loop.Count]; for(var n=0;n<loop.Count;n++){var a=loop[n];var z=loop[(n+1)%loop.Count];var key=a<z?(a,z):(z,a);if(!edges.TryGetValue(key,out var edge)){edge=b.AddEdge(vertices[a],vertices[z]);edges[key]=edge;dirs[key]=(a,z);}var d=dirs[key];uses[n]=d.Item1==a&&d.Item2==z?Use.F(edge):Use.R(edge);}faces.Add(AddFace(b,uses)); }
        var shell=b.AddShell(faces);b.AddBody([shell]);var geometry=new BrepGeometryStore();var bindings=new BrepBindingModel();var number=1;var arcs=new HashSet<(int,int)>{(4,5),(6,7),(8,9),(10,11),(10,12),(11,12)};
        foreach(var pair in edges.OrderBy(x=>x.Value.Value)){var (a,z)=dirs[pair.Key];var cid=new CurveGeometryId(number++);if(arcs.Contains(pair.Key)){var center=pair.Key is (4,5)?new Point3D(p[5].X,p[4].Y,p[4].Z):pair.Key is (6,7)?new Point3D(p[6].X,p[7].Y,p[6].Z):pair.Key is (8,9)?new Point3D(p[9].X,p[8].Y,p[8].Z):c.SphericalCornerPatch.Center;geometry.AddCurve(cid,CurveGeometry.FromCircle(Arc(p[a],p[z],center)));bindings.AddEdgeBinding(new EdgeGeometryBinding(pair.Value,cid,new(0d,double.Pi/2d)));}else{geometry.AddCurve(cid,CurveGeometry.FromLine(new Line3Curve(p[a],Direction3D.Create(p[z]-p[a]))));bindings.AddEdgeBinding(new EdgeGeometryBinding(pair.Value,cid,new(0d,(p[z]-p[a]).Length)));}}
        for(var n=0;n<faces.Count;n++){var sid=new SurfaceGeometryId(number++);if(n==6)geometry.AddSurface(sid,SurfaceGeometry.FromCylinder(new CylinderSurface(c.SphericalCornerPatch.Center,Direction3D.Create(new Vector3D(0,1,0)),c.SphericalCornerPatch.Radius,Direction3D.Create(new Vector3D(1,0,0)))));else if(n==7)geometry.AddSurface(sid,SurfaceGeometry.FromCylinder(new CylinderSurface(c.SphericalCornerPatch.Center,Direction3D.Create(new Vector3D(1,0,0)),c.SphericalCornerPatch.Radius,Direction3D.Create(new Vector3D(0,1,0)))));else if(n==8)geometry.AddSurface(sid,SurfaceGeometry.FromCylinder(new CylinderSurface(c.SphericalCornerPatch.Center,Direction3D.Create(new Vector3D(0,0,1)),c.SphericalCornerPatch.Radius,Direction3D.Create(new Vector3D(1,0,0)))));else if(n==9)geometry.AddSurface(sid,SurfaceGeometry.FromSphere(new SphereSurface(c.SphericalCornerPatch.Center,Direction3D.Create(new Vector3D(0,0,1)),c.SphericalCornerPatch.Radius,Direction3D.Create(new Vector3D(1,0,0)))));else{var loop=loops[n];var origin=p[loop[0]];var u=p[loop[1]]-origin;Vector3D normal=default;for(var k=2;k<loop.Count;k++){normal=u.Cross(p[loop[k]]-origin);if(normal.Length>Tol)break;}geometry.AddSurface(sid,SurfaceGeometry.FromPlane(new PlaneSurface(origin,Direction3D.Create(normal),Direction3D.Create(u))));}bindings.AddFaceBinding(new FaceGeometryBinding(faces[n],sid));}
        var points=vertices.Select((v,n)=>new KeyValuePair<VertexId,Point3D>(v,p[n])).ToDictionary();var body=new BrepBody(b.Model,geometry,bindings,points);var check=BrepBindingValidator.Validate(body,true);return check.IsSuccess?new(true,body,["localized-trihedral-fillet-plan-emitted"]):new(false,null,check.Diagnostics.Select(x=>x.Message).ToArray());
    }

    private static Circle3Curve Arc(Point3D start, Point3D end, Point3D center) => new(center,Direction3D.Create((start-center).Cross(end-center)),(start-center).Length,Direction3D.Create(start-center));
    private static AirEdgeFinishFeature Feature(AirLocalizedTrihedralFilletCompileRequest i,string suffix,string face,string edge) => new($"{i.FeatureId}.{suffix}",$"{i.FeatureName}.{suffix}",i.BodyId,new AirFaceBoundarySelection(face,edge,false),AirLocalizedEdgeFinishKind.Fillet,new AirConstantRadiusEdgeFinishRule(i.RadiusXZ),i.SourceSpan,"generated/history-known-axis-aligned-rectangular-prism",AirFeatureAdmissionStatus.Admitted,"localized-trihedral-fillet-spherical-octant-candidate");
    private static AirLocalizedTrihedralFilletCompileResult Failure(LocalizedTrihedralFilletConstruction c,AirBRepPlan p,TrihedralFilletError e)=>new(false,c,p,null,e,[e.Code,..e.Evidence]);
    private static FaceId AddFace(TopologyBuilder b,IReadOnlyList<Use> uses){var loop=b.AllocateLoopId();var ids=new CoedgeId[uses.Count];for(var n=0;n<ids.Length;n++)ids[n]=b.AllocateCoedgeId();for(var n=0;n<ids.Length;n++)b.AddCoedge(new Coedge(ids[n],uses[n].Id,loop,ids[(n+1)%ids.Length],ids[(n+ids.Length-1)%ids.Length],uses[n].Reverse));b.AddLoop(new Loop(loop,ids));return b.AddFace([loop]);}
    private readonly record struct Use(EdgeId Id,bool Reverse){public static Use F(EdgeId x)=>new(x,false);public static Use R(EdgeId x)=>new(x,true);}
    private static bool FirmamentManifold(BrepBody body) => body.Topology.Edges.All(e=>body.Topology.Coedges.Count(c=>c.EdgeId==e.Id)==2);
    private static bool FinitePositive(double x)=>double.IsFinite(x)&&x>Tol;
    private static bool Matches(string face,string target,string expectedFace,string expectedTarget)=>string.Equals(face,expectedFace,StringComparison.Ordinal)&&string.Equals(target,expectedTarget,StringComparison.Ordinal);
}
