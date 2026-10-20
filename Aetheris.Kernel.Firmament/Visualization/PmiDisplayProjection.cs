using System.Globalization;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Visualization;

/// <summary>Presentation only. Values and topology associations come from AP242 readback,
/// never from rounded display coordinates. Points are in Z-up millimetres.</summary>
public sealed record PmiDisplayAnnotation(string Id, string Kind, string Name, string Text,
    double? Value, double? TolerancePlus, double? ToleranceMinus, IReadOnlyList<int> StepFaceEntities,
    double[] Anchor, double[] LabelPosition, double TextHeight, IReadOnlyList<double[][]> Lines);

public static class PmiDisplayProjection
{
    public static IReadOnlyList<PmiDisplayAnnotation> FromStep(string step)
    {
        var inspection = Step242SemanticPmiInspector.Inspect(step);
        if (!inspection.Success) throw new InvalidOperationException("pmi-display-inspection-failed: " + string.Join("; ", inspection.Diagnostics));
        if (inspection.Items.Count == 0) return [];
        var imported = Step242Importer.ImportBody(step);
        if (!imported.IsSuccess) throw new InvalidOperationException("pmi-display-body-unavailable");
        var body = imported.Value;
        var tessellation = BrepDisplayTessellator.Tessellate(body, DisplayTessellationOptions.Default);
        if (!tessellation.IsSuccess) throw new InvalidOperationException("pmi-display-target-unavailable");
        var patches = tessellation.Value.FacePatches.ToDictionary(p => p.FaceId.Value);
        var sourceFaces = body.Bindings.FaceBindings.Where(b => b.SourceStepEntityId.HasValue)
            .ToDictionary(b => b.SourceStepEntityId!.Value, b => b.FaceId.Value);
        var points = patches.Values.SelectMany(p => p.Positions).ToArray();
        if (points.Length == 0) throw new InvalidOperationException("pmi-display-target-unavailable: No displayable faces are available for callouts.");
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y); var maxZ = points.Max(p => p.Z);
        var height = Math.Clamp(Math.Max(maxX - minX, maxY - minY) / 25, .5, 8);
        var result = new List<PmiDisplayAnnotation>();
        foreach (var item in inspection.Items)
        {
            if (item.Kind is not ("Datum" or "Diameter"))
                throw new InvalidOperationException($"pmi-display-kind-unsupported: {item.Kind} '{item.Name}' has no qualified 3D callout yet.");
            var targets = item.GeometricFaceEntityIds.Where(id => sourceFaces.TryGetValue(id, out var face)
                    && patches.TryGetValue(face, out var patch) && patch.Positions.Count > 0)
                .Select(id => patches[sourceFaces[id]]).ToArray();
            if (targets.Length == 0 || targets.Length != item.GeometricFaceEntityIds.Count)
                throw new InvalidOperationException($"pmi-display-target-unresolved: '{item.Name}' has no complete geometric face association.");
            var anchorPoint = targets[0].Positions[0];
            double[] anchor = [anchorPoint.X, anchorPoint.Y, anchorPoint.Z];
            double[] label = [maxX + height * 3, maxY - result.Count * height * 2.5, maxZ + height];
            string N(double value) => value.ToString("G9", CultureInfo.InvariantCulture);
            var text = item.Kind == "Datum" ? $"[{item.Name}]" : $"Ø{N(item.Value!.Value)}"
                + (item.TolerancePlus.HasValue && item.ToleranceMinus.HasValue ? $" +{N(item.TolerancePlus.Value)}/-{N(item.ToleranceMinus.Value)}" : "") + " mm";
            var outlines = PlanarTextProfiles.PresentationContours(text, height);
            var lines = new List<double[][]> { new[] { anchor, new[] { label[0] - height, label[1], label[2] }, label } };
            foreach (var curve in outlines)
            {
                double[] P((double X, double Y) p) => [label[0] + p.X, label[1] + p.Y, label[2]];
                lines.Add(curve switch
                {
                    LineArcLineSegment2D line => [P(line.Start), P(line.End)],
                    LineArcCubicBezier2D cubic => Enumerable.Range(0, 13).Select(i =>
                    {
                        var t = i / 12d; var s = 1 - t;
                        return P((s*s*s*cubic.Start.X + 3*s*s*t*cubic.Control1.X + 3*s*t*t*cubic.Control2.X + t*t*t*cubic.End.X,
                            s*s*s*cubic.Start.Y + 3*s*s*t*cubic.Control1.Y + 3*s*t*t*cubic.Control2.Y + t*t*t*cubic.End.Y));
                    }).ToArray(),
                    _ => throw new InvalidOperationException("pmi-display-font-curve-unsupported")
                });
            }
            result.Add(new("pmi:" + item.EntityId, item.Kind, item.AuthoredName ?? item.Name, text,
                item.Value, item.TolerancePlus, item.ToleranceMinus, item.GeometricFaceEntityIds, anchor, label, height, lines));
        }
        return result;
    }
}
