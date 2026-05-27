using System.Linq;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Minimal M08 primitive solid constructors.
/// Current scope intentionally favors explicit, validator-accepted topology over advanced manifold semantics.
/// </summary>
public static class BrepPrimitives
{
    /// <summary>
    /// Bounded M3 triangular prism primitive with a centered isosceles profile.
    /// Profile frame is XY, extrusion is world +Z, and the local profile vertices are:
    /// (-baseWidth/2,-baseDepth/2), (+baseWidth/2,-baseDepth/2), (0,+baseDepth/2).
    /// Legacy body is centered on Z in [-height/2,+height/2].
    /// This primitive is intentionally not a right-triangle contract.
    /// </summary>
    public static KernelResult<BrepBody> CreateTriangularPrism(double baseWidth, double baseDepth, double height)
    {
        var diagnostics = ValidatePositiveFinite((baseWidth, nameof(baseWidth)), (baseDepth, nameof(baseDepth)), (height, nameof(height)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var profile = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(-baseWidth * 0.5d, -baseDepth * 0.5d),
            new ProfilePoint2D(baseWidth * 0.5d, -baseDepth * 0.5d),
            new ProfilePoint2D(0d, baseDepth * 0.5d),
        ]);
        if (!profile.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(profile.Diagnostics);
        }

        var extrude = BrepExtrude.Create(
            profile.Value,
            new ExtrudeFrame3D(new Point3D(0d, 0d, -height * 0.5d), Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            height);
        return extrude;
    }

    /// <summary>
    /// Bounded M3 hexagonal prism primitive.
    /// Profile frame is XY, extrusion is world +Z, regular hexagon centered at origin with across-flats distance.
    /// Legacy body is centered on Z in [-height/2,+height/2].
    /// </summary>
    public static KernelResult<BrepBody> CreateHexagonalPrism(double acrossFlats, double height)
    {
        var diagnostics = ValidatePositiveFinite((acrossFlats, nameof(acrossFlats)), (height, nameof(height)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var circumradius = acrossFlats / double.Sqrt(3d);
        var vertices = Enumerable.Range(0, 6)
            .Select(index =>
            {
                var angle = (double.Pi / 3d) * index;
                return new ProfilePoint2D(circumradius * double.Cos(angle), circumradius * double.Sin(angle));
            })
            .ToArray();
        var profile = PolylineProfile2D.Create(vertices);
        if (!profile.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(profile.Diagnostics);
        }

        var extrude = BrepExtrude.Create(
            profile.Value,
            new ExtrudeFrame3D(new Point3D(0d, 0d, -height * 0.5d), Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            height);
        return extrude;
    }

    /// <summary>
    /// Bounded M3 straight slot primitive as an obround (capsule) prism.
    /// Profile frame is XY, extrusion is world +Z, with slot major axis on X and rounded ends approximated by a polyline.
    /// Legacy body is centered on Z in [-height/2,+height/2].
    /// </summary>
    public static KernelResult<BrepBody> CreateStraightSlot(double length, double width, double height)
    {
        var diagnostics = ValidatePositiveFinite((length, nameof(length)), (width, nameof(width)), (height, nameof(height)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        if (length < width)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"{nameof(length)} must be greater than or equal to {nameof(width)} for straight slot primitive.")
            ]);
        }

        const int semicircleSegments = 8;
        var halfLength = length * 0.5d;
        var radius = width * 0.5d;
        var centerOffset = halfLength - radius;
        var profileVertices = new List<ProfilePoint2D>(2 * (semicircleSegments + 1));

        for (var i = 0; i <= semicircleSegments; i++)
        {
            var t = double.Pi * (i / (double)semicircleSegments) - (double.Pi * 0.5d);
            profileVertices.Add(new ProfilePoint2D(centerOffset + (radius * double.Cos(t)), radius * double.Sin(t)));
        }

        for (var i = 0; i <= semicircleSegments; i++)
        {
            var t = double.Pi * (i / (double)semicircleSegments) + (double.Pi * 0.5d);
            profileVertices.Add(new ProfilePoint2D(-centerOffset + (radius * double.Cos(t)), radius * double.Sin(t)));
        }

        var profile = PolylineProfile2D.Create(profileVertices);
        if (!profile.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(profile.Diagnostics);
        }

        var extrude = BrepExtrude.Create(
            profile.Value,
            new ExtrudeFrame3D(new Point3D(0d, 0d, -height * 0.5d), Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            height);
        return extrude;
    }

    public static KernelResult<BrepBody> CreateBox(double width, double height, double depth)
    {
        var diagnostics = ValidatePositiveFinite((width, nameof(width)), (height, nameof(height)), (depth, nameof(depth)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }
        var profile = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(-width * 0.5d, -height * 0.5d),
            new ProfilePoint2D(width * 0.5d, -height * 0.5d),
            new ProfilePoint2D(width * 0.5d, height * 0.5d),
            new ProfilePoint2D(-width * 0.5d, height * 0.5d),
        ]);
        if (!profile.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(profile.Diagnostics);
        }

        var frame = new ExtrudeFrame3D(
            new Point3D(0d, 0d, -depth * 0.5d),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        return BrepExtrude.Create(profile.Value, frame, depth);
    }

    public static KernelResult<BrepBody> CreateCylinder(double radius, double height)
    {
        var diagnostics = ValidatePositiveFinite((radius, nameof(radius)), (height, nameof(height)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var profile = new[]
        {
            new ProfilePoint2D(radius, -height * 0.5d),
            new ProfilePoint2D(radius, height * 0.5d),
        };

        var frame = new ExtrudeFrame3D(
            new Point3D(0d, 0d, 0d),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var axis = new RevolveAxis3D(new Point3D(0d, 0d, 0d), new Vector3D(0d, 0d, 1d));
        return BrepRevolve.Create(profile, frame, axis);
    }


    public static KernelResult<BrepBody> CreateTorus(double majorRadius, double minorRadius)
    {
        var diagnostics = ValidatePositiveFinite((majorRadius, nameof(majorRadius)), (minorRadius, nameof(minorRadius)));
        if (majorRadius <= minorRadius)
        {
            diagnostics.Add(new KernelDiagnostic(
                KernelDiagnosticCode.InvalidArgument,
                KernelDiagnosticSeverity.Error,
                $"{nameof(majorRadius)} must be greater than {nameof(minorRadius)} for a non-self-intersecting torus."));
        }

        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var builder = new TopologyBuilder();

        // Narrow M10g1 torus convention: one periodic toroidal face with one loop,
        // represented by two circular self-loop seam edges that are each used twice.
        var seamVertex = builder.AddVertex();
        var majorSeamEdge = builder.AddEdge(seamVertex, seamVertex);
        var minorSeamEdge = builder.AddEdge(seamVertex, seamVertex);

        var torusFace = AddFaceWithLoop(
            builder,
            [
                EdgeUse.Forward(majorSeamEdge),
                EdgeUse.Reversed(minorSeamEdge),
                EdgeUse.Reversed(majorSeamEdge),
                EdgeUse.Forward(minorSeamEdge),
            ]);

        var shell = builder.AddShell([torusFace]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var yAxis = Direction3D.Create(new Vector3D(0d, 1d, 0d));
        var zAxis = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var negativeXAxis = Direction3D.Create(new Vector3D(-1d, 0d, 0d));
        var positiveXAxis = Direction3D.Create(new Vector3D(1d, 0d, 0d));

        var sharedVertexPoint = new Point3D(-(majorRadius - minorRadius), 0d, 0d);

        geometry.AddCurve(
            new CurveGeometryId(1),
            CurveGeometry.FromCircle(new Circle3Curve(Point3D.Origin, yAxis, majorRadius - minorRadius, negativeXAxis)));
        geometry.AddCurve(
            new CurveGeometryId(2),
            CurveGeometry.FromCircle(new Circle3Curve(new Point3D(-majorRadius, 0d, 0d), zAxis, minorRadius, positiveXAxis)));
        geometry.AddSurface(
            new SurfaceGeometryId(1),
            SurfaceGeometry.FromTorus(new TorusSurface(Point3D.Origin, yAxis, majorRadius, minorRadius, negativeXAxis)));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(majorSeamEdge, new CurveGeometryId(1), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(minorSeamEdge, new CurveGeometryId(2), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddFaceBinding(new FaceGeometryBinding(torusFace, new SurfaceGeometryId(1)));

        return ValidateAndReturn(new BrepBody(
            builder.Model,
            geometry,
            bindings,
            new Dictionary<VertexId, Point3D>
            {
                [seamVertex] = sharedVertexPoint,
            }));
    }

    public static KernelResult<BrepBody> CreateSphere(double radius)
    {
        var diagnostics = ValidatePositiveFinite((radius, nameof(radius)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var builder = new TopologyBuilder();

        // M08 simplification: represent the sphere as one closed periodic face with no boundary loops.
        var sphereFace = builder.AddFace([]);
        var shell = builder.AddShell([sphereFace]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        geometry.AddSurface(
            new SurfaceGeometryId(1),
            SurfaceGeometry.FromSphere(
                new SphereSurface(
                    Point3D.Origin,
                    Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                    radius,
                    Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        var bindings = new BrepBindingModel();
        bindings.AddFaceBinding(new FaceGeometryBinding(sphereFace, new SurfaceGeometryId(1)));

        return ValidateAndReturn(new BrepBody(builder.Model, geometry, bindings));
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
    {
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new CoedgeId[edgeUses.Count];

        for (var i = 0; i < edgeUses.Count; i++)
        {
            coedgeIds[i] = builder.AllocateCoedgeId();
        }

        for (var i = 0; i < edgeUses.Count; i++)
        {
            var next = coedgeIds[(i + 1) % edgeUses.Count];
            var prev = coedgeIds[(i + edgeUses.Count - 1) % edgeUses.Count];
            builder.AddCoedge(new Coedge(coedgeIds[i], edgeUses[i].EdgeId, loopId, next, prev, edgeUses[i].IsReversed));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return builder.AddFace([loopId]);
    }

    private static KernelResult<BrepBody> ValidateAndReturn(BrepBody body)
    {
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static List<KernelDiagnostic> ValidatePositiveFinite(params (double Value, string Name)[] values)
    {
        var diagnostics = new List<KernelDiagnostic>();

        foreach (var (value, name) in values)
        {
            if (!double.IsFinite(value) || value <= 0d)
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"{name} must be finite and greater than zero."));
            }
        }

        return diagnostics;
    }

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, IsReversed: false);

        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, IsReversed: true);
    }
}
