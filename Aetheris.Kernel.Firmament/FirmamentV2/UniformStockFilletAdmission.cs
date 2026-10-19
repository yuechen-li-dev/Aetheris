namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Shared AST admission for one whole-stock finish. No arbitrary-body promise.</summary>
internal static class UniformStockFilletAdmission
{
    internal const string Code = "firmament-v2-uniform-stock-fillet-unsupported";
    internal static string? Error(IReadOnlyList<FirmamentV2SolidBinding> solids, IReadOnlyList<FirmamentV2ModifyBlock> modifies, bool hasOtherEdits = false)
    {
        var finishes = modifies.SelectMany(m => m.EdgeFinishes ?? []).ToArray();
        if (!finishes.Any(f => f.Target == "AllEdges")) return null;
        if (solids.Count != 1 || solids[0].Primitive is not (FirmamentV2BoxRecord or FirmamentV2PolyhedronRecord)
            || solids[0].ConstructionPolicy != FirmamentV2ConstructionPolicy.Solid)
            return "target: AllEdges currently admits only an untouched Box or regular Polyhedron<4|6|8|12|20>. Rounding all edges on arbitrary bodies, imported bodies, RoundedBox or hollow stock is unsupported. Use an admitted local EdgeFinish selection where available.";
        if (hasOtherEdits || modifies.Count != 1 || modifies[0].TargetSolid != solids[0].Name || finishes.Length != 1
            || modifies[0].Regions.Count != 0 || modifies[0].SemanticHoles.Count != 0 || modifies[0].Perforations is { Count: > 0 })
            return "target: AllEdges requires one finish on untouched stock. Combining whole-stock rounding with holes, other finishes or body edits is currently unsupported.";
        var finish = finishes[0];
        if (finish.Kind != "Fillet" || finish.FaceAxis.Length != 0)
            return "target: AllEdges uses EdgeFinish<Fillet> with radius and no face selector. Whole-stock chamfer is not currently admitted.";
        if (!double.IsFinite(finish.Distance) || finish.Distance <= 0)
            return "target: AllEdges requires a finite positive fillet radius.";
        return null;
    }
}
