using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>Every face of a composed body that its construction can name has a stable selector, and PMI reaches it by that name.</summary>
public sealed class ComposeFaceNamingTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }

    private static FirmamentStepExportResult Build(string source, string? directory = null)
    {
        var result = FirmamentBuildAndExport.CompileSource(source, directory);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Value;
    }

    private static FirmamentStepExportResult BuildFixture(string relative)
    {
        var path = Path.Combine(Root(), "fixtures", relative);
        return Build(File.ReadAllText(path), Path.GetDirectoryName(path));
    }

    /// <summary>The selector each face's owner carries, by face id.</summary>
    private static Dictionary<int, string?> Names(FirmamentStepExportResult result)
    {
        var correspondence = result.RuntimeCorrespondence!;
        return result.RuntimeBody!.Topology.Faces.ToDictionary(f => f.Id.Value,
            f => correspondence.Descendants.FirstOrDefault(d => d.Face == f.Id && !d.IsTopologyAlias)?.FirmamentSelector);
    }

    [Theory]
    [InlineData("Canonical/Features/Pocket/rectangular-pocket.firmament")]
    [InlineData("Canonical/Features/Boss/boss-pocket-block.firmament")]
    [InlineData("Canonical/Feature/pickup-functional.firmament")]
    [InlineData("Canonical/Hollow/enclosure-boss-pocket.firmament")]
    [InlineData("Canonical/Offset/prism-groove.firmament")]
    [InlineData("Canonical/PMI/counterbore-shaft-diameter.firmament")]
    [InlineData("Canonical/PMI/showcase-bearing-mount.firmament")]
    [InlineData("Regression/FrontendX4/cases/f01-xz-layout.firmament")]
    [InlineData("Regression/Hole/valid/path-step-plane-drills.firmament")]
    [InlineData("Regression/JunctionFillet/drilled-boss-foot.firmament")]
    [InlineData("Regression/Decompile/ctc-01.firmament")]
    public void EveryFaceOfAComposedBodyHasASelector(string fixture)
    {
        var names = Names(BuildFixture(fixture));
        Assert.Empty(names.Where(n => n.Value is null).Select(n => n.Key));
    }

    [Fact]
    public void ASideSplitBySectionLevelsKeepsOneNameAndCapsAreNamedByTheirOperation()
    {
        var names = Names(BuildFixture("Canonical/Features/Pocket/rectangular-pocket.firmament"));
        Assert.Equal(2, names.Values.Count(n => n == "face(Body.Side(\"profile:BaseProfile.Outer.Bottom\"))"));
        Assert.Superset(new HashSet<string?> { "face(Stock.Top)", "face(Stock.Bottom)", "face(Recess.Floor)",
            "face(Body.Side(\"profile:PocketProfile.Outer.Right\"))" }, names.Values.ToHashSet());
    }

    [Fact]
    public void HoleFacesAreNamedByTheHole()
    {
        var names = Names(BuildFixture("Canonical/PMI/counterbore-shaft-diameter.firmament")).Values.ToHashSet();
        Assert.Superset(new HashSet<string?> { "face(CounterboredMount.Wall)", "face(CounterboredMount.Counterbore)",
            "face(CounterboredMount.Shoulder)", "face(MountPattern_0.Wall)" }, names);
        var drills = Names(BuildFixture("Regression/Hole/valid/profile-edge-plane-drill.firmament")).Values.ToHashSet();
        Assert.Superset(new HashSet<string?> { "face(Drill.Wall)", "face(Drill.Tip)", "face(Body.Side(\"profile:Plate.Outer.Front\"))" }, drills);
    }

    [Fact]
    public void AChamferNamesTheFacesItCuts()
    {
        var names = Names(BuildFixture("Regression/FrontendX4/cases/d01-edgefinish-generic-variant.firmament")).Values.ToHashSet();
        Assert.Contains("face(TopEdge.Side(\"profile:Stock.Outer.Bottom\"))", names);
        Assert.Contains("face(Body.Side(\"profile:Stock.Outer.Bottom\"))", names);
    }

    [Fact]
    public void AHollowCavityIsTheInsideOfTheCapsItFollows()
    {
        var names = Names(BuildFixture("Canonical/Hollow/enclosure-boss-pocket.firmament")).Values.ToHashSet();
        Assert.Superset(new HashSet<string?> { "face(stock.TopInside)", "face(tower.TopInside)", "face(recess.FloorInside)" }, names);
    }

    [Fact]
    public void AJunctionFilletKeepsTheNamesOfTheFacesItRoundsBetweenAndNamesItsOwn()
    {
        var names = Names(BuildFixture("Regression/JunctionFillet/hex-boss-foot.firmament"));
        Assert.Equal(6, names.Values.Count(n => n == "face(HexFoot.Fillet)"));
        Assert.Superset(new HashSet<string?> { "face(Stock.Top)", "face(Hex.Top)", "face(Stock.Bottom)" }, names.Values.ToHashSet());
    }

    [Fact]
    public void PmiOnComposedFacesIsWrittenAgainstThoseFaces()
    {
        const string source = """
            Model Lid {
              units: mm
              Profile outline = Rect2 { center: [0mm, 0mm]; size: [100mm, 60mm] }
              Profile tower = Rect2 { center: [-25mm, 0mm]; size: [30mm, 30mm] }
              Compose<Hollow> body {
                wallThickness: 2mm
                openings: [Bottom]
                Base stock { profile: outline; from: 0mm; to: 20mm; role: Stock }
                Boss tower { on: Top; profile: tower; height: 12mm }
              }
              Pmi {
                Datum A { target: face(stock.Bottom) }
                Datum B { target: face(body.Side("profile:__Boundary2Value_outline__Boundary.Outer.Left")) }
                Distance Wall { between: [face(stock.Top), face(stock.TopInside)] value: 2mm tolerance: PlusMinus(0.1mm, 0.1mm) }
                Parallelism Ceiling { target: face(tower.TopInside) tolerance: 0.05mm datumRefs: [A] }
                Perpendicularity Right { target: face(body.Side("profile:__Boundary2Value_outline__Boundary.Outer.Right")) tolerance: 0.1mm datumRefs: [A] }
              }
            }
            """;
        var result = Build(source);
        var items = Step242SemanticPmiInspector.Inspect(result.StepText).Items;
        Assert.Equal(["A", "B"], items.Where(i => i.Kind == "Datum").Select(i => i.Name).Order());
        Assert.Equal(2, Assert.Single(items, i => i.Kind == "Dimension").Value);
        Assert.Equal(["Parallelism", "Perpendicularity"], items.Where(i => Step242GeometricToleranceKinds.Find(i.Kind) is not null).Select(i => i.Kind).Order());
        // A side the boss level splits is one plane in two faces; the record holds both.
        Assert.Equal(2, Assert.Single(items, i => i.Name == "B").GeometricFaceEntityIds.Count);
        Assert.Equal(1, Assert.Single(items, i => i.Name == "Ceiling").GeometricFaceEntityIds.Count);
    }

    [Fact]
    public void PmiSurvivesAJunctionFillet()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/JunctionFillet/hex-boss-foot.firmament")).Replace("    Modify Body {", """
                Pmi {
                    Datum A { target: face(Stock.Bottom) }
                    Distance Rise { between: [face(Stock.Bottom), face(Hex.Top)] value: 50mm tolerance: PlusMinus(0.2mm, 0.2mm) }
                    Parallelism TopFace { target: face(Hex.Top) tolerance: 0.05mm datumRefs: [A] }
                }
                Modify Body {
            """, StringComparison.Ordinal);
        var items = Step242SemanticPmiInspector.Inspect(Build(source).StepText).Items;
        Assert.Equal(["Datum", "Dimension", "Parallelism"], items.Select(i => i.Kind).Order());
        Assert.Equal(50, Assert.Single(items, i => i.Kind == "Dimension").Value);
    }
}
