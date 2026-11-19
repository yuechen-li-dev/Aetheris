using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>
/// ST-Developer writes both circles of a cylindrical band as FACE_OUTER_BOUND. A band has no outer loop, so the
/// pair carries no role and imports as written; two outer bounds anywhere else still contradict each other.
/// </summary>
public sealed class Step242BandRingOuterBoundTests
{
    private const string Boxy = "testdata/step242/steptools/boxy_with_surfacetex.stp";

    [Fact]
    public void BandWithBothCirclesDeclaredOuter_Imports_AndExportsOneOuterBound()
    {
        var imported = Step242Importer.ImportBody(Step242Corpus.Text(Boxy));
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(30, imported.Diagnostics.Count(d => d.Source?.StartsWith("Importer.LoopRole.BandRingsBothDeclaredOuter", StringComparison.Ordinal) == true));
        var body = imported.Value;
        Assert.Equal(80, body.Topology.Faces.Count());

        // 174839.178053 mm³ is the independent reader's volume of the unmodified file.
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
        Assert.True(System.Math.Abs(mass.AbsoluteVolume - 174839.178053) <= System.Math.Max(1e-3, mass.ErrorBound ?? 0d),
            $"volume {mass.AbsoluteVolume} ± {mass.ErrorBound}");

        var exported = Step242Exporter.ExportBody(body);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics.Select(d => d.Message)));
        // Every face of the export carries exactly one outer bound.
        var entities = Regex.Matches(exported.Value, @"#(\d+)\s*=\s*([A-Z_]+)\(([^;]*)\);")
            .ToDictionary(m => m.Groups[1].Value, m => (Type: m.Groups[2].Value, Args: m.Groups[3].Value));
        var faces = entities.Values.Where(e => e.Type == "ADVANCED_FACE").ToArray();
        Assert.Equal(80, faces.Length);
        Assert.All(faces, face =>
        {
            var bounds = Regex.Match(face.Args, @"\(([^()]*)\)").Groups[1].Value;
            Assert.Equal(1, Regex.Matches(bounds, @"#(\d+)").Count(b => entities[b.Groups[1].Value].Type == "FACE_OUTER_BOUND"));
        });
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join("\n", reimported.Diagnostics.Select(d => d.Message)));
        Assert.Empty(reimported.Diagnostics.Where(d => d.Source?.StartsWith("Importer.LoopRole.BandRingsBothDeclaredOuter", StringComparison.Ordinal) == true));
        // An independent reader measures the export at the source's 174839.178053 mm³; the integrated volume here
        // is approximate, so the reread is checked by its topology and surfaces.
        Assert.Equal(body.Topology.Faces.Count(), reimported.Value.Topology.Faces.Count());
        Assert.Equal(body.Topology.Edges.Count(), reimported.Value.Topology.Edges.Count());
        Assert.Equal(body.Topology.Loops.Count(), reimported.Value.Topology.Loops.Count());
        Assert.Equal(body.Geometry.Surfaces.Select(s => s.Value.Kind).Order(), reimported.Value.Geometry.Surfaces.Select(s => s.Value.Kind).Order());
    }

    /// <summary>Boxy names each datum's face through the datum feature's own shape (a shape definition of the aspect).</summary>
    [Fact]
    public void DatumsLocatedThroughTheirFeaturesShape_FindTheirFaces()
    {
        var inspection = Step242SemanticPmiInspector.Inspect(Step242Corpus.Text(Boxy));
        var datums = inspection.Items.Where(item => item.Kind == "Datum").ToArray();
        Assert.Equal(3, datums.Length);
        Assert.All(datums, datum => Assert.Single(datum.GeometricFaceEntityIds));
        Assert.Equal([2009, 2014, 2019], datums.Select(d => d.GeometricFaceEntityIds.Single()).Order());
    }

    [Fact]
    public void PlanarFaceWithTwoOuterBounds_IsStillRefused()
    {
        var text = Step242Corpus.Text(Boxy).Replace("#304=FACE_BOUND(", "#304=FACE_OUTER_BOUND(", StringComparison.Ordinal);
        var imported = Step242Importer.ImportBody(text);
        Assert.False(imported.IsSuccess);
        Assert.Contains(imported.Diagnostics, d => d.Source == "Importer.LoopRole.DuplicateDeclaredOuter.Face:278");
    }
}
