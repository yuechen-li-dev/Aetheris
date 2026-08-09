using Aetheris.Kernel.Core.Brep.Prismatic;
using Aetheris.Kernel.Core.Math;
using Aetheris.Continuum.Backends.Sdf;

namespace Aetheris.Continuum.Mirrors;

internal enum CirConvexPointClassification
{
    Inside,
    Boundary,
    Outside,
}

internal enum CirPrismaticMirrorRequestKind
{
    PointContainment,
    MapOccupancy,
    FaceIdentity,
    TopologyParity,
}

internal readonly record struct CirHalfSpacePlane(Vector3D Normal, double Offset, string Label)
{
    public double Evaluate(Point3D point) =>
        (Normal.X * point.X) + (Normal.Y * point.Y) + (Normal.Z * point.Z) + Offset;
}

internal sealed record CirPrismaticMirrorSummary(
    int Rows,
    int Cols,
    string View,
    int OccupiedCount,
    int EmptyCount,
    double? ThicknessMin,
    double? ThicknessMax,
    double? ThicknessAverage,
    CirBounds Bounds,
    IReadOnlyList<string> Diagnostics);

internal sealed record CirPrismaticMirrorResult(
    CirMirrorStatus Status,
    CirConvexPolyhedronMirror? Mirror,
    CirMirrorAdmissionResult Admission,
    IReadOnlyList<string> Diagnostics,
    string Recommendation)
{
    public bool Succeeded => Status == CirMirrorStatus.MirrorAdmittedExact && Mirror is not null;
}

internal sealed class CirConvexPolyhedronMirror
{
    private const double DefaultTolerance = 1e-8d;

    public CirConvexPolyhedronMirror(
        string caseLabel,
        IReadOnlyList<CirHalfSpacePlane> HalfSpaces,
        CirBounds bounds,
        CirMirrorAdmissionResult admission,
        IReadOnlyList<string> diagnostics)
    {
        CaseLabel = caseLabel;
        this.HalfSpaces = HalfSpaces;
        Bounds = bounds;
        Admission = admission;
        Diagnostics = diagnostics;
    }

    public string CaseLabel { get; }

    public IReadOnlyList<CirHalfSpacePlane> HalfSpaces { get; }

    public CirBounds Bounds { get; }

