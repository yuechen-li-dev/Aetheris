using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Air;

/// <summary>Bounded M9 authority model. A fill region is never inferred from wall distances or topology.</summary>
public sealed record LatticeFillRegion(string RegionId, AxisAlignedBoxExtents Bounds, string Provenance)
{
    public double Width => Bounds.MaxX - Bounds.MinX;
    public double Depth => Bounds.MaxY - Bounds.MinY;
    public double Height => Bounds.MaxZ - Bounds.MinZ;
}

public enum LatticePatternKind { OctetTruss }
public enum LatticeBoundaryPolicy { Bond }

public sealed record AdditiveManufacturingContext(
    string Template,
    string Process,
    double MinimumWallThickness,
    double MinimumStrutDiameter,
    double MinimumBondDiameter,
    double MinimumHoleDiameter,
    string Provenance);

public sealed record LatticeFillProvenance(string SourceKind, string SourceSymbol, string SourceSpan, IReadOnlyList<string> Notes);

/// <summary>Feature AIR remains semantic; it deliberately does not contain one source feature per cylinder.</summary>
public sealed record LatticeFillFeature(
    string FeatureId,
    string HostId,
    LatticeFillRegion Region,
    LatticePatternKind Pattern,
    double CellSize,
    double StrutRadius,
    LatticeBoundaryPolicy BoundaryPolicy,
    AdditiveManufacturingContext AdditiveContext,
    LatticeFillProvenance Provenance);

/// <summary>M9R's standalone intent. Host replacement remains a separate, deferred M9 route.</summary>
public enum CubicLatticePlacementPolicy { MaterialBounds }
public sealed record CubicLatticeAdditiveContext(string Template, string Process, double MinimumStrutDiameter, double MinimumNodeDiameter, double MinimumFeatureSpacing, string Provenance);
public sealed record CubicLatticeFeature(
    string FeatureId,
    LatticeFillRegion Domain,
    int CellsX,
    int CellsY,
    int CellsZ,
    double CellSize,
    double StrutRadius,
    double NodeRadius,
    CubicLatticePlacementPolicy PlacementPolicy,
    CubicLatticeAdditiveContext AdditiveContext,
    LatticeFillProvenance Provenance)
{
    public string Pattern => "CubicTruss";
    public string Materialization => "StandaloneBody";
    public string? HostId => null;
}

public sealed record LatticeNodeInstance(string Id, Point3D Position, string Role);
public sealed record LatticeMemberInstance(string Id, string StartNodeId, string EndNodeId, Point3D Start, Point3D End, string Role);
public sealed record LatticeBoundaryIncident(string MemberId, string NodeId, string BoundaryPlane, Point3D Point);
public sealed record LatticeAttachmentWitness(string Id, string MemberId, string BoundaryPlane, Point3D Point, double ContactDiameter, string RetainedHostOwner);

/// <summary>Hierarchical construction evidence for M9. Geometry lowering is intentionally a later, authoritative-plan stage.</summary>
public sealed record LatticeFillConstruction(
    LatticeFillFeature Feature,
    IReadOnlyList<(int X, int Y, int Z)> CellDomain,
    IReadOnlyList<LatticeNodeInstance> NodeInstances,
    IReadOnlyList<LatticeMemberInstance> MemberInstances,
    IReadOnlyList<LatticeBoundaryIncident> BoundaryIncidents,
    IReadOnlyList<LatticeAttachmentWitness> AttachmentWitnesses,
    string Signature,
    IReadOnlyList<string> Diagnostics)
{
    public int JunctionCount => NodeInstances.Count(n => MemberInstances.Count(m => m.StartNodeId == n.Id || m.EndNodeId == n.Id) > 1);
}

public static class LatticeFillM9
{
    public const string FillRegionDegenerate = "fill-region-degenerate";
    public const string FillRegionOutsideHost = "fill-region-outside-host";
    public const string FillRegionIntersectsExterior = "fill-region-intersects-exterior";
    public const string FillRegionIntersectsVoid = "fill-region-intersects-void";
    public const string UnsupportedFillRegionGeometry = "unsupported-fill-region-geometry";
    public const string CellSizeInvalid = "lattice-cell-size-invalid";
    public const string StrutRadiusInvalid = "lattice-strut-radius-invalid";
    public const string RegionTooSmall = "lattice-region-too-small";
    public const string MinimumStrutDiameterViolation = "additive-minimum-strut-diameter-violation";
    public const string MinimumWallThicknessViolation = "additive-minimum-wall-thickness-violation";
    public const string MinimumHoleDiameterViolation = "additive-minimum-hole-diameter-violation";
    public const string MinimumBondDiameterViolation = "additive-minimum-bond-diameter-violation";

