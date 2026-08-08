using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.StandardLibrary;

/// <summary>
/// Engineering parameters for a threadless material representation of a hex-head bolt.
/// Length values are millimetres and angles are degrees. Thread fields are semantic only.
/// </summary>
public sealed record HexBoltSpec(
    double NominalDiameter,
    double Length,
    double HeadAcrossFlats,
    double HeadHeight,
    double TopFlatDiameter,
    double TopChamferAngle,
    double TipChamferLength,
    double TipDiameter,
    double ThreadLength,
    string ThreadDesignation,
    string PropertyClass,
    double UnderHeadRadius = 0d);

public static class McMasterHexBoltSpecs
{
    public const string ReferencePartNumber = "91180A151";

    /// <summary>Audited from McMaster-Carr 91180A151_NO THREADS.STEP.</summary>
    public static HexBoltSpec Reference91180A151 { get; } = new(
        NominalDiameter: 8d,
        Length: 35d,
        HeadAcrossFlats: 13d,
        HeadHeight: 5.3d,
        TopFlatDiameter: 12.35d,
        TopChamferAngle: 25d,
        TipChamferLength: 0.9375d,
        TipDiameter: 6.125d,
        ThreadLength: 22d,
        ThreadDesignation: "M8 x 1.25",
        PropertyClass: "8.8",
        UnderHeadRadius: 0.2d);
}

public enum HexBoltAdmissionCode
{
    NonFiniteOrNonPositiveDimension,
    ThreadLengthOutsideShank,
    TopFlatOutsideHex,
    TopChamferConsumesHead,
    TipChamferInvalid,
    UnderHeadRadiusInvalid,
    EmptySemanticMetadata
}

public sealed record HexBoltAdmissionDiagnostic(HexBoltAdmissionCode Code, string Field, string Message);

public sealed record HexBoltDerivedDimensions(
    double HeadApothem,
    double HeadCircumradius,
    double TopFlatRadius,
    double TopConeSemiAngleDegrees,
    double TopConeApexX,
    double TopConeSideMidpointX,
    double TopConeCornerX,
    double TipChamferStartX);

public enum HexBoltSemanticKind { Part, Region, Face }

public sealed record HexBoltSemanticDescendant(
    string StableId,
    HexBoltSemanticKind Kind,
    FaceId? Face = null,
    string? ParentStableId = null,
    string? Metadata = null);

