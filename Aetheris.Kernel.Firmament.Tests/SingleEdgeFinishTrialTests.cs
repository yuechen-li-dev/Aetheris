using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>
/// A picked-edge finish is offered only when a trial build of it succeeds, so the editor's offer and the
/// compiler's build cannot disagree; and an offered finish builds anywhere inside the size range shown.
/// </summary>
public sealed class SingleEdgeFinishTrialTests
{
    private static string Bracket
    {
        get
        {
            var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament");
            return source[..source.IndexOf("  Modify", StringComparison.Ordinal)] + "}";
        }
    }

    private static (string Source, string? Directory, SemanticTopologyCorrespondence Correspondence) Built(string source, string? directory = null)
    {
        var build = FirmamentBuildAndExport.CompileSource(source, directory);
        Assert.True(build.IsSuccess, string.Join(";", build.Diagnostics.Select(d => d.Message)));
        return (source, directory, build.Value.RuntimeCorrespondence!);
    }

    [Fact]
    public void Insert_WritesTheFinishTheEditorWrites()
    {
        var (source, _, correspondence) = Built(Bracket);
        var selection = SingleEdgeFinishSelection.Index(correspondence).Values.First(s => s.Face == "Longitudinal");
        var (edited, error) = SingleEdgeFinishTrial.Insert(source, selection, "Fillet", 1.5);
        Assert.Null(error);
        Assert.Contains($"EdgeFinish EdgeFillet {{ Face: Longitudinal Target: {selection.Target} Kind: Fillet Radius: 1.5mm }}", edited);
        Assert.Contains($"Modify {selection.Body}", edited);
    }

    [Fact]
    public void Bracket_OffersFilletsAlongTheExtrusion_AndChamfersOnItsEnds()
    {
        var (source, directory, correspondence) = Built(Bracket);
        var fillets = SingleEdgeFinishTrial.Admit(source, directory, null, correspondence, "Fillet");
        var chamfers = SingleEdgeFinishTrial.Admit(source, directory, null, correspondence, "Chamfer");
        Assert.All(fillets.Where(a => a.Face == "Longitudinal"), a => Assert.True(a.Admitted, a.Reason));
        // An end-face edge takes a round along its whole length where both its corners are square and
        // convex; the others say which corner stops it.
        Assert.All(fillets.Where(a => a.Face is "Top" or "Bottom"), a =>
            Assert.True(a.Admitted || a.Code == "ProfileBoundaryFilletFullLengthCornerUnsupported", $"{a.Target}: {a.Reason}"));
        Assert.Contains(fillets, a => a.Face is "Top" or "Bottom" && a.Admitted);
        Assert.All(chamfers, a => Assert.True(a.Admitted, a.Reason));
        Assert.All(fillets.Concat(chamfers), a => Assert.NotEmpty(a.EdgeIds));
    }

    [Fact]
    public void ARefusal_CarriesTheCompilersCode_AndAPlainReason()
    {
        Assert.Equal("ProfileRimFinishHistoryUnsupported",
            SingleEdgeFinishTrial.Code("ProfileRimFinishHistoryUnsupported: This rim plan preserves one constant-stock Base."));
        Assert.Equal("ProfileBoundaryChamferComposeOpenChainUnsupported",
            SingleEdgeFinishTrial.Code("ProfileBoundaryChamferComposeOpenChainUnsupported"));
        Assert.Equal("EdgeFinishRefused", SingleEdgeFinishTrial.Code("The model did not build."));
        Assert.Equal("Chamfers on curved edges are not supported yet.",
            SingleEdgeFinishTrial.Explain("ProfileBoundaryChamferArcMaterializationNotImplemented", "ProfileBoundaryChamferArcMaterializationNotImplemented:station=A", "Chamfer"));
        Assert.StartsWith("A fillet here breaks one of this model's own Assert checks.",
            SingleEdgeFinishTrial.Explain("firmament-v2-assert-volume-failed", "firmament-v2-assert-volume-failed: target=Base", "Fillet"));
    }

