using System.Globalization;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Scene;

public static class SceneExport
{
    /// <summary>A presentation projection over owned Room boundaries. Geometry and
    /// authored room validity remain unchanged; unknown boundaries fail closed.</summary>
    public static AssemblyDisplayMeshDocument Project(CompiledScene scene,IReadOnlyCollection<string>? hiddenBoundaries = null)
    {
        if (hiddenBoundaries is null || hiddenBoundaries.Count == 0) return scene.Display;
        var known=scene.Boundaries.Select(b => b.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var boundary in hiddenBoundaries)
            if (!known.Contains(boundary)) throw new ArgumentException($"scene-presentation-boundary-unknown: '{boundary}'.");
        var prefixes=hiddenBoundaries.Select(b => scene.Source.Name+"."+b+".").ToArray();
        return scene.Display with { Occurrences=scene.Display.Occurrences.Select(o =>
            prefixes.Any(p => o.Path.StartsWith(p,StringComparison.Ordinal)) ? o with { DefinitionId=null } : o).ToArray() };
    }

    public static string Usd(CompiledScene scene,IReadOnlyCollection<string>? hiddenBoundaries = null)
    {
        var ids=scene.Display.Occurrences.ToDictionary(o => o.Path,o => o.Id,StringComparer.Ordinal);
        var identities=scene.Nodes.ToDictionary(n => ids[n.Path],n => n.DefinitionIdentity,StringComparer.Ordinal);
        var properties=scene.Nodes.ToDictionary(n => ids[n.Path],n => (IReadOnlyDictionary<string,string>)new Dictionary<string,string> {
            ["kind"]=n.Kind,["sourceDocument"]=n.Span.Source,["sourceStart"]=n.Span.Start.ToString(CultureInfo.InvariantCulture),
            ["sourceLength"]=n.Span.Length.ToString(CultureInfo.InvariantCulture),["patternKey"]=n.PatternKey ?? "",["placementAuthority"]=n.PlacementAuthority },StringComparer.Ordinal);
        foreach (var o in scene.Source.Openings)
        {
            var id=ids[scene.Source.Name+"."+o.Boundary+"."+o.Name];
            properties[id]=new Dictionary<string,string>(properties[id]) { ["widthMm"]=F(o.WidthMm),["heightMm"]=F(o.HeightMm),["alongMm"]=F(o.AlongMm),["sillMm"]=F(o.SillMm),["boundary"]=o.Boundary };
        }
        return AssemblyUsdExporter.SerializeSpatial(Project(scene,hiddenBoundaries),new(identities,scene.Appearances,properties,Cameras(scene)));
    }
    public static byte[] Glb(CompiledScene scene,IReadOnlyCollection<string>? hiddenBoundaries = null) =>
        AssemblyGlbExporter.Serialize(Project(scene,hiddenBoundaries),occurrenceLooks:scene.Appearances,cameras:Cameras(scene));
    private static DisplayCamera[] Cameras(CompiledScene scene) => scene.Source.Cameras.Select(c => new DisplayCamera(c.Name,c.WorldTransform(),c.FovDegrees)).ToArray();
    private static string F(double v) => v.ToString("R",CultureInfo.InvariantCulture);
}
