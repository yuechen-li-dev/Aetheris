using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;


namespace Aetheris.Kernel.Core.Brep.Features;

public sealed record HollowRadialCutPlacement(string StableId, double Radius, double Z, double AngleRadians);
public sealed record HollowRadialCutPatternResult(BrepBody Body, HollowRadialCutTopologyMap TopologyMap,
    IReadOnlyDictionary<string, HollowRadialCutCertificate> Certificates, double HostSeamAngleRadians);

/// <summary>Compose qualified local radial cuts on one analytic Hollow wall.
/// The stock faces are constructed once; each instance contributes two trim loops
/// and one cylindrical connector face. No Boolean or faceted topology is involved.</summary>
public static class BrepHollowRadialCutPattern
{
    public static KernelResult<HollowRadialCutPatternResult> Build(ThinWalledBodyRealization hollow,
        IReadOnlyList<HollowRadialCutPlacement> placements, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(hollow);
        ArgumentNullException.ThrowIfNull(placements);
        var tol = tolerance ?? ToleranceContext.Default;
        KernelResult<HollowRadialCutPatternResult> Reject(string code, string message) =>
            KernelResult<HollowRadialCutPatternResult>.Failure([
                new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, code)]);
        if (hollow.Feature.PrimitiveKind != "Cylinder" || hollow.Feature.Openings.Count != 1 || hollow.Feature.Openings[0] != "Top")
            return Reject("Brep.HollowRadialCutPattern.UnsupportedHost", "A top-open Cylinder<Hollow> is required.");
        if (placements.Count == 0 || placements.Count > 1000 || placements.Select(p => p.StableId).Distinct(StringComparer.Ordinal).Count() != placements.Count)
            return Reject("Brep.HollowRadialCutPattern.InvalidInstances", "Provide one to 1000 uniquely identified local holes.");
        var radius = hollow.Feature.PrimitiveParameters["Radius"];
        var height = hollow.Feature.PrimitiveParameters["Height"];
        var thickness = hollow.Feature.WallThickness;
        var innerRadius = radius - thickness;
        var blendRadius = hollow.Feature.PrimitiveParameters.GetValueOrDefault("BottomBlendRadius");
        var blended = blendRadius > 0;
        var wallStart = blended ? blendRadius : 0d;
        if (placements.Any(p => !double.IsFinite(p.Radius) || !double.IsFinite(p.Z) || !double.IsFinite(p.AngleRadians) ||
            p.Radius <= 0 || p.Radius >= innerRadius - tol.Linear || p.Z - p.Radius <= wallStart + tol.Linear ||
            p.Z + p.Radius >= height - tol.Linear))
            return Reject("Brep.HollowRadialCutPattern.InvalidPlacement", "Every opening must clear the inner bottom, top rim, and inner-cylinder tangent case.");
        // The current single-loop pcurve representation needs a host seam outside
        // every opening. Choose the midpoint of the largest free angular gap.
        var centers = placements.Select(p => Normalize(p.AngleRadians)).Distinct().Order().ToArray();
        var bestGap = -1d; var seamAngle = 0d;
        for (var i = 0; i < centers.Length; i++)
        {
            var next = i + 1 < centers.Length ? centers[i + 1] : centers[0] + 2 * System.Math.PI;
            var gap = next - centers[i];
            if (gap > bestGap) { bestGap = gap; seamAngle = Normalize(centers[i] + gap / 2); }
        }
        if (placements.Any(p => AngularDistance(seamAngle, Normalize(p.AngleRadians)) <= System.Math.Asin(p.Radius / innerRadius) + tol.Angular))
            return Reject("Brep.HollowRadialCutPattern.SeamClearance", "No host seam clears all local opening loops; periodic seam splitting is required.");
        for (var i = 0; i < placements.Count; i++)
            for (var j = i + 1; j < placements.Count; j++)
            {
                var arc = innerRadius * AngularDistance(Normalize(placements[i].AngleRadians), Normalize(placements[j].AngleRadians));
                var dz = placements[i].Z - placements[j].Z;
                if (System.Math.Sqrt(arc * arc + dz * dz) <= placements[i].Radius + placements[j].Radius + tol.Linear)
                    return Reject("Brep.HollowRadialCutPattern.Overlap", $"Openings {placements[i].StableId} and {placements[j].StableId} overlap.");
            }