    public static IReadOnlyList<string> Validate(
        LatticeFillFeature feature,
        AxisAlignedBoxExtents host,
        double throughHoleDiameter,
        Point3D throughHoleCenter)
    {
        var d = new List<string>();
        var r = feature.Region.Bounds;
        const double eps = 1e-9;
        if (feature.Pattern != LatticePatternKind.OctetTruss) d.Add(UnsupportedFillRegionGeometry);
        if (r.MaxX - r.MinX <= eps || r.MaxY - r.MinY <= eps || r.MaxZ - r.MinZ <= eps) d.Add(FillRegionDegenerate);
        if (!host.Contains(r, Numerics.ToleranceContext.Default)) d.Add(FillRegionOutsideHost);
        if (r.MinX <= host.MinX + eps || r.MaxX >= host.MaxX - eps || r.MinY <= host.MinY + eps || r.MaxY >= host.MaxY - eps || r.MinZ <= host.MinZ + eps || r.MaxZ >= host.MaxZ - eps) d.Add(FillRegionIntersectsExterior);
        if (IntersectsZThroughHole(r, throughHoleCenter, throughHoleDiameter / 2d, eps)) d.Add(FillRegionIntersectsVoid);
        if (!double.IsFinite(feature.CellSize) || feature.CellSize <= eps) d.Add(CellSizeInvalid);
        if (!double.IsFinite(feature.StrutRadius) || feature.StrutRadius <= eps) d.Add(StrutRadiusInvalid);
        if (feature.CellSize > eps && (feature.Region.Width < feature.CellSize || feature.Region.Depth < feature.CellSize || feature.Region.Height < feature.CellSize)) d.Add(RegionTooSmall);

        var c = feature.AdditiveContext;
        if (2d * feature.StrutRadius + eps < c.MinimumStrutDiameter) d.Add($"{MinimumStrutDiameterViolation}: template '{c.Template}', feature '{feature.FeatureId}', actual {2d * feature.StrutRadius:R}mm, required {c.MinimumStrutDiameter:R}mm.");
        if (throughHoleDiameter + eps < c.MinimumHoleDiameter) d.Add($"{MinimumHoleDiameterViolation}: template '{c.Template}', host hole actual {throughHoleDiameter:R}mm, required {c.MinimumHoleDiameter:R}mm.");
        var clearances = new[] { r.MinX - host.MinX, host.MaxX - r.MaxX, r.MinY - host.MinY, host.MaxY - r.MaxY, r.MinZ - host.MinZ, host.MaxZ - r.MaxZ };
        if (clearances.Any(v => v + eps < c.MinimumWallThickness)) d.Add($"{MinimumWallThicknessViolation}: template '{c.Template}', actual {clearances.Min():R}mm, required {c.MinimumWallThickness:R}mm.");
        if (2d * feature.StrutRadius + eps < c.MinimumBondDiameter) d.Add($"{MinimumBondDiameterViolation}: template '{c.Template}', terminal contact actual {2d * feature.StrutRadius:R}mm, required {c.MinimumBondDiameter:R}mm.");
        return d;
    }

