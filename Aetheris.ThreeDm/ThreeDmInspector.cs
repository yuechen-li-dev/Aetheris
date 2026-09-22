using System.Diagnostics;
using Rhino;
using Rhino.FileIO;
using Rhino.Geometry;

namespace Aetheris.ThreeDm;

public sealed record ThreeDmPoint(double X, double Y, double Z);
public sealed record ThreeDmBounds(ThreeDmPoint Min, ThreeDmPoint Max);
public sealed record ThreeDmObjectInventory(
    int Index, Guid SourceId, int LayerIndex, string GeometryType, string Classification,
    bool IsSolid, bool IsManifold, int Faces, int Loops, int Trims, int Edges, int Vertices,
    ThreeDmBounds? BoundsMillimetres);
public sealed record ThreeDmInventory(
    int ArchiveVersion, long FileSizeBytes, string SourceUnit, double? MillimetresPerSourceUnit,
    double SourceAbsoluteTolerance, double? AbsoluteToleranceMillimetres, double SourceAngleToleranceDegrees,
    int LayerCount, int InstanceDefinitionCount, int ObjectCount,
    IReadOnlyDictionary<string, int> ObjectTypes, int BrepCount, int ExtrusionCount, int MeshCount,
    int SubDCount, int CurveCount, int SurfaceCount, int UnsupportedObjectCount,
    int FaceCount, int LoopCount, int TrimCount, int EdgeCount, int VertexCount,
    int SeamTrimCount, int SingularTrimCount, int RationalEdgeCount, int RationalNoncircularEdgeCount,
    int RationalArcEdgeCount, int RationalEllipticEdgeCount, int RationalGenericEdgeCount,
    int RationalSurfaceCount, int RationalTrimCount, ThreeDmBounds? BoundsMillimetres,
    IReadOnlyList<ThreeDmObjectInventory> Objects, IReadOnlyList<string> Diagnostics,
    double ParseMilliseconds, double InventoryMilliseconds);