        var z = Direction3D.Create(new Vector3D(0, 0, 1));
        var hostReference = Direction3D.Create(new Vector3D(System.Math.Cos(seamAngle), System.Math.Sin(seamAngle), 0));
        var origin = new Point3D(0, 0, 0);
        var outer = new CylinderSurface(origin, z, radius, hostReference);
        var inner = new CylinderSurface(new Point3D(0, 0, thickness), z, innerRadius, hostReference);
        var full = new ParameterInterval(0, 2 * System.Math.PI);
        var first = new ParameterInterval(0, System.Math.PI);
        var second = new ParameterInterval(System.Math.PI, 2 * System.Math.PI);
        var builder = new TopologyBuilder();
        var points = new Dictionary<VertexId, Point3D>();
        VertexId V(Point3D point) { var id = builder.AddVertex(); points.Add(id, point); return id; }
        var ob = V(new Point3D(0, 0, wallStart) + hostReference.ToVector() * radius);
        var ot = V(new Point3D(0, 0, height) + hostReference.ToVector() * radius);
        var ib = V(new Point3D(0, 0, wallStart > 0 ? wallStart : thickness) + hostReference.ToVector() * innerRadius);
        var it = V(new Point3D(0, 0, height) + hostReference.ToVector() * innerRadius);
        var outerSeam = builder.AddEdge(ob, ot); var innerSeam = builder.AddEdge(ib, it);
        var outerBottom = builder.AddEdge(ob, ob); var outerTop = builder.AddEdge(ot, ot);
        var innerBottom = builder.AddEdge(ib, ib); var innerTop = builder.AddEdge(it, it);
        var floorRadius = blended ? radius - blendRadius : radius;
        var outerFloorVertex = blended ? V(origin + hostReference.ToVector() * floorRadius) : ob;
        var innerFloorVertex = blended ? V(new Point3D(0, 0, thickness) + hostReference.ToVector() * floorRadius) : ib;
        var outerFloor = blended ? builder.AddEdge(outerFloorVertex, outerFloorVertex) : outerBottom;
        var innerFloor = blended ? builder.AddEdge(innerFloorVertex, innerFloorVertex) : innerBottom;
        var outerEnvelope = AddLoop(builder, [new(outerSeam), new(outerTop), new(outerSeam, true), new(outerBottom, true)]);
        var innerEnvelope = AddLoop(builder, [new(innerSeam), new(innerTop), new(innerSeam, true), new(innerBottom, true)]);
        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(points[ob], z)));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromLine(new Line3Curve(points[ib], z)));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, wallStart), z, radius, hostReference)));
        geometry.AddCurve(new CurveGeometryId(4), CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, height), z, radius, hostReference)));
        geometry.AddCurve(new CurveGeometryId(5), CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, blended ? wallStart : thickness), z, innerRadius, hostReference)));
        geometry.AddCurve(new CurveGeometryId(6), CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, height), z, innerRadius, hostReference)));
        if (blended)
        {
            geometry.AddCurve(new CurveGeometryId(7), CurveGeometry.FromCircle(new Circle3Curve(origin, z, floorRadius, hostReference)));
            geometry.AddCurve(new CurveGeometryId(8), CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, thickness), z, floorRadius, hostReference)));
        }
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCylinder(outer));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromCylinder(inner));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(-z.ToVector()), hostReference)));
        geometry.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, thickness), z, hostReference)));
        geometry.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, height), z, hostReference)));
        if (blended)
        {
            var blendCenter = new Point3D(0, 0, blendRadius);
            geometry.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromTorus(new TorusSurface(blendCenter, z, floorRadius, blendRadius, hostReference)));
            geometry.AddSurface(new SurfaceGeometryId(7), SurfaceGeometry.FromTorus(new TorusSurface(blendCenter, z, floorRadius, blendRadius - thickness, hostReference)));
        }
        var bindings = new BrepBindingModel();
        void Edge(EdgeId edge, int curve, ParameterInterval interval) => bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, new CurveGeometryId(curve), interval));
        Edge(outerSeam, 1, new(0, height - wallStart)); Edge(innerSeam, 2, new(0, height - (blended ? wallStart : thickness)));
        Edge(outerBottom, 3, full); Edge(outerTop, 4, full); Edge(innerBottom, 5, full); Edge(innerTop, 6, full);
        if (blended) { Edge(outerFloor, 7, full); Edge(innerFloor, 8, full); }
        void Bind(CoedgeId id, FaceId face, int surface, PcurveGeometry uv) =>
            bindings.AddPcurveBinding(new CoedgePcurveBinding(id, face, new SurfaceGeometryId(surface), uv));
        void Role(FaceId face, LoopId loop, FaceBoundaryRole role) =>
            bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(face, loop, role));
        var holes = new List<Hole>();
        var faceNames = new Dictionary<string, FaceId>(StringComparer.Ordinal);
        var loopNames = new Dictionary<string, LoopId>(StringComparer.Ordinal);
        var edgeNames = new Dictionary<string, IReadOnlyList<EdgeId>>(StringComparer.Ordinal);
        var certificates = new Dictionary<string, HollowRadialCutCertificate>(StringComparer.Ordinal);
        var nextCurve = blended ? 9 : 7; var nextSurface = blended ? 8 : 6;
        foreach (var placement in placements)
        {
            var canonicalAngle = Normalize(placement.AngleRadians);
            if (canonicalAngle <= tol.Angular || 2 * System.Math.PI - canonicalAngle <= tol.Angular)
                canonicalAngle = 0;
            var radial = Direction3D.Create(new Vector3D(System.Math.Cos(canonicalAngle), System.Math.Sin(canonicalAngle), 0));
            var toolReference = Direction3D.Create(z.ToVector().Cross(radial.ToVector()));
            var center = new Point3D(0, 0, placement.Z);
            var tool = new CylinderSurface(center, radial, placement.Radius, toolReference);
            var outerAuthority = CylinderCylinderIntersectionCurve.Create(outer, tool, center, CylinderIntersectionBranch.PositiveCutterAxis, full, tolerance: tol);
            if (!outerAuthority.IsSuccess) return InstanceFailure(placement, outerAuthority.Diagnostics);
            var innerAuthority = CylinderCylinderIntersectionCurve.Create(inner, tool, center, CylinderIntersectionBranch.PositiveCutterAxis, full, tolerance: tol);
            if (!innerAuthority.IsSuccess) return InstanceFailure(placement, innerAuthority.Diagnostics);
            var outerCurve = CylinderIntersectionCurveRealizer.Realize(outerAuthority.Value, tol);
            if (!outerCurve.IsSuccess) return InstanceFailure(placement, outerCurve.Diagnostics);
            var innerCurve = CylinderIntersectionCurveRealizer.Realize(innerAuthority.Value, tol);
            if (!innerCurve.IsSuccess) return InstanceFailure(placement, innerCurve.Diagnostics);
            var outerUv = CylinderIntersectionPcurveRealizer.Realize(outerCurve.Value, tol);
            if (!outerUv.IsSuccess) return InstanceFailure(placement, outerUv.Diagnostics);
            var innerUv = CylinderIntersectionPcurveRealizer.Realize(innerCurve.Value, tol);
            if (!innerUv.IsSuccess) return InstanceFailure(placement, innerUv.Diagnostics);
            var o0 = V(outerAuthority.Value.Evaluate(0)); var oPi = V(outerAuthority.Value.Evaluate(System.Math.PI));
            var i0 = V(innerAuthority.Value.Evaluate(0)); var iPi = V(innerAuthority.Value.Evaluate(System.Math.PI));
            var outerFirst = builder.AddEdge(o0, oPi); var outerSecond = builder.AddEdge(oPi, o0);
            var innerFirst = builder.AddEdge(i0, iPi); var innerSecond = builder.AddEdge(iPi, i0);
            var toolSeam = builder.AddEdge(o0, i0);
            var outerOpening = AddLoop(builder, [new(outerSecond, true), new(outerFirst, true)]);
            var innerOpening = AddLoop(builder, [new(innerFirst), new(innerSecond)]);
            var wall = AddLoop(builder, [new(outerFirst), new(outerSecond), new(toolSeam),
                new(innerSecond, true), new(innerFirst, true), new(toolSeam, true)]);
            var wallFace = builder.AddFace([wall.Id]);
            var outerCurveId = nextCurve++; var innerCurveId = nextCurve++; var toolSeamCurveId = nextCurve++;
            geometry.AddCurve(new CurveGeometryId(outerCurveId), CurveGeometry.FromCertifiedIntersection(outerCurve.Value));
            geometry.AddCurve(new CurveGeometryId(innerCurveId), CurveGeometry.FromCertifiedIntersection(innerCurve.Value));
            geometry.AddCurve(new CurveGeometryId(toolSeamCurveId), CurveGeometry.FromLine(new Line3Curve(points[o0], Direction3D.Create(points[i0] - points[o0]))));
            var seamLength = (points[o0] - points[i0]).Length;
            Edge(outerFirst, outerCurveId, first); Edge(outerSecond, outerCurveId, second);
            Edge(innerFirst, innerCurveId, first); Edge(innerSecond, innerCurveId, second);
            Edge(toolSeam, toolSeamCurveId, new(0, seamLength));
            var surface = nextSurface++;
            geometry.AddSurface(new SurfaceGeometryId(surface), SurfaceGeometry.FromCylinder(tool));
            bindings.AddFaceBinding(new FaceGeometryBinding(wallFace, new SurfaceGeometryId(surface), IsAlignedWithSurface: false));
            Role(wallFace, wall.Id, FaceBoundaryRole.Outer);
            Bind(wall.Coedges[0], wallFace, surface, PcurveGeometry.Polynomial(first, outerUv.Value.Tool));
            Bind(wall.Coedges[1], wallFace, surface, PcurveGeometry.Polynomial(second, outerUv.Value.Tool));
            Bind(wall.Coedges[2], wallFace, surface, PcurveGeometry.Line(new(0, seamLength),
                new(2 * System.Math.PI, (points[o0] - center).Dot(radial.ToVector())),
                new(2 * System.Math.PI, (points[i0] - center).Dot(radial.ToVector()))));
            Bind(wall.Coedges[3], wallFace, surface, PcurveGeometry.Polynomial(second, innerUv.Value.Tool));
            Bind(wall.Coedges[4], wallFace, surface, PcurveGeometry.Polynomial(first, innerUv.Value.Tool));
            Bind(wall.Coedges[5], wallFace, surface, PcurveGeometry.Line(new(0, seamLength),
                new(0, (points[o0] - center).Dot(radial.ToVector())),
                new(0, (points[i0] - center).Dot(radial.ToVector()))));
            var identity = placement.StableId;
            faceNames[$"{identity}.Wall"] = wallFace;
            loopNames[$"{identity}.OuterOpening"] = outerOpening.Id;
            loopNames[$"{identity}.InnerOpening"] = innerOpening.Id;
            edgeNames[$"{identity}.OuterOpening"] = [outerFirst, outerSecond];
            edgeNames[$"{identity}.InnerOpening"] = [innerFirst, innerSecond];
            certificates[identity] = new(outerCurve.Value.CertifiedDeviationBoundMm, innerCurve.Value.CertifiedDeviationBoundMm,
                outerUv.Value.HostEdgeMismatchBoundMm, outerUv.Value.ToolEdgeMismatchBoundMm,
                innerUv.Value.HostEdgeMismatchBoundMm, innerUv.Value.ToolEdgeMismatchBoundMm,
                outerCurve.Value.SegmentCount, innerCurve.Value.SegmentCount);
            holes.Add(new(outerOpening, innerOpening, wallFace, outerUv.Value, innerUv.Value));
        }
        var outerFace = builder.AddFace([outerEnvelope.Id, .. holes.Select(h => h.Outer.Id)]);
        var innerFace = builder.AddFace([innerEnvelope.Id, .. holes.Select(h => h.Inner.Id)]);
        var bottomOutside = AddLoop(builder, [new(outerFloor)]); var outerBottomFace = builder.AddFace([bottomOutside.Id]);
        var bottomInside = AddLoop(builder, [new(innerFloor)]); var innerBottomFace = builder.AddFace([bottomInside.Id]);
        var outerBlendFloor = blended ? AddLoop(builder, [new(outerFloor, true)]) : default;
        var outerBlendWall = blended ? AddLoop(builder, [new(outerBottom, true)]) : default;
        var innerBlendFloor = blended ? AddLoop(builder, [new(innerFloor, true)]) : default;
        var innerBlendWall = blended ? AddLoop(builder, [new(innerBottom, true)]) : default;
        var outerBlendFace = blended ? builder.AddFace([outerBlendFloor.Id, outerBlendWall.Id]) : default;
        var innerBlendFace = blended ? builder.AddFace([innerBlendFloor.Id, innerBlendWall.Id]) : default;
        var rimOuter = AddLoop(builder, [new(outerTop, true)]); var rimInner = AddLoop(builder, [new(innerTop, true)]);
        var rimFace = builder.AddFace([rimOuter.Id, rimInner.Id]);
        var shell = builder.AddShell(blended
            ? [outerFace, innerFace, outerBottomFace, innerBottomFace, rimFace, outerBlendFace, innerBlendFace, .. holes.Select(h => h.WallFace)]
            : [outerFace, innerFace, outerBottomFace, innerBottomFace, rimFace, .. holes.Select(h => h.WallFace)]);
        builder.AddBody([shell]);
        bindings.AddFaceBinding(new FaceGeometryBinding(outerFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(innerFace, new SurfaceGeometryId(2), IsAlignedWithSurface: false));
        bindings.AddFaceBinding(new FaceGeometryBinding(outerBottomFace, new SurfaceGeometryId(3)));
        bindings.AddFaceBinding(new FaceGeometryBinding(innerBottomFace, new SurfaceGeometryId(4)));
        bindings.AddFaceBinding(new FaceGeometryBinding(rimFace, new SurfaceGeometryId(5)));
        if (blended)
        {
            bindings.AddFaceBinding(new FaceGeometryBinding(outerBlendFace, new SurfaceGeometryId(6)));
            bindings.AddFaceBinding(new FaceGeometryBinding(innerBlendFace, new SurfaceGeometryId(7), IsAlignedWithSurface: false));
        }
        Role(outerFace, outerEnvelope.Id, FaceBoundaryRole.Outer);
        Role(innerFace, innerEnvelope.Id, FaceBoundaryRole.Outer);
        foreach (var h in holes)
        {
            Role(outerFace, h.Outer.Id, FaceBoundaryRole.Inner);
            Role(innerFace, h.Inner.Id, FaceBoundaryRole.Inner);
            Bind(h.Outer.Coedges[0], outerFace, 1, PcurveGeometry.Polynomial(second, h.OuterUv.Host));
            Bind(h.Outer.Coedges[1], outerFace, 1, PcurveGeometry.Polynomial(first, h.OuterUv.Host));
            Bind(h.Inner.Coedges[0], innerFace, 2, PcurveGeometry.Polynomial(first, h.InnerUv.Host));
            Bind(h.Inner.Coedges[1], innerFace, 2, PcurveGeometry.Polynomial(second, h.InnerUv.Host));
        }
        Role(outerBottomFace, bottomOutside.Id, FaceBoundaryRole.Outer);
        Role(innerBottomFace, bottomInside.Id, FaceBoundaryRole.Outer);
        Role(rimFace, rimOuter.Id, FaceBoundaryRole.Outer); Role(rimFace, rimInner.Id, FaceBoundaryRole.Inner);
        if (blended)
        {
            Role(outerBlendFace, outerBlendFloor.Id, FaceBoundaryRole.Outer);
            Role(outerBlendFace, outerBlendWall.Id, FaceBoundaryRole.Inner);
            Role(innerBlendFace, innerBlendFloor.Id, FaceBoundaryRole.Outer);
            Role(innerBlendFace, innerBlendWall.Id, FaceBoundaryRole.Inner);
        }
        void HostEnvelope((LoopId Id, CoedgeId[] Coedges) loop, FaceId face, int surface, double startZ, double originZ)
        {
            var length = height - startZ; var uvStart = startZ - originZ; var uvEnd = height - originZ;
            Bind(loop.Coedges[0], face, surface, PcurveGeometry.Line(new(0, length), new(0, uvStart), new(0, uvEnd)));
            Bind(loop.Coedges[1], face, surface, PcurveGeometry.Line(full, new(0, uvEnd), new(2 * System.Math.PI, uvEnd)));
            Bind(loop.Coedges[2], face, surface, PcurveGeometry.Line(new(0, length), new(2 * System.Math.PI, uvStart), new(2 * System.Math.PI, uvEnd)));
            Bind(loop.Coedges[3], face, surface, PcurveGeometry.Line(full, new(0, uvStart), new(2 * System.Math.PI, uvStart)));
        }
        HostEnvelope(outerEnvelope, outerFace, 1, wallStart, 0);
        HostEnvelope(innerEnvelope, innerFace, 2, blended ? wallStart : thickness, thickness);
        Bind(bottomOutside.Coedges[0], outerBottomFace, 3, PcurveGeometry.Circle(full, new(0, 0), floorRadius, -floorRadius));
        Bind(bottomInside.Coedges[0], innerBottomFace, 4, PcurveGeometry.Circle(full, new(0, 0), blended ? floorRadius : innerRadius, blended ? floorRadius : innerRadius));
        Bind(rimOuter.Coedges[0], rimFace, 5, PcurveGeometry.Circle(full, new(0, 0), radius, radius));
        Bind(rimInner.Coedges[0], rimFace, 5, PcurveGeometry.Circle(full, new(0, 0), innerRadius, innerRadius));
        if (blended)
        {
            void TorusCircle((LoopId Id, CoedgeId[] Coedges) loop, FaceId face, int surface, double minorAngle) =>
                Bind(loop.Coedges[0], face, surface, PcurveGeometry.Line(full,
                    new(0, minorAngle), new(2 * System.Math.PI, minorAngle)));
            TorusCircle(outerBlendFloor, outerBlendFace, 6, -System.Math.PI / 2);
            TorusCircle(outerBlendWall, outerBlendFace, 6, 0);
            TorusCircle(innerBlendFloor, innerBlendFace, 7, -System.Math.PI / 2);
            TorusCircle(innerBlendWall, innerBlendFace, 7, 0);
        }
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var bindingValidation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!bindingValidation.IsSuccess) return KernelResult<HollowRadialCutPatternResult>.Failure(bindingValidation.Diagnostics);
        var pcurveValidation = BrepPcurveValidator.Validate(body, tol.Linear, requireEveryCoedge: true, samples: 129);
        if (!pcurveValidation.IsValid) return Reject("Brep.HollowRadialCutPattern.PcurveValidation", string.Join("; ", pcurveValidation.Diagnostics));
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return Reject("Brep.HollowRadialCutPattern.Preflight", string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => $"{d.Code}: {d.Message}")));
        return KernelResult<HollowRadialCutPatternResult>.Success(new(body,
            new HollowRadialCutTopologyMap(faceNames, loopNames, edgeNames), certificates, seamAngle));
    }

    private static double Normalize(double value) { value %= 2 * System.Math.PI; return value < 0 ? value + 2 * System.Math.PI : value; }
    private static double AngularDistance(double a, double b) { var d = System.Math.Abs(a - b); return System.Math.Min(d, 2 * System.Math.PI - d); }
    private static KernelResult<HollowRadialCutPatternResult> InstanceFailure(HollowRadialCutPlacement placement,
        IReadOnlyList<KernelDiagnostic> diagnostics) => KernelResult<HollowRadialCutPatternResult>.Failure(
            diagnostics.Select(d => d with { Message = $"{placement.StableId}: {d.Message}" }).ToArray());
    private static (LoopId Id, CoedgeId[] Coedges) AddLoop(TopologyBuilder builder, IReadOnlyList<Use> uses)
    {
        var loop = builder.AllocateLoopId();
        var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < uses.Count; i++)
            builder.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop,
                coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].Reversed));
        builder.AddLoop(new Loop(loop, coedges));
        return (loop, coedges);
    }
    private sealed record Hole((LoopId Id, CoedgeId[] Coedges) Outer, (LoopId Id, CoedgeId[] Coedges) Inner,
        FaceId WallFace, QualifiedCylinderIntersectionPcurves OuterUv, QualifiedCylinderIntersectionPcurves InnerUv);
    private readonly record struct Use(EdgeId Edge, bool Reversed = false);
}