    public CirMirrorAdmissionResult Admission { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public double Evaluate(Point3D point) => HalfSpaces.Max(plane => plane.Evaluate(point));

    public CirConvexPointClassification Classify(Point3D point, double tolerance = DefaultTolerance)
    {
        var violation = Evaluate(point);
        if (violation > tolerance)
        {
            return CirConvexPointClassification.Outside;
        }

        return violation >= -tolerance ? CirConvexPointClassification.Boundary : CirConvexPointClassification.Inside;
    }

    public CirPrismaticMirrorSummary CreateTopViewSummary(int rows = 16, int cols = 16, double tolerance = DefaultTolerance)
    {
        if (!Admission.Supports(CirMirrorCapability.MapOccupancy))
        {
            return new CirPrismaticMirrorSummary(rows, cols, "top", 0, rows * cols, null, null, null, Bounds,
                Diagnostics.Concat(["cir-prismatic-x2-mirror-rejected-lossy-for-request:map-occupancy"]).ToArray());
        }

        var occupied = 0;
        var thicknesses = new List<double>();
        var diagnostics = Diagnostics.Concat([$"cir-prismatic-x2-map-summary-created:{CaseLabel}"]).ToArray();

        for (var row = 0; row < rows; row++)
        {
            var y = Bounds.Min.Y + ((row + 0.5d) / rows * Bounds.SizeY);
            for (var col = 0; col < cols; col++)
            {
                var x = Bounds.Min.X + ((col + 0.5d) / cols * Bounds.SizeX);
                if (TryIntersectVerticalRay(x, y, tolerance, out var zMin, out var zMax))
                {
                    var thickness = double.Max(0d, zMax - zMin);
                    if (thickness > tolerance)
                    {
                        occupied++;
                        thicknesses.Add(thickness);
                    }
                }
            }
        }

        var total = rows * cols;
        return new CirPrismaticMirrorSummary(
            rows,
            cols,
            "top",
            occupied,
            total - occupied,
            thicknesses.Count == 0 ? null : thicknesses.Min(),
            thicknesses.Count == 0 ? null : thicknesses.Max(),
            thicknesses.Count == 0 ? null : thicknesses.Average(),
            Bounds,
            diagnostics);
    }

    public CirMirrorAdmissionResult RejectLossyRequest(CirPrismaticMirrorRequestKind requestKind)
    {
        var request = requestKind switch
        {
            CirPrismaticMirrorRequestKind.FaceIdentity => CirMirrorCapability.FaceIdentity,
            CirPrismaticMirrorRequestKind.TopologyParity => CirMirrorCapability.TopologyParity,
            CirPrismaticMirrorRequestKind.PointContainment => CirMirrorCapability.PointContainment,
            CirPrismaticMirrorRequestKind.MapOccupancy => CirMirrorCapability.MapOccupancy,
            _ => CirMirrorCapability.None,
        };

        if ((request & (CirMirrorCapability.FaceIdentity | CirMirrorCapability.TopologyParity)) == 0)
        {
            return Admission;
        }

        var token = requestKind == CirPrismaticMirrorRequestKind.FaceIdentity ? "face-identity" : "topology-parity";
        var diagnostics = new List<string>
        {
            $"cir-prismatic-x2-mirror-rejected-lossy-for-request:{token}",
            "cir-prismatic-x2-loss-face-identity",
            "cir-prismatic-x2-loss-loop-identity",
            "cir-prismatic-x2-loss-split-face-lineage",
            "cir-prismatic-x2-loss-topology-parity",
            "cir-prismatic-x2-no-production-analyzer-behavior-changed",
            "cir-prismatic-x2-no-cir-to-brep-extraction",
        };

        return CreateAdmission(CaseLabel, CirMirrorStatus.MirrorRejectedLossyForRequest, CirMirrorCapability.None, diagnostics);
    }

    private bool TryIntersectVerticalRay(double x, double y, double tolerance, out double zMin, out double zMax)
    {
        zMin = Bounds.Min.Z;
        zMax = Bounds.Max.Z;

        foreach (var plane in HalfSpaces)
        {
            var constant = (plane.Normal.X * x) + (plane.Normal.Y * y) + plane.Offset;
            if (double.Abs(plane.Normal.Z) <= tolerance)
            {
                if (constant > tolerance)
                {
                    return false;
                }

                continue;
            }

            var zLimit = (tolerance - constant) / plane.Normal.Z;
            if (plane.Normal.Z > 0d)
            {
                zMax = double.Min(zMax, zLimit);
            }
            else
            {
                zMin = double.Max(zMin, zLimit);
            }

            if (zMin > zMax + tolerance)
            {
                return false;
            }
        }

        return zMax >= zMin - tolerance;
    }

    internal static CirMirrorAdmissionResult CreateAdmission(string caseLabel, CirMirrorStatus status, CirMirrorCapability capabilities, IReadOnlyList<string> diagnostics)
    {
        var provenance = new CirMirrorProvenance(
            CirMirrorSourceRepresentationKind.Air,
            "cir-prismatic-x2-convex-polyhedron",
            caseLabel,
            "cir-prismatic-x2-halfspace-v1",
            "convex-all-planar-prismatic-halfspace-set",
            "default-1e-8",
            diagnostics);
        var losses = CirMirrorRegistry.PrismaticConvexMirrorLosses;
        var descriptor = new CirMirrorDescriptor(
            $"cir-prismatic-x2:{caseLabel}",
            CirMirrorAtomKind.PrismaticSectionTransition,
            status,
            capabilities,
            losses,
            provenance,
            diagnostics);
        return new CirMirrorAdmissionResult(status, capabilities, losses, provenance, diagnostics, descriptor);
    }
}

internal static class CirPrismaticMirrorBuilder
{
    private const double Tol = 1e-9d;