/// <summary>Read-only OpenNURBS inventory. Does not claim a BRep translation.</summary>
public static class ThreeDmInspector
{
    public static ThreeDmInventory Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var fileSize = new FileInfo(fullPath).Length;
        var timer = Stopwatch.StartNew();
        using var file = File3dm.Read(fullPath) ?? throw new InvalidDataException("OpenNURBS could not read the 3DM file.");
        var parseMilliseconds = timer.Elapsed.TotalMilliseconds;
        var settings = file.Settings;
        var factor = MillimetresPerSourceUnit(settings.ModelUnitSystem);
        var diagnostics = new List<string>();
        if (factor is null)
            diagnostics.Add($"UnitConversionIssue: model unit {settings.ModelUnitSystem} has no qualified mm conversion.");
        var objects = new List<ThreeDmObjectInventory>();
        var types = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var faceCount = 0; var loopCount = 0; var trimCount = 0; var edgeCount = 0; var vertexCount = 0;
        var seamCount = 0; var singularCount = 0; var rationalEdgeCount = 0; var rationalNoncircularEdgeCount = 0;
        var rationalArcEdgeCount = 0; var rationalEllipticEdgeCount = 0; var rationalGenericEdgeCount = 0;
        var rationalSurfaceCount = 0; var rationalTrimCount = 0;
        var brepCount = 0; var extrusionCount = 0; var meshCount = 0; var subDCount = 0;
        var curveCount = 0; var surfaceCount = 0; var unsupportedCount = 0;
        BoundingBox? overall = null;
        var index = 0;
        foreach (var item in file.Objects)
        {
            var geometry = item.Geometry;
            var type = geometry.GetType().Name;
            types[type] = types.GetValueOrDefault(type) + 1;
            if (geometry is not Brep brep)
            {
                switch (geometry)
                {
                    case Extrusion: extrusionCount++; break;
                    case Mesh: meshCount++; break;
                    case SubD: subDCount++; break;
                    case Curve: curveCount++; break;
                    case Surface: surfaceCount++; break;
                    default: unsupportedCount++; break;
                }
                diagnostics.Add($"Unsupported3dmObjectType: object {index} ({type}) is outside the X0 BRep bridge.");
                objects.Add(new ThreeDmObjectInventory(index++, item.Attributes.ObjectId, item.Attributes.LayerIndex,
                    type, "non-BRep", false, false, 0, 0, 0, 0, 0, null));
                continue;
            }
            brepCount++;
            faceCount += brep.Faces.Count; loopCount += brep.Loops.Count; trimCount += brep.Trims.Count;
            edgeCount += brep.Edges.Count; vertexCount += brep.Vertices.Count;
            foreach (var edge in brep.Edges)
            {
                if (!edge.EdgeCurve.ToNurbsCurve().IsRational) continue;
                rationalEdgeCount++;
                if (!edge.EdgeCurve.TryGetCircle(out _))
                {
                    rationalNoncircularEdgeCount++;
                    if (edge.EdgeCurve.TryGetArc(out _)) rationalArcEdgeCount++;
                    else if (edge.EdgeCurve.TryGetEllipse(out _)) rationalEllipticEdgeCount++;
                    else
                    {
                        rationalGenericEdgeCount++;
                        diagnostics.Add($"Unsupported3dmCurveType: object {index}, edge {edge.EdgeIndex} is a generic rational curve; Aetheris has no generic rational 3D curve carrier.");
                    }
                }
            }
            foreach (var surface in brep.Surfaces)
                if (surface.ToNurbsSurface().IsRational) rationalSurfaceCount++;
            foreach (var trim in brep.Trims)
            {
                if (trim.TrimType == BrepTrimType.Seam) seamCount++;
                if (trim.TrimType == BrepTrimType.Singular) singularCount++;
                if (trim.TrimCurve.ToNurbsCurve().IsRational) rationalTrimCount++;
            }
            var box = ThreeDmBoundsSampler.Sample(brep);
            if (overall is null) overall = box;
            else { var merged = overall.Value; merged.Union(box); overall = merged; }
            objects.Add(new ThreeDmObjectInventory(index++, item.Attributes.ObjectId, item.Attributes.LayerIndex,
                type, "exact-but-not-yet-mapped", brep.IsSolid, brep.IsManifold,
                brep.Faces.Count, brep.Loops.Count, brep.Trims.Count, brep.Edges.Count, brep.Vertices.Count,
                factor is double scale ? ConvertBounds(box, scale) : null));
        }
        return new ThreeDmInventory(file.ArchiveVersion, fileSize, settings.ModelUnitSystem.ToString(), factor,
            settings.ModelAbsoluteTolerance, factor * settings.ModelAbsoluteTolerance,
            settings.ModelAngleToleranceDegrees, file.AllLayers.Count, file.AllInstanceDefinitions.Count,
            index, types, brepCount, extrusionCount, meshCount, subDCount, curveCount, surfaceCount,
            unsupportedCount, faceCount, loopCount, trimCount, edgeCount, vertexCount, seamCount, singularCount,
            rationalEdgeCount, rationalNoncircularEdgeCount, rationalArcEdgeCount,
            rationalEllipticEdgeCount, rationalGenericEdgeCount,
            rationalSurfaceCount, rationalTrimCount,
            overall is BoundingBox bounds && factor is double f ? ConvertBounds(bounds, f) : null,
            objects, diagnostics, parseMilliseconds, timer.Elapsed.TotalMilliseconds - parseMilliseconds);
    }

    private static ThreeDmBounds ConvertBounds(BoundingBox bounds, double factor) => new(
        new(bounds.Min.X * factor, bounds.Min.Y * factor, bounds.Min.Z * factor),
        new(bounds.Max.X * factor, bounds.Max.Y * factor, bounds.Max.Z * factor));

    private static double? MillimetresPerSourceUnit(UnitSystem unit) => unit switch
    {
        UnitSystem.Millimeters => 1,
        UnitSystem.Centimeters => 10,
        UnitSystem.Meters => 1000,
        UnitSystem.Microns => 0.001,
        UnitSystem.Inches => 25.4,
        UnitSystem.Feet => 304.8,
        _ => null
    };
}