    public static LatticeFillConstruction Construct(LatticeFillFeature feature)
    {
        if (feature.Pattern != LatticePatternKind.OctetTruss) throw new ArgumentOutOfRangeException(nameof(feature));
        var r = feature.Region.Bounds;
        var nx = (int)System.Math.Floor((r.MaxX - r.MinX) / feature.CellSize + 1e-9);
        var ny = (int)System.Math.Floor((r.MaxY - r.MinY) / feature.CellSize + 1e-9);
        var nz = (int)System.Math.Floor((r.MaxZ - r.MinZ) / feature.CellSize + 1e-9);
        if (nx < 1 || ny < 1 || nz < 1) throw new ArgumentException(RegionTooSmall, nameof(feature));

        var cells = new List<(int X, int Y, int Z)>();
        var nodes = new Dictionary<string, LatticeNodeInstance>(StringComparer.Ordinal);
        var members = new Dictionary<string, LatticeMemberInstance>(StringComparer.Ordinal);
        for (var x = 0; x < nx; x++) for (var y = 0; y < ny; y++) for (var z = 0; z < nz; z++)
        {
            cells.Add((x, y, z));
            var corners = new[] { (0,0,0), (1,0,0), (1,1,0), (0,1,0), (0,0,1), (1,0,1), (1,1,1), (0,1,1) };
            // The six face-centres and their four face corners are the tetrahedral-octahedral (octet) cell graph.
            var faces = new[] { ("-X", new[]{0,3,7,4}), ("+X", new[]{1,2,6,5}), ("-Y", new[]{0,1,5,4}), ("+Y", new[]{3,2,6,7}), ("-Z", new[]{0,1,2,3}), ("+Z", new[]{4,5,6,7}) };
            var point = (int a, int b, int c) => new Point3D(r.MinX + (x + a) * feature.CellSize, r.MinY + (y + b) * feature.CellSize, r.MinZ + (z + c) * feature.CellSize);
            var cornerIds = corners.Select(c => Node("C", x + c.Item1, y + c.Item2, z + c.Item3, point(c.Item1, c.Item2, c.Item3))).ToArray();
            foreach (var (role, indices) in faces)
            {
                var center = new Point3D(indices.Select(i => nodes[cornerIds[i]].Position.X).Average(), indices.Select(i => nodes[cornerIds[i]].Position.Y).Average(), indices.Select(i => nodes[cornerIds[i]].Position.Z).Average());
                // Face-centres are shared by the two cells on either side of an internal face.
                // The axis family and doubled lattice coordinates identify the geometric node;
                // the local +/- face role must not leak into identity.
                var faceId = Node("F" + role[1], x * 2 + (role is "+X" ? 2 : role is "-X" ? 0 : 1), y * 2 + (role is "+Y" ? 2 : role is "-Y" ? 0 : 1), z * 2 + (role is "+Z" ? 2 : role is "-Z" ? 0 : 1), center);
                foreach (var corner in indices) Member(faceId, cornerIds[corner], $"cell:{x},{y},{z}:{role}");
            }
        }
        var boundary = new List<LatticeBoundaryIncident>();
        foreach (var member in members.Values)
        foreach (var nodeId in new[] { member.StartNodeId, member.EndNodeId })
        {
            var p = nodes[nodeId].Position;
            var plane = BoundaryPlane(p, r);
            if (plane is not null) boundary.Add(new(member.Id, nodeId, plane, p));
        }
        boundary = boundary.DistinctBy(b => (b.MemberId, b.NodeId, b.BoundaryPlane)).OrderBy(b => b.MemberId, StringComparer.Ordinal).ThenBy(b => b.NodeId, StringComparer.Ordinal).ToList();
        var attachments = boundary.Select((b, i) => new LatticeAttachmentWitness($"bond:{i:D4}", b.MemberId, b.BoundaryPlane, b.Point, 2d * feature.StrutRadius, feature.HostId)).ToArray();
        var signatureText = string.Join("|", cells) + ";" + string.Join("|", nodes.Keys.Order()) + ";" + string.Join("|", members.Keys.Order());
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureText))).ToLowerInvariant();
        return new(feature, cells, nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal).ToArray(), members.Values.OrderBy(m => m.Id, StringComparer.Ordinal).ToArray(), boundary, attachments, signature, []);

        string Node(string role, int x, int y, int z, Point3D p)
        {
            var id = string.Create(CultureInfo.InvariantCulture, $"octet:{role}:{x}:{y}:{z}");
            nodes.TryAdd(id, new LatticeNodeInstance(id, p, role));
            return id;
        }
        void Member(string a, string b, string role)
        {
            var (start, end) = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
            var id = $"octet:member:{start}:{end}";
            members.TryAdd(id, new LatticeMemberInstance(id, start, end, nodes[start].Position, nodes[end].Position, role));
        }
    }

    private static string? BoundaryPlane(Point3D p, AxisAlignedBoxExtents b) =>
        System.Math.Abs(p.X - b.MinX) < 1e-9 ? "XMin" : System.Math.Abs(p.X - b.MaxX) < 1e-9 ? "XMax" :
        System.Math.Abs(p.Y - b.MinY) < 1e-9 ? "YMin" : System.Math.Abs(p.Y - b.MaxY) < 1e-9 ? "YMax" :
        System.Math.Abs(p.Z - b.MinZ) < 1e-9 ? "ZMin" : System.Math.Abs(p.Z - b.MaxZ) < 1e-9 ? "ZMax" : null;

    private static bool IntersectsZThroughHole(AxisAlignedBoxExtents region, Point3D center, double radius, double eps)
    {
        var dx = center.X < region.MinX ? region.MinX - center.X : center.X > region.MaxX ? center.X - region.MaxX : 0d;
        var dy = center.Y < region.MinY ? region.MinY - center.Y : center.Y > region.MaxY ? center.Y - region.MaxY : 0d;
        return dx * dx + dy * dy < (radius + eps) * (radius + eps);
    }
}