    public static CirPrismaticMirrorResult BuildFromSections(
        string caseLabel,
        IReadOnlyList<PrismaticSection> sections,
        PrismaticCorrespondenceMap? correspondence = null,
        double tolerance = Tol)
    {
        var token = Normalize(caseLabel);
        var diagnostics = new List<string>
        {
            "cir-prismatic-x2-mirror-builder-started",
        };

        var validation = Validate(sections, correspondence, tolerance, diagnostics);
        if (!validation.Valid)
        {
            diagnostics.Add($"cir-prismatic-x2-mirror-rejected-unsupported:{validation.Reason}");
            diagnostics.Add("cir-prismatic-x2-no-production-analyzer-behavior-changed");
            diagnostics.Add("cir-prismatic-x2-no-cir-to-brep-extraction");
            var rejectedAdmission = CirConvexPolyhedronMirror.CreateAdmission(token, CirMirrorStatus.MirrorRejectedUnsupportedAtom, CirMirrorCapability.None, diagnostics);
            return new CirPrismaticMirrorResult(CirMirrorStatus.MirrorRejectedUnsupportedAtom, null, rejectedAdmission, diagnostics, validation.Reason);
        }

        var vertexMap = correspondence?.VertexMap ?? Enumerable.Range(0, sections[0].OuterLoop.Count).ToArray();
        var vertices = sections.SelectMany(section => section.OuterLoop.Select(vertex => new Point3D(vertex.X, vertex.Y, section.Z))).ToArray();
        var centroid = new Point3D(vertices.Average(point => point.X), vertices.Average(point => point.Y), vertices.Average(point => point.Z));
        var bounds = Bounds(vertices);
        var planes = new List<CirHalfSpacePlane>
        {
            new(new Vector3D(0d, 0d, -1d), sections[0].Z, "cap-lower"),
            new(new Vector3D(0d, 0d, 1d), -sections[^1].Z, "cap-upper"),
        };

        for (var interval = 0; interval < sections.Count - 1; interval++)
        {
            var lower = sections[interval];
            var upper = sections[interval + 1];
            for (var edge = 0; edge < lower.OuterLoop.Count; edge++)
            {
                var next = (edge + 1) % lower.OuterLoop.Count;
                var p0 = ToPoint(lower, edge);
                var p1 = ToPoint(lower, next);
                var q1 = ToPoint(upper, vertexMap[next]);

                var edgeVector = p1 - p0;
                var riseVector = q1 - p0;
                var normal = edgeVector.Cross(riseVector);
                if (!normal.TryNormalize(out var normalized))
                {
                    diagnostics.Add("cir-prismatic-x2-mirror-rejected-unsupported:degenerate-side-plane");
                    var rejectedAdmission = CirConvexPolyhedronMirror.CreateAdmission(token, CirMirrorStatus.MirrorRejectedUnsupportedAtom, CirMirrorCapability.None, diagnostics);
                    return new CirPrismaticMirrorResult(CirMirrorStatus.MirrorRejectedUnsupportedAtom, null, rejectedAdmission, diagnostics, "degenerate-side-plane");
                }

                var offset = -normalized.Dot(p0 - Point3D.Origin);
                if (((normalized.X * centroid.X) + (normalized.Y * centroid.Y) + (normalized.Z * centroid.Z) + offset) > 0d)
                {
                    normalized = -normalized;
                    offset = -normalized.Dot(p0 - Point3D.Origin);
                }

                planes.Add(new CirHalfSpacePlane(normalized, offset, $"side-{interval}-{edge}"));
            }
        }

        var worstViolation = vertices.Max(vertex => planes.Max(plane => plane.Evaluate(vertex)));
        if (worstViolation > tolerance * 10d)
        {
            diagnostics.Add("cir-prismatic-x2-mirror-rejected-unsupported:non-convex-or-inconsistent-halfspaces");
            var rejectedAdmission = CirConvexPolyhedronMirror.CreateAdmission(token, CirMirrorStatus.MirrorRejectedUnsupportedAtom, CirMirrorCapability.None, diagnostics);
            return new CirPrismaticMirrorResult(CirMirrorStatus.MirrorRejectedUnsupportedAtom, null, rejectedAdmission, diagnostics, "non-convex-or-inconsistent-halfspaces");
        }

        diagnostics.Add($"cir-prismatic-x2-halfspace-mirror-created:{token}");
        diagnostics.Add($"cir-prismatic-x2-halfspace-count:{planes.Count}");
        diagnostics.Add($"cir-prismatic-x2-mirror-admitted-exact:{token}");
        diagnostics.Add("cir-prismatic-x2-loss-face-identity");
        diagnostics.Add("cir-prismatic-x2-loss-loop-identity");
        diagnostics.Add("cir-prismatic-x2-loss-split-face-lineage");
        diagnostics.Add("cir-prismatic-x2-loss-topology-parity");
        diagnostics.Add("cir-prismatic-x2-no-production-analyzer-behavior-changed");
        diagnostics.Add("cir-prismatic-x2-no-cir-to-brep-extraction");

        var admission = CirConvexPolyhedronMirror.CreateAdmission(
            token,
            CirMirrorStatus.MirrorAdmittedExact,
            CirMirrorRegistry.PrismaticConvexMirrorCapabilities,
            diagnostics);
        var mirror = new CirConvexPolyhedronMirror(token, planes, bounds, admission, diagnostics);
        return new CirPrismaticMirrorResult(CirMirrorStatus.MirrorAdmittedExact, mirror, admission, diagnostics, "cir-prismatic-x2-convex-polyhedron-mirror-ready-for-lab-use");
    }

