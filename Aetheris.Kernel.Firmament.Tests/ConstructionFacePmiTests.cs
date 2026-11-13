using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>PMI on native parts targets faces by the selectors inspection reports for them.</summary>
public sealed class ConstructionFacePmiTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
    private static string Fixture() => File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/PMI/extruded-bracket-faces.firmament"));

    [Fact]
    public void ExtrudedProfileFacesTakeDatumsSizesAndControls()
    {
        var result = FirmamentBuildAndExport.CompileSource(Fixture());
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var items = Step242SemanticPmiInspector.Inspect(result.Value.StepText).Items;
        Assert.Equal(["A", "B"], items.Where(i => i.Kind == "Datum").Select(i => i.Name).Order());
        Assert.Equal(new double?[] { 3, 20, 30 }, items.Where(i => i.Kind == "Dimension").Select(i => i.Value).Order());
        Assert.Equal(["Flatness", "Parallelism", "Perpendicularity"], items.Where(i => Step242GeometricToleranceKinds.Find(i.Kind) is not null).Select(i => i.Kind).Order());
        // Every requirement is attached to the faces its selectors name: one face each, two for a distance.
        foreach (var item in items.Where(i => i.Kind != "Annotation"))
            Assert.Equal(item.Kind == "Dimension" ? 2 : 1, item.GeometricFaceEntityIds.Count);
        // Inspection names the same faces the record used.
        var selectors = result.Value.RuntimeCorrespondence!.Descendants.Select(d => d.FirmamentSelector).OfType<string>().ToHashSet();
        Assert.Contains("face(bracket.Top)", selectors);
        Assert.Contains("face(bracket.Side(\"profile:section.Outer.Vertex5_Vertex0\"))", selectors);
    }

    [Fact]
    public void AFaceThePartDoesNotHaveIsRefusedByName()
    {
        var source = Fixture().Replace("Vertex3_Vertex4", "Vertex8_Vertex9", StringComparison.Ordinal);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("'Leg'", StringComparison.Ordinal) && d.Message.Contains("requires proved face associations", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownOwnerIsRefusedWhenParsed()
    {
        var source = Fixture().Replace("face(bracket.Top)", "face(plate.Top)", StringComparison.Ordinal);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("firmament-v2-pmi-target-unresolved", StringComparison.Ordinal) && d.Message.Contains("plate", StringComparison.Ordinal));
    }

    [Fact]
    public void BoxWallsBesideAHoleAreNamedByTheirAxis()
    {
        const string source = "Model Plate { units: mm Box stock { size: [40mm, 30mm, 8mm] } Modify stock { Hole<Shaft> mount { on: +Z center: Point2(0mm, 0mm) diameter: 8mm end: ThroughAll } }\n"
            + " Pmi { Datum A { target: face(-Z) } Datum B { target: face(-X) } Parallelism Side { target: face(+X) tolerance: 0.05mm datumRefs: [B] } } }";
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var selectors = result.Value.RuntimeCorrespondence!.Descendants.Where(d => d.Face is not null).Select(d => d.FirmamentSelector).OfType<string>().ToHashSet();
        Assert.Superset(new HashSet<string> { "face(-X)", "face(+X)", "face(-Y)", "face(+Y)", "face(-Z)", "face(+Z)", "face(mount.Wall)" }, selectors);
        Assert.Equal(3, Step242SemanticPmiInspector.Inspect(result.Value.StepText).Items.Count(i => i.Kind is "Datum" or "Parallelism"));
    }
}