public sealed record HexBoltSemanticModel(
    string BodyStableId,
    IReadOnlyList<HexBoltSemanticDescendant> Descendants,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record HexBoltDefinition(
    HexBoltSpec Spec,
    HexBoltDerivedDimensions Dimensions,
    BrepBody Body,
    HexBoltSemanticModel Semantics,
    string DeterministicSignature);

/// <summary>
/// Exact bounded construction for the first StandardLibrary hex-bolt family. The top
/// treatment is one coaxial cone trimmed by six analytic hyperbola edges.
/// </summary>
public static class HexBoltBuilder
{
    private const double Tolerance = 1e-9d;
    private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1d, 0d, 0d));
    private static readonly Direction3D MinusX = Direction3D.Create(new Vector3D(-1d, 0d, 0d));
    private static readonly Direction3D PlusY = Direction3D.Create(new Vector3D(0d, 1d, 0d));

    public static IReadOnlyList<HexBoltAdmissionDiagnostic> Validate(HexBoltSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var diagnostics = new List<HexBoltAdmissionDiagnostic>();
        var positive = new (string Name, double Value)[]
        {
            (nameof(spec.NominalDiameter), spec.NominalDiameter),
            (nameof(spec.Length), spec.Length),
            (nameof(spec.HeadAcrossFlats), spec.HeadAcrossFlats),
            (nameof(spec.HeadHeight), spec.HeadHeight),
            (nameof(spec.TopFlatDiameter), spec.TopFlatDiameter),
            (nameof(spec.TipDiameter), spec.TipDiameter)
        };
        foreach (var (name, value) in positive)
        {
            if (!double.IsFinite(value) || value <= Tolerance)
                diagnostics.Add(new(HexBoltAdmissionCode.NonFiniteOrNonPositiveDimension, name, $"{name} must be finite and positive."));
        }

        if (!double.IsFinite(spec.TopChamferAngle) || spec.TopChamferAngle <= Tolerance || spec.TopChamferAngle >= 90d - Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.NonFiniteOrNonPositiveDimension, nameof(spec.TopChamferAngle), "TopChamferAngle must be in (0, 90) degrees."));
        if (!double.IsFinite(spec.ThreadLength) || spec.ThreadLength < 0d || spec.ThreadLength > spec.Length + Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.ThreadLengthOutsideShank, nameof(spec.ThreadLength), "ThreadLength must lie in [0, Length]."));
        if (!double.IsFinite(spec.UnderHeadRadius) || spec.UnderHeadRadius < 0d || spec.UnderHeadRadius >= spec.NominalDiameter / 2d)
            diagnostics.Add(new(HexBoltAdmissionCode.UnderHeadRadiusInvalid, nameof(spec.UnderHeadRadius), "UnderHeadRadius must be non-negative and smaller than the shank radius."));
        if (string.IsNullOrWhiteSpace(spec.ThreadDesignation) || string.IsNullOrWhiteSpace(spec.PropertyClass))
            diagnostics.Add(new(HexBoltAdmissionCode.EmptySemanticMetadata, nameof(spec.ThreadDesignation), "ThreadDesignation and PropertyClass must be non-empty semantic metadata."));

        if (diagnostics.Count > 0) return diagnostics;

        var apothem = spec.HeadAcrossFlats / 2d;
        var circumradius = spec.HeadAcrossFlats / double.Sqrt(3d);
        var topRadius = spec.TopFlatDiameter / 2d;
        if (topRadius >= apothem - Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.TopFlatOutsideHex, nameof(spec.TopFlatDiameter), "The top-flat circle must lie strictly inside the hex apothem."));

        var coneSlope = 1d / double.Tan(spec.TopChamferAngle * double.Pi / 180d);
        var axialToCorner = (circumradius - topRadius) / coneSlope;
        if (axialToCorner >= spec.HeadHeight - Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.TopChamferConsumesHead, nameof(spec.HeadHeight), "The cone/hex corner intersection must remain above the under-head plane with non-zero side remnants."));

        if (!double.IsFinite(spec.TipChamferLength) || spec.TipChamferLength <= Tolerance || spec.TipChamferLength >= spec.Length - spec.UnderHeadRadius - Tolerance
            || spec.TipDiameter >= spec.NominalDiameter - Tolerance)
            diagnostics.Add(new(HexBoltAdmissionCode.TipChamferInvalid, nameof(spec.TipChamferLength), "Tip chamfer must have positive axial length, leave a cylindrical shank, and reduce the tip diameter."));
        return diagnostics;
    }

    public static KernelResult<HexBoltDefinition> Create(HexBoltSpec spec, string bodyStableId = "HexBolt")
    {
        var admission = Validate(spec);
        if (admission.Count > 0)
            return KernelResult<HexBoltDefinition>.Failure(admission.Select(d => new KernelDiagnostic(
                KernelDiagnosticCode.InvalidArgument,
                KernelDiagnosticSeverity.Error,
                d.Message,
                $"StandardLibrary.HexBolt.{d.Code}:{d.Field}")));

        var dimensions = Derive(spec);
        var realization = Build(spec, dimensions, bodyStableId);
        var preflight = BrepExportPreflight.Validate(realization.Body);
        if (!preflight.IsValid)
            return KernelResult<HexBoltDefinition>.Failure(preflight.Diagnostics
                .Where(d => d.Severity == BrepExportPreflightSeverity.Error)
                .Select(d => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, d.Message, d.Context)));

        return KernelResult<HexBoltDefinition>.Success(realization);
    }

    public static HexBoltDerivedDimensions Derive(HexBoltSpec spec)
    {
        var apothem = spec.HeadAcrossFlats / 2d;
        var circumradius = spec.HeadAcrossFlats / double.Sqrt(3d);
        var topRadius = spec.TopFlatDiameter / 2d;
        var semiAngle = 90d - spec.TopChamferAngle;
        var slope = double.Tan(semiAngle * double.Pi / 180d);
        var topX = -spec.HeadHeight;
        var apexX = topX - topRadius / slope;
        return new(apothem, circumradius, topRadius, semiAngle, apexX,
            apexX + apothem / slope,
            apexX + circumradius / slope,
            spec.Length - spec.TipChamferLength);
    }

    private static HexBoltDefinition Build(HexBoltSpec spec, HexBoltDerivedDimensions d, string bodyStableId)
    {
        var topology = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var faces = new List<FaceId>();

        VertexId Vertex(Point3D point) { var id = topology.AddVertex(); points[id] = point; return id; }
        EdgeId CurveEdge(VertexId start, VertexId end, CurveGeometry curve, double t0, double t1)
        {
            var edge = topology.AddEdge(start, end);
            var curveId = new CurveGeometryId(geometry.Curves.Count() + 1);
            geometry.AddCurve(curveId, curve);
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, curveId, new ParameterInterval(t0, t1)));
            return edge;
        }
        EdgeId Line(VertexId start, VertexId end)
        {
            var a = points[start]; var b = points[end]; var vector = b - a;
            return CurveEdge(start, end, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(vector))), 0d, vector.Length);
        }
        Ring CircleRing(double x, double radius)
        {
            var positive = Vertex(new Point3D(x, radius, 0d));
            var negative = Vertex(new Point3D(x, -radius, 0d));
            var support = new Circle3Curve(new Point3D(x, 0d, 0d), PlusX, radius, PlusY);
            return new Ring(positive, negative,
                CurveEdge(positive, negative, CurveGeometry.FromCircle(support), 0d, double.Pi),
                CurveEdge(negative, positive, CurveGeometry.FromCircle(support), double.Pi, 2d * double.Pi));
        }
        FaceId Face(IReadOnlyList<IReadOnlyList<Use>> loops, SurfaceGeometry surface, bool sameSense = true, SurfaceGeometryId? sharedSurfaceId = null)
        {
            var loopIds = new List<LoopId>();
            foreach (var uses in loops)
            {
                var loop = topology.AllocateLoopId();
                var coedges = uses.Select(_ => topology.AllocateCoedgeId()).ToArray();
                for (var i = 0; i < coedges.Length; i++)
                    topology.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop, coedges[(i + 1) % coedges.Length], coedges[(i + coedges.Length - 1) % coedges.Length], uses[i].Reversed));
                topology.AddLoop(new Loop(loop, coedges)); loopIds.Add(loop);
            }
            var face = topology.AddFace(loopIds);
            var surfaceId = sharedSurfaceId ?? new SurfaceGeometryId(geometry.Surfaces.Count() + 1);
            if (sharedSurfaceId is null) geometry.AddSurface(surfaceId, surface);
            bindings.AddFaceBinding(new FaceGeometryBinding(face, surfaceId, sameSense));
            faces.Add(face); return face;
        }

        var lower = new VertexId[6]; var upper = new VertexId[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = (30d + i * 60d) * double.Pi / 180d;
            var y = d.HeadCircumradius * double.Cos(angle);
            var z = d.HeadCircumradius * double.Sin(angle);
            lower[i] = Vertex(new Point3D(0d, y, z));
            upper[i] = Vertex(new Point3D(d.TopConeCornerX, y, z));
        }

        var lowerHex = new EdgeId[6]; var upperHyperbolas = new EdgeId[6]; var vertical = new EdgeId[6];
        for (var i = 0; i < 6; i++)
        {
            lowerHex[i] = Line(lower[i], lower[(i + 1) % 6]);
            vertical[i] = Line(lower[i], upper[i]);

            var normalAngle = (60d + i * 60d) * double.Pi / 180d;
            var normal = new Vector3D(0d, double.Cos(normalAngle), double.Sin(normalAngle));
            var tangent = new Vector3D(0d, -double.Sin(normalAngle), double.Cos(normalAngle));
            var center = new Point3D(d.TopConeApexX, normal.Y * d.HeadApothem, normal.Z * d.HeadApothem);
            var support = new Hyperbola3Curve(center, Direction3D.Create(normal), PlusX,
                d.HeadApothem / double.Tan(d.TopConeSemiAngleDegrees * double.Pi / 180d), d.HeadApothem, HyperbolaBranch.PositiveAxisU);
            var tStart = ProjectHyperbolaParameter(support, points[upper[i]]);
            var tEnd = ProjectHyperbolaParameter(support, points[upper[(i + 1) % 6]]);
            if (tEnd < tStart)
            {
                support = support.Reverse();
                tStart = ProjectHyperbolaParameter(support, points[upper[i]]);
                tEnd = ProjectHyperbolaParameter(support, points[upper[(i + 1) % 6]]);
            }
            upperHyperbolas[i] = CurveEdge(upper[i], upper[(i + 1) % 6], CurveGeometry.FromHyperbola(support), tStart, tEnd);
        }

        var shankRadius = spec.NominalDiameter / 2d;
        var underHeadOuterRadius = shankRadius + spec.UnderHeadRadius;
        var torusEndX = spec.UnderHeadRadius;
        var topVertices = new VertexId[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = (30d + i * 60d) * double.Pi / 180d;
            topVertices[i] = Vertex(new Point3D(-spec.HeadHeight, d.TopFlatRadius * double.Cos(angle), d.TopFlatRadius * double.Sin(angle)));
        }
        var topArcs = new EdgeId[6]; var coneGenerators = new EdgeId[6];
        var topCircleSupport = new Circle3Curve(new Point3D(-spec.HeadHeight, 0d, 0d), PlusX, d.TopFlatRadius, PlusY);
        for (var i = 0; i < 6; i++)
        {
            var startAngle = (30d + i * 60d) * double.Pi / 180d;
            topArcs[i] = CurveEdge(topVertices[i], topVertices[(i + 1) % 6], CurveGeometry.FromCircle(topCircleSupport), startAngle, startAngle + double.Pi / 3d);
            coneGenerators[i] = Line(topVertices[i], upper[i]);
        }
        var underHeadRing = CircleRing(0d, underHeadOuterRadius);
        var shankStartRing = spec.UnderHeadRadius > Tolerance ? CircleRing(torusEndX, shankRadius) : underHeadRing;
        var tipStartRing = CircleRing(d.TipChamferStartX, shankRadius);
        var tipRing = CircleRing(spec.Length, spec.TipDiameter / 2d);

        var sideFaces = new FaceId[6];
        for (var i = 0; i < 6; i++)
        {
            var a = points[lower[i]]; var b = points[lower[(i + 1) % 6]];
            var tangent = Direction3D.Create(b - a);
            var outward = Direction3D.Create(tangent.ToVector().Cross(PlusX.ToVector()));
            sideFaces[i] = Face([[new(lowerHex[i], false), new(vertical[(i + 1) % 6], false), new(upperHyperbolas[i], true), new(vertical[i], true)]],
                SurfaceGeometry.FromPlane(new PlaneSurface(a, outward, PlusX)));
        }

        var cone = new ConeSurface(new Point3D(d.TopConeApexX, 0d, 0d), PlusX,
            d.TopConeSemiAngleDegrees * double.Pi / 180d, PlusY);
        var coneSurfaceId = new SurfaceGeometryId(geometry.Surfaces.Count() + 1);
        geometry.AddSurface(coneSurfaceId, SurfaceGeometry.FromCone(cone));
        var topChamferFaces = new FaceId[6];
        for (var i = 0; i < 6; i++)
            topChamferFaces[i] = Face([[new(topArcs[i], false), new(coneGenerators[(i + 1) % 6], false), new(upperHyperbolas[i], true), new(coneGenerators[i], true)]], SurfaceGeometry.FromCone(cone), sharedSurfaceId: coneSurfaceId);
        var topFlat = Face([topArcs.Select(e => new Use(e, false)).ToArray()], SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(-spec.HeadHeight, 0d, 0d), MinusX, PlusY)));
        var underHead = Face([lowerHex.Reverse().Select(e => new Use(e, true)).ToArray(), [new(underHeadRing.Arc0, false), new(underHeadRing.Arc1, false)]],
            SurfaceGeometry.FromPlane(new PlaneSurface(Point3D.Origin, PlusX, PlusY)));

        var underHeadBlendFaces = new List<FaceId>();
        if (spec.UnderHeadRadius > Tolerance)
        {
            var plusCenter = new Point3D(spec.UnderHeadRadius, underHeadOuterRadius, 0d);
            var minusCenter = new Point3D(spec.UnderHeadRadius, -underHeadOuterRadius, 0d);
            var plusSeam = CurveEdge(underHeadRing.Positive, shankStartRing.Positive,
                CurveGeometry.FromCircle(new Circle3Curve(plusCenter, Direction3D.Create(new Vector3D(0d, 0d, 1d)), spec.UnderHeadRadius, MinusX)), 0d, double.Pi / 2d);
            var minusSeam = CurveEdge(underHeadRing.Negative, shankStartRing.Negative,
                CurveGeometry.FromCircle(new Circle3Curve(minusCenter, Direction3D.Create(new Vector3D(0d, 0d, -1d)), spec.UnderHeadRadius, MinusX)), 0d, double.Pi / 2d);
            var torusSupport = SurfaceGeometry.FromTorus(new TorusSurface(new Point3D(spec.UnderHeadRadius, 0d, 0d), PlusX, underHeadOuterRadius, spec.UnderHeadRadius, PlusY));
            var torusSurfaceId = new SurfaceGeometryId(geometry.Surfaces.Count() + 1); geometry.AddSurface(torusSurfaceId, torusSupport);
            underHeadBlendFaces.Add(Face([[new(underHeadRing.Arc0, false), new(minusSeam, false), new(shankStartRing.Arc0, true), new(plusSeam, true)]], torusSupport, sharedSurfaceId: torusSurfaceId));
            underHeadBlendFaces.Add(Face([[new(underHeadRing.Arc1, false), new(plusSeam, false), new(shankStartRing.Arc1, true), new(minusSeam, true)]], torusSupport, sharedSurfaceId: torusSurfaceId));
        }
        var shankPlusSeam = Line(shankStartRing.Positive, tipStartRing.Positive);
        var shankMinusSeam = Line(shankStartRing.Negative, tipStartRing.Negative);
        var cylinderSupport = SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(torusEndX, 0d, 0d), PlusX, shankRadius, PlusY));
        var cylinderSurfaceId = new SurfaceGeometryId(geometry.Surfaces.Count() + 1); geometry.AddSurface(cylinderSurfaceId, cylinderSupport);
        var shankFaces = new[]
        {
            Face([[new(shankStartRing.Arc0, false), new(shankMinusSeam, false), new(tipStartRing.Arc0, true), new(shankPlusSeam, true)]], cylinderSupport, sharedSurfaceId: cylinderSurfaceId),
            Face([[new(shankStartRing.Arc1, false), new(shankPlusSeam, false), new(tipStartRing.Arc1, true), new(shankMinusSeam, true)]], cylinderSupport, sharedSurfaceId: cylinderSurfaceId)
        };
        var tipSlope = (shankRadius - spec.TipDiameter / 2d) / spec.TipChamferLength;
        var tipApexX = d.TipChamferStartX + shankRadius / tipSlope;
        var tipPlusSeam = Line(tipStartRing.Positive, tipRing.Positive);
        var tipMinusSeam = Line(tipStartRing.Negative, tipRing.Negative);
        var tipSupport = SurfaceGeometry.FromCone(new ConeSurface(new Point3D(tipApexX, 0d, 0d), MinusX, double.Atan(tipSlope), PlusY));
        var tipSurfaceId = new SurfaceGeometryId(geometry.Surfaces.Count() + 1); geometry.AddSurface(tipSurfaceId, tipSupport);
        var tipChamferFaces = new[]
        {
            Face([[new(tipStartRing.Arc0, false), new(tipMinusSeam, false), new(tipRing.Arc0, true), new(tipPlusSeam, true)]], tipSupport, sharedSurfaceId: tipSurfaceId),
            Face([[new(tipStartRing.Arc1, false), new(tipPlusSeam, false), new(tipRing.Arc1, true), new(tipMinusSeam, true)]], tipSupport, sharedSurfaceId: tipSurfaceId)
        };
        var tipFace = Face([[new(tipRing.Arc1, true), new(tipRing.Arc0, true)]], SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(spec.Length, 0d, 0d), PlusX, PlusY)));

        var shell = topology.AddShell(faces); topology.AddBody([shell]);
        var body = new BrepBody(topology.Model, geometry, bindings, points);
        var descendants = new List<HexBoltSemanticDescendant>
        {
            new(bodyStableId, HexBoltSemanticKind.Part),
            new($"{bodyStableId}.Head", HexBoltSemanticKind.Region, ParentStableId: bodyStableId),
            new($"{bodyStableId}.Head.TopChamfer", HexBoltSemanticKind.Region, ParentStableId: $"{bodyStableId}.Head"),
            new($"{bodyStableId}.Head.TopFlat", HexBoltSemanticKind.Face, topFlat, $"{bodyStableId}.Head"),
            new($"{bodyStableId}.Head.UnderHead", HexBoltSemanticKind.Face, underHead, $"{bodyStableId}.Head"),
            new($"{bodyStableId}.Shank", HexBoltSemanticKind.Region, ParentStableId: bodyStableId),
            new($"{bodyStableId}.ThreadRegion", HexBoltSemanticKind.Region, ParentStableId: bodyStableId, Metadata: $"{spec.ThreadDesignation};length={spec.ThreadLength:R}mm;material-geometry=Cylinder"),
            new($"{bodyStableId}.TipChamfer", HexBoltSemanticKind.Region, ParentStableId: bodyStableId),
            new($"{bodyStableId}.TipFace", HexBoltSemanticKind.Face, tipFace, bodyStableId)
        };
        for (var i = 0; i < sideFaces.Length; i++) descendants.Add(new($"{bodyStableId}.Head.Side[{i}]", HexBoltSemanticKind.Face, sideFaces[i], $"{bodyStableId}.Head"));
        for (var i = 0; i < topChamferFaces.Length; i++) descendants.Add(new($"{bodyStableId}.Head.TopChamfer.Face[{i}]", HexBoltSemanticKind.Face, topChamferFaces[i], $"{bodyStableId}.Head.TopChamfer"));
        for (var i = 0; i < shankFaces.Length; i++) descendants.Add(new($"{bodyStableId}.Shank.Face[{i}]", HexBoltSemanticKind.Face, shankFaces[i], $"{bodyStableId}.Shank"));
        for (var i = 0; i < shankFaces.Length; i++) descendants.Add(new($"{bodyStableId}.ThreadRegion.Face[{i}]", HexBoltSemanticKind.Face, shankFaces[i], $"{bodyStableId}.ThreadRegion"));
        for (var i = 0; i < tipChamferFaces.Length; i++) descendants.Add(new($"{bodyStableId}.TipChamfer.Face[{i}]", HexBoltSemanticKind.Face, tipChamferFaces[i], $"{bodyStableId}.TipChamfer"));
        for (var i = 0; i < underHeadBlendFaces.Count; i++) descendants.Add(new($"{bodyStableId}.Head.UnderHeadBlend.Face[{i}]", HexBoltSemanticKind.Face, underHeadBlendFaces[i], $"{bodyStableId}.Head"));

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NominalDiameter"] = $"{spec.NominalDiameter:R}mm",
            ["ThreadLength"] = $"{spec.ThreadLength:R}mm",
            ["ThreadDesignation"] = spec.ThreadDesignation,
            ["PropertyClass"] = spec.PropertyClass,
            ["ThreadGeometry"] = "deferred-semantic-cylinder"
        };
        var signatureSource = string.Join("|", new[]
        {
            spec.NominalDiameter, spec.Length, spec.HeadAcrossFlats, spec.HeadHeight, spec.TopFlatDiameter,
            spec.TopChamferAngle, spec.TipChamferLength, spec.TipDiameter, spec.ThreadLength, spec.UnderHeadRadius
        }.Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + $"|{spec.ThreadDesignation}|{spec.PropertyClass}";
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureSource))).ToLowerInvariant();
        return new(spec, d, body, new(bodyStableId, descendants, metadata), signature);
    }

    private static double ProjectHyperbolaParameter(Hyperbola3Curve curve, Point3D point)
    {
        var relative = point - curve.Center;
        var sinh = relative.Dot(curve.AxisV.ToVector()) / curve.SemiAxisB;
        return double.Asinh(sinh);
    }

    private readonly record struct Ring(VertexId Positive, VertexId Negative, EdgeId Arc0, EdgeId Arc1);
    private readonly record struct Use(EdgeId Edge, bool Reversed);
}