    private static (bool Valid, string Reason) Validate(IReadOnlyList<PrismaticSection> sections, PrismaticCorrespondenceMap? correspondence, double tolerance, List<string> diagnostics)
    {
        if (sections.Count < 2)
        {
            return (false, "fewer-than-two-sections");
        }

        var vertexCount = sections[0].OuterLoop.Count;
        if (vertexCount < 3)
        {
            return (false, "fewer-than-three-vertices");
        }

        if (correspondence is not null && (correspondence.VertexMap.Count != vertexCount || correspondence.VertexMap.Distinct().Count() != vertexCount || correspondence.VertexMap.Any(index => index < 0 || index >= vertexCount)))
        {
            return (false, "invalid-correspondence");
        }

        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var section = sections[sectionIndex];
            if (!double.IsFinite(section.Z) || section.HasArcs || section.HasHoles || section.OuterLoopCount != 1)
            {
                return (false, "unsupported-section-atom");
            }

            if (section.OuterLoop.Count != vertexCount)
            {
                return (false, "mismatched-vertex-count");
            }

            if (sectionIndex > 0 && section.Z <= sections[sectionIndex - 1].Z + tolerance)
            {
                return (false, "non-increasing-z");
            }

            if (!IsConvexCounterClockwise(section, tolerance))
            {
                return (false, "non-convex-or-clockwise-section");
            }

            diagnostics.Add("cir-prismatic-x2-section-validated");
        }

        return (true, "ok");
    }

    private static bool IsConvexCounterClockwise(PrismaticSection section, double tolerance)
    {
        var vertices = section.OuterLoop;
        var area = 0d;
        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        if (area <= tolerance)
        {
            return false;
        }

        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            var c = vertices[(i + 2) % vertices.Count];
            var cross = ((b.X - a.X) * (c.Y - b.Y)) - ((b.Y - a.Y) * (c.X - b.X));
            if (cross < -tolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static Point3D ToPoint(PrismaticSection section, int index) =>
        new(section.OuterLoop[index].X, section.OuterLoop[index].Y, section.Z);

    private static CirBounds Bounds(IReadOnlyList<Point3D> vertices) =>
        new(
            new Point3D(vertices.Min(point => point.X), vertices.Min(point => point.Y), vertices.Min(point => point.Z)),
            new Point3D(vertices.Max(point => point.X), vertices.Max(point => point.Y), vertices.Max(point => point.Z)));

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');
}
