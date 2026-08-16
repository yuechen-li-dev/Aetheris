namespace Aetheris.SheetMetal;

public sealed record SheetMetalConceptPath(
    string Path,
    string Kind,
    IReadOnlyList<string> Capabilities,
    string StableId,
    string? FormedId,
    string? FlatId,
    string Provenance);

/// <summary>Canonical authored Sheet Metal public surface; geometry regeneration does not name it.</summary>
public static class SheetMetalConceptPaths
{
    public static IReadOnlyList<SheetMetalConceptPath> Inspect(SheetMetalConstructionSpec spec,SheetMetalPartIr part,SheetMetalFlatPatternIr? flat=null)
    {
        ArgumentNullException.ThrowIfNull(spec);ArgumentNullException.ThrowIfNull(part);
        var paths=new List<SheetMetalConceptPath>();
        void Add(string path,string kind,string stableId,string? formed,string? flatId,string provenance,params string[] capabilities)=>paths.Add(new(path,kind,capabilities,stableId,formed,flatId,provenance));
        var baseId=part.BaseRegionId;
        Add(spec.Base.Name,"SheetRegion",spec.Base.Name,baseId,$"flat-{baseId}","canonical rectangular authored region","Cuttable","PlanarRegion","FlatCorrespondent");
        foreach(var edge in new[]{"Front","Right","Rear","Left"})Add($"{spec.Base.Name}.{edge}","SheetEdge",$"{spec.Base.Name}.{edge}",baseId,$"flat-{baseId}","canonical rectangular edge","FlangeAttachable","BendBoundary","CornerAdjacent");
        Add($"{spec.Base.Name}.Center","Point2",$"{spec.Base.Name}.Center",baseId,$"flat-{baseId}","canonical rectangular center","PointCapable","CutPlacement");
        foreach(var flange in spec.Flanges)
        {
            var region=part.Regions.FirstOrDefault(x=>x.StableId.Equals(flange.Name,StringComparison.OrdinalIgnoreCase)||x.StableId.EndsWith($"{flange.EdgeName.ToLowerInvariant()}-flange",StringComparison.OrdinalIgnoreCase));
            var bend=part.Bends.FirstOrDefault(x=>x.AdjacentRegionB==region?.StableId);
            Add(flange.Name,"SheetFlange",flange.Name,region?.StableId,$"flat-{region?.StableId}","authored flange declaration","SheetRegion","Cuttable","FlatCorrespondent");
            Add($"{flange.Name}.Root","SheetEdge",$"{flange.Name}.Root",bend?.StableId,$"flat-{bend?.StableId}","flange root tangent edge","BendBoundary","CornerAdjacent");
            Add($"{flange.Name}.Outer","SheetEdge",$"{flange.Name}.Outer",region?.StableId,$"flat-{region?.StableId}","canonical flange free edge","FlangeAttachable","FreeEdge","CornerAdjacent");
            Add($"{flange.Name}.Left","SheetEdge",$"{flange.Name}.Left",region?.StableId,$"flat-{region?.StableId}","orientation-relative flange side","FreeEdge","CornerAdjacent");
            Add($"{flange.Name}.Right","SheetEdge",$"{flange.Name}.Right",region?.StableId,$"flat-{region?.StableId}","orientation-relative flange side","FreeEdge","CornerAdjacent");
            Add($"{flange.Name}.LeftCorner","SheetCorner",$"{flange.Name}.LeftCorner",region?.StableId,$"flat-{region?.StableId}","canonical flange endpoint","CornerAdjacent");
            Add($"{flange.Name}.RightCorner","SheetCorner",$"{flange.Name}.RightCorner",region?.StableId,$"flat-{region?.StableId}","canonical flange endpoint","CornerAdjacent");
            Add($"{flange.Name}.Bend","SheetBend",$"{flange.Name}.Bend",bend?.StableId,$"flat-{bend?.StableId}","authored flange bend","BendBoundary","FlatCorrespondent");
        }
        foreach(var cut in spec.Cuts)
        {
            Add(cut.Name,"SheetCut",cut.Name,cut.Name,cut.Name,"authored cut declaration","FlatFeature","Cuttable");
            Add($"{cut.RegionName}.{cut.Name}","SheetCut",$"{cut.RegionName}.{cut.Name}",cut.Name,cut.Name,"semantic owning-region cut path","FlatFeature","Cuttable");
        }
        foreach(var datum in spec.SemanticLayout.Datums)
            Add(datum.Path,"SheetDatum",datum.Path,null,null,datum.Provenance,"PointCapable","StableSemanticIdentity");
        foreach(var attachment in spec.SemanticLayout.AttachmentPaths??[])
        {
            var ir=part.AttachmentPaths?.FirstOrDefault(x=>x.StableId.Equals(attachment.Path,StringComparison.Ordinal));
            Add(attachment.Path,"SheetAttachmentPath",attachment.Path,ir?.StableId,$"flat-{attachment.Path}","bounded region-owned path derived from a physical carrier","FlangeAttachable","BendAttachable","FeatureAttachable","StableSemanticIdentity");
        }
        foreach(var tab in spec.SemanticLayout.Tabs)
            Add(tab.Path,"SheetTab",tab.Path,tab.Region,$"flat-{tab.Region}","exact outer-edge contour extension","EdgeFeature","ManufacturingContour","StableSemanticIdentity");
        foreach(var notch in spec.SemanticLayout.SteppedNotches??[])
            Add(notch.Path,"SheetSteppedNotch",notch.Path,notch.Region,$"flat-{notch.Region}","semantic stepped edge removal","EdgeFeature","ManufacturingContour","StableSemanticIdentity");
        foreach(var corner in spec.SemanticLayout.Corners??[])
        {
            Add(corner.CornerPath,"SheetProfileCorner",corner.CornerPath,corner.Region,$"flat-{corner.Region}","shared semantic corner consuming two adjacent profile edges","CornerFeature","ManufacturingContour","StableSemanticIdentity");
            Add(corner.OperationPath,"SheetCorner"+corner.Operation,corner.OperationPath,corner.Region,$"flat-{corner.Region}","deterministic exact cross-edge corner lowering","CornerFeature","ManufacturingContour","StableSemanticIdentity");
        }
        foreach(var pattern in spec.SemanticLayout.Patterns)
            Add(pattern.Path,"SheetPattern",pattern.Path,null,null,"compile-time semantic feature pattern","EqualPitch","EqualSize","StableSemanticIdentity");
        foreach(var constraint in spec.SemanticLayout.Constraints)
            Add(constraint.Path,"SheetConstraint",constraint.Path,null,null,constraint.Detail,"ValidatedBeforeProfileLowering");
        Add("Flat","FlatPattern",$"flat-{part.StableId}",null,flat?.StableId,"derived flat state","ManufacturingContour");
        foreach(var path in paths.Where(x=>x.FormedId is not null&&x.Path!="Flat").ToArray())Add($"Flat.{path.Path}","Flat"+path.Kind,$"Flat.{path.StableId}",path.FormedId,path.FlatId,"formed/flat semantic correspondence",[..path.Capabilities,"FlatState"]);
        return paths.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> AvailableMembers(string conceptKind)=>conceptKind switch
    {
        "SheetRegion.Rectangle"=>["Front","Right","Rear","Left","Center"],
        "SheetFlange"=>["Root","Outer","Left","Right","LeftCorner","RightCorner","Bend"],
        _=>[]
    };
}