    /// <summary>The plate is a rhombus: no corner is square, so an open chamfer run ends on slanted neighbours.</summary>
    [Theory]
    [InlineData("Bottom")]
    [InlineData("Top")]
    public void ChamferOnASlantedCorner_KeepsTheNeighbourSidePlanar(string face)
    {
        var plate = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/Set/named-point-access.firmament");
        var (source, _, correspondence) = Built(plate);
        var selection = SingleEdgeFinishSelection.Index(correspondence).Values
            .First(s => s.Face == face && s.Target == "Boundary.Outer.SE");
        var (edited, error) = SingleEdgeFinishTrial.Insert(source, selection, "Chamfer", 0.5);
        Assert.Null(error);
        var build = FirmamentBuildAndExport.CompileSource(edited!);
        Assert.True(build.IsSuccess, string.Join(";", build.Diagnostics.Select(d => d.Message)));
        // 11520 mm³ less a 0.5 mm chamfer prism along the 55.2 mm edge, its ends cut by the neighbours' planes.
        var expected = 90d * 64d / 2d * 4d - 0.5 * 0.5 * 0.5 * Math.Sqrt(45d * 45d + 32d * 32d);
        var mass = BrepMassProperties.Evaluate(build.Value.RuntimeBody!);
        Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
        Assert.Equal(expected, mass.AbsoluteVolume, 6);
        var imported = Step242Importer.ImportBody(build.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(";", imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expected, BrepMassProperties.Evaluate(imported.Value).AbsoluteVolume, 6);
    }

    /// <summary>The foot's end edge has square corners at both ends: its round runs the whole edge.</summary>
    [Theory]
    [InlineData("Top")]
    [InlineData("Bottom")]
    public void FilletOnOneEndFaceEdge_RunsItsWholeLength(string face)
    {
        var body = BuildBracketFillet(face, "section.Outer.Vertex1_Vertex2", "Radius: 1mm");
        // 4020 mm³ less (1 - π/4) r² along the 3 mm edge; the neighbours keep one planar face each.
        AssertVolume(4020d - (1d - Math.PI / 4d) * 3d, body);
        Assert.Equal(9, body.Topology.Faces.Count());
    }

    [Theory]
    [InlineData("Top")]
    [InlineData("Bottom")]
    /// <summary>
    /// The end walls once faced into the material: the STEP reread with a different volume and other readers
    /// rejected the faces. Rereading the export at the native volume is the check that catches it.
    /// </summary>
    public void FilletStoppingShortOfItsCorners_HasEndWallsFacingTheGroove(string face)
    {
        var body = BuildBracketFillet(face, "section.Outer.Vertex1_Vertex2", "Radius: 1mm EndClearance: 0.5mm");
        AssertVolume(4020d - (1d - Math.PI / 4d) * 2d, body);
    }

    [Fact]
    public void FilletOnAnEdgeRunningIntoAnInsideCorner_SaysSo()
    {
        var (source, _, correspondence) = Built(Bracket);
        var admission = SingleEdgeFinishTrial.Admit(source, null, null, correspondence, "Fillet")
            .Single(a => a.Face == "Top" && a.Target == "section.Outer.Vertex2_Vertex3");
        Assert.False(admission.Admitted);
        Assert.Equal("ProfileBoundaryFilletFullLengthCornerUnsupported", admission.Code);
        Assert.Contains("inside corner", SingleEdgeFinishTrial.Explain(admission.Code!, admission.Reason!, "Fillet"));
    }

    /// <summary>Curved bodies' volume is integrated; it must hold the closed form within its own error bound.</summary>
    private static void AssertVolume(double expected, BrepBody body)
    {
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
        var bound = Math.Max(1e-6, mass.ErrorBound ?? 0d);
        Assert.True(Math.Abs(mass.AbsoluteVolume - expected) <= bound, $"volume {mass.AbsoluteVolume} against {expected} ± {bound} ({mass.EvaluationMethod})");
    }

    private static BrepBody BuildBracketFillet(string face, string target, string size)
    {
        var source = Bracket[..Bracket.LastIndexOf('}')] + $"Modify bracket {{ EdgeFinish Round {{ Face: {face} Target: {target} Kind: Fillet {size} }} }} }}";
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join(";", build.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(build.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(";", imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(BrepMassProperties.Evaluate(build.Value.RuntimeBody!).AbsoluteVolume, BrepMassProperties.Evaluate(imported.Value).AbsoluteVolume, 6);
        return build.Value.RuntimeBody!;
    }

    [Fact]
    public void ConceptBoundedBox_TakesAFilletRadius()
    {
        const string path = "fixtures/Canonical/Feature/feature-pattern.firmament";
        var (source, directory, correspondence) = Built(FirmamentCorpusHarness.ReadFixtureText(path), Path.GetDirectoryName(FirmamentCorpusHarness.ResolveFixtureFullPath(path)));
        var admissions = SingleEdgeFinishTrial.Admit(source, directory, null, correspondence, "Fillet");
        Assert.NotEmpty(admissions);
        Assert.All(admissions, a => Assert.True(a.Admitted, a.Reason));
    }

    public static TheoryData<string> ContractFixtures => new()
    {
        "fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament",
        "fixtures/Canonical/Set/named-point-access.firmament",
        "fixtures/Canonical/Feature/feature-pattern.firmament",
        "fixtures/Regression/OrientedComposeHoles/simple-bracket-through-hole.firmament",
    };

    /// <summary>The offer contract: every admitted finish builds at sizes spread across the range it reports.</summary>
    [Theory]
    [MemberData(nameof(ContractFixtures))]
    [Trait("Category", "SlowCorpus")]
    public void EveryOfferedFinish_BuildsInsideItsRange(string path)
    {
        var (source, directory, correspondence) = Built(FirmamentCorpusHarness.ReadFixtureText(path), Path.GetDirectoryName(FirmamentCorpusHarness.ResolveFixtureFullPath(path)));
        var failures = new List<string>();
        foreach (var kind in SingleEdgeFinishTrial.Kinds)
        foreach (var admission in SingleEdgeFinishTrial.Admit(source, directory, null, correspondence, kind).Where(a => a.Admitted))
        {
            var selection = new SingleEdgeFinishSelection(admission.Body, admission.Face, admission.Target, admission.Label);
            var range = SingleEdgeFinishTrial.Range(source, directory, null, selection, kind, admission.AdmittedSize!.Value, resolutionMm: 0.05);
            if (range is null) { failures.Add($"{kind} {admission.Face}/{admission.Target}: no range"); continue; }
            foreach (var fraction in new[] { 0.25, 0.5, 0.75 })
            {
                var size = range.MinimumMm + (range.MaximumMm - range.MinimumMm) * fraction;
                if (SingleEdgeFinishTrial.Try(source, directory, null, selection, kind, size) is { } error)
                    failures.Add($"{kind} {admission.Face}/{admission.Target} at {size:0.###} mm in [{range.MinimumMm}, {range.MaximumMm}]: {error}");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
