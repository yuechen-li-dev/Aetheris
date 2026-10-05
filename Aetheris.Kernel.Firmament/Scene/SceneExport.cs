using System.Globalization;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Scene;

public static class SceneExport
{
    public static string Usd(CompiledScene scene)
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
        return AssemblyUsdExporter.SerializeSpatial(scene.Display,new(identities,scene.Appearances,properties,Cameras(scene)));
    }
    public static byte[] Glb(CompiledScene scene) => AssemblyGlbExporter.Serialize(scene.Display,occurrenceLooks:scene.Appearances,cameras:Cameras(scene));
    private static DisplayCamera[] Cameras(CompiledScene scene) => scene.Source.Cameras.Select(c => new DisplayCamera(c.Name,c.WorldTransform(),c.FovDegrees)).ToArray();
    private static string F(double v) => v.ToString("R",CultureInfo.InvariantCulture);
}
