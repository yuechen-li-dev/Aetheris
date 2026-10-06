using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Product-neutral display state over already compiled geometry and resolved appearance.</summary>
public static class DisplayProjection
{
    public static string FallbackReason(FirmamentCirRetention? cir) => cir?.Qualification == "cir-qualified"
        ? "shader-artifact-not-bound" : cir?.FallbackReason ?? "no-field-representation";
    public static ResolvedDisplayMaterial? Material(AssemblyAppearanceBinding? appearance) => appearance is null ? null :
        new([appearance.Preview.Red, appearance.Preview.Green, appearance.Preview.Blue], appearance.Preview.Roughness,
            appearance.Preview.Metallic, appearance.Preview.Opacity,
            [appearance.Preview.EmissiveRed, appearance.Preview.EmissiveGreen, appearance.Preview.EmissiveBlue]);

    public static AssemblyDisplayMeshDocument Project(CompiledScene scene, IReadOnlyCollection<string>? hiddenBoundaries = null)
    {
        var display = SceneExport.Project(scene, hiddenBoundaries);
        var nodes = scene.Nodes.ToDictionary(n => n.Path, StringComparer.Ordinal);
        return display with
        {
            Definitions = display.Definitions.Select(d => d with { Shader = d.Shader ?? CirShaderArtifactProvider.Resolve(d.Cir) }).ToArray(),
            Occurrences = display.Occurrences.Select(o => o with
            {
                Material = Material(scene.Appearances.GetValueOrDefault(o.Id)) ?? o.Material,
                Kind = nodes.GetValueOrDefault(o.Path)?.Kind ?? o.Kind
            }).ToArray(),
            Cameras = scene.Source.Cameras.Select(c => new DisplayCamera(c.Name, c.WorldTransform(), c.FovDegrees) { LookAtMm = c.LookAtMm.ToArray() }).ToArray(),
            MinimumMm = scene.MinimumMm.ToArray(), MaximumMm = scene.MaximumMm.ToArray(), Boundaries = scene.Boundaries
        };
    }
}
