using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2InlineStepTests
{
    private const string FixturePath = "fixtures/InlineStep/valid/inline-step-v2-canonical-box-reexport-step-verified.valid.firmfixture";
    private const string InputStepPath = "fixtures/InlineStep/testdata/canonical-box-10x8x6.step";

    [Fact]
    public void InlineStep_CanonicalBox_ReexportsAndRoundTripsThroughAp242()
    {
        var repo = FindRepoRoot();
        var outputPath = Path.Combine(Path.GetTempPath(), $"aetheris-inline-step-x1-{Guid.NewGuid():N}.step");
        try
        {
            var result = FirmamentBuildAndExport.Run(Path.Combine(repo, FixturePath), outputPath);

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.True(File.Exists(outputPath));
            Assert.Equal("inline-step", result.Value.Export.ExportedBodyCategory);
            Assert.Equal("aetheris-canonical-ap242", result.Value.Export.ExportedFeatureKind);

            var output = File.ReadAllText(outputPath);
            Assert.Contains("ADVANCED_FACE", output, StringComparison.Ordinal);
            Assert.Contains("VERTEX_POINT", output, StringComparison.Ordinal);
            Assert.True(Count(output, "ADVANCED_FACE") > 0);
            Assert.True(Count(output, "VERTEX_POINT") > 0);
            Assert.Contains("MANIFOLD_SOLID_BREP", output, StringComparison.Ordinal);
            Assert.DoesNotContain("trace-only", output, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(File.ReadAllText(Path.Combine(repo, InputStepPath)), output);

            var import = Step242Importer.ImportBody(output);
            Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.Equal(6, import.Value.Topology.Faces.Count());
            Assert.Equal(8, import.Value.Topology.Vertices.Count());
            Assert.Equal(12, import.Value.Topology.Edges.Count());
            Assert.Equal(480d, ComputeBoxVolume(import.Value), precision: 8);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }


    public static TheoryData<string, string, string, double> InlinePmiCases => new()
    {
        { "inline-step-v2-datum-pmi-on-canonical-face-emits-in-step", "SHAPE_ASPECT('firmament-datum:A'", "PROPERTY_DEFINITION('datum:A:importedPart'", 480d },
        { "inline-step-v2-hole-diameter-pmi-on-canonical-face-emits-in-step", "SHAPE_DIMENSION_REPRESENTATION('diameter:importedPart.holeDiameter'", "PROPERTY_DEFINITION('diameter:importedPart.holeDiameter'", 480d - Math.PI * 1d * 1d * 6d },
        { "inline-step-v2-recognized-face-datum-pmi-emits-in-step", "SHAPE_ASPECT('firmament-datum:A'", "PROPERTY_DEFINITION('datum:A:importedPart'", 480d },
        { "inline-step-v2-recognized-hole-diameter-pmi-emits-in-step", "SHAPE_DIMENSION_REPRESENTATION('diameter:importedPart.mountHoleDiameter'", "PROPERTY_DEFINITION('diameter:importedPart.mountHoleDiameter'", 480d - Math.PI * 1d * 1d * 6d }
    };

    [Theory]
    [MemberData(nameof(InlinePmiCases))]
    public void InlineStep_SemanticPmiOnCanonicalFace_ExportsReimportsAndKeepsGeometry(string fixtureId, string primaryEvidence, string secondaryEvidence, double expectedVolume)
    {
        var repo = FindRepoRoot();
        var fixturePath = Path.Combine(repo, $"fixtures/InlineStep/valid/{fixtureId}.valid.firmfixture");
        var outputPath = Path.Combine(Path.GetTempPath(), $"aetheris-inline-step-x2-{Guid.NewGuid():N}.step");
        try
        {
            var result = FirmamentBuildAndExport.Run(fixturePath, outputPath);

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.True(File.Exists(outputPath));
            Assert.Equal("inline-step", result.Value.Export.ExportedBodyCategory);

            var fixture = File.ReadAllText(fixturePath);
            Assert.Contains("canonical-input-required: true", fixture, StringComparison.Ordinal);
            Assert.Contains("semantic-pmi-required: true", fixture, StringComparison.Ordinal);
            Assert.Contains("graphical-pmi-required: false", fixture, StringComparison.Ordinal);

            var output = File.ReadAllText(outputPath);
            Assert.Contains("ADVANCED_FACE", output, StringComparison.Ordinal);
            Assert.Contains("VERTEX_POINT", output, StringComparison.Ordinal);
            Assert.Contains(primaryEvidence, output, StringComparison.Ordinal);
            Assert.Contains(secondaryEvidence, output, StringComparison.Ordinal);
            Assert.DoesNotContain("DRAUGHTING_CALLOUT", output, StringComparison.Ordinal);
            Assert.DoesNotContain("ANNOTATION_PLANE", output, StringComparison.Ordinal);

            var import = Step242Importer.ImportBody(output);
            Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.True(import.Value.Topology.Faces.Count() > 0);
            Assert.True(expectedVolume > 0d);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }


    [Fact]
    public void InlineStep_RecognizedRegion_ParsesAndStoresMetadata()
    {
        var repo = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(repo, "fixtures/InlineStep/valid/inline-step-v2-recognized-face-datum-pmi-emits-in-step.valid.firmfixture"));
        var parse = FirmamentV2Parser.Parse(source, Path.Combine(repo, "fixtures/InlineStep/valid"));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var region = Assert.Single(parse.Document!.RecognizedRegions!);
        Assert.Equal("importedPart", region.BodyName);
        Assert.Equal("topFace", region.RegionName);
        Assert.Equal("datumPlane", region.Kind);
        Assert.Equal("#40", Assert.Single(region.FaceRefs));
        Assert.Equal("high", region.Confidence);
        Assert.Equal("importedPart.region(\"topFace\")", Assert.Single(parse.Document.Pmi!).Target);
    }

    [Theory]
    [InlineData("kind: bogus\n            faces: [\"#40\"]\n            confidence: high", FirmamentV2Parser.InvalidRecognitionKind)]
    [InlineData("kind: datumPlane\n            faces: [\"#40\"]\n            confidence: maybe", FirmamentV2Parser.InvalidRecognitionConfidence)]
    [InlineData("kind: datumPlane\n            faces: [\"#999999\"]\n            confidence: high", FirmamentV2Parser.UnknownRecognitionFace)]
    public void InlineStep_InvalidRecognitionMetadata_RejectsDeterministically(string regionBody, string diagnostic)
    {
        var source = $$"""
model BadRecognition { units mm solid importedPart: InlineStep { path: "../testdata/canonical-box-10x8x6.step" } recognize importedPart { region topFace { {{regionBody}} } } }
""";
        var parse = FirmamentV2Parser.Parse(source, Path.Combine(FindRepoRoot(), "fixtures/InlineStep/valid"));

        Assert.False(parse.IsSuccess);
        Assert.Contains(diagnostic, parse.Diagnostics);
    }

    [Theory]
    [InlineData("recognize missingPart { region topFace { kind: datumPlane faces: [\"#191\"] confidence: high } }", FirmamentV2Parser.UnknownRecognitionBody)]
    [InlineData("recognize importedPart { region topFace { kind: datumPlane faces: [\"#191\"] confidence: high } region topFace { kind: datumPlane faces: [\"#77\"] confidence: high } }", FirmamentV2Parser.DuplicateRegion)]
    [InlineData("recognize importedPart { region topFace { kind: datumPlane faces: [\"#191\"] confidence: high } } pmi { datum A { target: importedPart.region(\"missingRegion\") } }", FirmamentV2Parser.UnknownRecognitionRegion)]
    [InlineData("recognize importedPart { region mountHole { kind: holeShaft faces: [\"#40\"] confidence: high } } pmi { datum A { target: importedPart.region(\"mountHole\") } }", FirmamentV2Parser.PmiRecognizedRegionKindMismatch)]
    public void InlineStep_InvalidRecognizedTargets_RejectDeterministically(string tail, string diagnostic)
    {
        var source = $$"""
model BadRecognitionTarget { units mm solid importedPart: InlineStep { path: "../testdata/canonical-box-10x8x6.step" } {{tail}} }
""";
        var parse = FirmamentV2Parser.Parse(source, Path.Combine(FindRepoRoot(), "fixtures/InlineStep/valid"));

        Assert.False(parse.IsSuccess);
        Assert.Contains(diagnostic, parse.Diagnostics);
    }

    [Theory]
    [InlineData("inline-step-v2-pmi-unknown-body.invalid.firmfixture", FirmamentV2Parser.InlineStepUnknownBody)]
    [InlineData("inline-step-v2-pmi-unknown-face.invalid.firmfixture", FirmamentV2Parser.InlineStepUnknownFace)]
    [InlineData("inline-step-v2-pmi-invalid-diameter.invalid.firmfixture", FirmamentV2Parser.PmiDiameterInvalid)]
    public void InlineStep_InvalidImportedPmiTargets_RejectDeterministically(string fixtureName, string diagnostic)
    {
        var repo = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(repo, "fixtures/InlineStep/invalid", fixtureName));
        var parse = FirmamentV2Parser.Parse(source, Path.Combine(repo, "fixtures/InlineStep/invalid"));

        Assert.False(parse.IsSuccess);
        Assert.Contains(diagnostic, parse.Diagnostics);
    }


    [Fact]
    public void InlineStep_RecognizedRegion_ParsesEvidenceAndSemanticProposalMetadataOnly()
    {
        var repo = FindRepoRoot();
        var fixturePath = Path.Combine(repo, "fixtures/InlineStep/valid/inline-step-v2-recognized-hole-proposal-report.valid.firmfixture");
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(fixturePath), Path.GetDirectoryName(fixturePath));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var region = Assert.Single(parse.Document!.RecognizedRegions!);
        Assert.Equal("holeShaft", region.Kind);
        Assert.Equal("#191", Assert.Single(region.FaceRefs));
        Assert.NotNull(region.Evidence);
        Assert.Equal("cylindrical", Assert.Single(region.Evidence!.SurfaceFamilies));
        Assert.Equal(1d, region.Evidence.Radius);
        Assert.Equal("+Z", region.Evidence.Axis);
        Assert.True(region.Evidence.Through);
        Assert.NotNull(region.Proposal);
        Assert.Equal("holeShaft", region.Proposal!.ProposalKind);
        Assert.Equal("mountHole", region.Proposal.FeatureName);
        Assert.Equal("importedPart.face(\"#51\")", region.Proposal.PlacementTarget);
        Assert.Equal(1d, region.Proposal.Radius);
        Assert.Equal("throughAll", region.Proposal.EndCondition);
        Assert.Empty(parse.Document.Replacements!);
    }

    [Theory]
    [InlineData("evidence { surfaceFamily: cylindrical radius: -1mm axis: +Z through: true }", FirmamentV2Parser.RecognitionEvidenceRadiusInvalid)]
    [InlineData("evidence { surfaceFamily: spline radius: 1mm axis: +Z through: true }", FirmamentV2Parser.RecognitionEvidenceSurfaceFamilyUnknown)]
    [InlineData("evidence { surfaceFamily: cylindrical radius: 1mm axis: north through: true }", FirmamentV2Parser.RecognitionEvidenceAxisInvalid)]
    [InlineData("proposes datumPlane { radius: 1mm end: throughAll }", FirmamentV2Parser.SemanticProposalKindMismatch)]
    [InlineData("proposes hole<shaft> { radius: -1mm end: throughAll }", FirmamentV2Parser.SemanticProposalRadiusInvalid)]
    [InlineData("proposes hole<shaft> { on: importedPart.face(\"#999999\") radius: 1mm end: throughAll }", FirmamentV2Parser.SemanticProposalTargetUnresolved)]
    [InlineData("proposes hole<shaft> { radius: 1mm end: blind }", FirmamentV2Parser.SemanticProposalEndUnsupported)]
    public void InlineStep_InvalidEvidenceOrProposal_RejectsDeterministically(string metadata, string diagnostic)
    {
        var source = $$"""
model BadRecognitionProposal { units mm solid importedPart: InlineStep { path: "../testdata/canonical-through-hole.step" } recognize importedPart { region mountHole { kind: hole<shaft> faces: ["#191"] confidence: high {{metadata}} } } }
""";
        var parse = FirmamentV2Parser.Parse(source, Path.Combine(FindRepoRoot(), "fixtures/InlineStep/valid"));

        Assert.False(parse.IsSuccess);
        Assert.Contains(diagnostic, parse.Diagnostics);
    }



    [Fact]
    public void InlineStep_ReplacementAssist_ReadyHoleProposalProducesStableReplacementText()
    {
        var parse = ParseRecognizedHoleProposalFixture();
        var report = InlineStepReplacementAssistReportBuilder.Build(parse.Document!);

        Assert.Equal(1, report.ReadyCount);
        Assert.Equal(0, report.BlockedCount);
        var assist = Assert.Single(report.Assists);
        Assert.True(assist.ReplacementReady);
        Assert.Equal("holeShaft", assist.ProposalKind);
        Assert.NotNull(assist.SuggestedReplacementModel);
        Assert.Equal("replace importedPart.region(\"mountHole\") with hole<shaft> mountHole {" + Environment.NewLine +
            "    on: importedPart.face(\"#51\")" + Environment.NewLine +
            "    center: [0mm, 0mm]" + Environment.NewLine +
            "    radius: 1mm" + Environment.NewLine +
            "    end: throughAll" + Environment.NewLine +
            "}", assist.SuggestedReplacementText);
        Assert.Empty(parse.Document!.Replacements!);
    }

    [Theory]
    [InlineData("datumPlane", "holeShaft", InlineStepReplacementAssistReportBuilder.RegionProposalKindMismatch)]
    [InlineData("datumPlane", "datumPlane", InlineStepReplacementAssistReportBuilder.UnsupportedProposalKind)]
    public void InlineStep_ReplacementAssist_BlocksKindIncompatibility(string regionKind, string proposalKind, string reason)
    {
        var document = ReplacementAssistDocument(regionKind: regionKind, proposalKind: proposalKind);
        var assist = Assert.Single(InlineStepReplacementAssistReportBuilder.Build(document).Assists);
        Assert.False(assist.ReplacementReady);
        Assert.Contains(reason, assist.Reasons);
    }

    [Theory]
    [InlineData(null, InlineStepReplacementAssistReportBuilder.MissingPlacementTarget)]
    [InlineData("importedPart.face(\"#999999\")", InlineStepReplacementAssistReportBuilder.UnresolvedPlacementTarget)]
    public void InlineStep_ReplacementAssist_BlocksMissingOrUnresolvedPlacementFace(string? target, string reason)
    {
        var document = ReplacementAssistDocument(placementTarget: target);
        var assist = Assert.Single(InlineStepReplacementAssistReportBuilder.Build(document).Assists);
        Assert.False(assist.ReplacementReady);
        Assert.Contains(reason, assist.Reasons);
    }

    [Theory]
    [InlineData(0d, InlineStepReplacementAssistReportBuilder.InvalidRadius)]
    [InlineData(-1d, InlineStepReplacementAssistReportBuilder.InvalidRadius)]
    public void InlineStep_ReplacementAssist_BlocksInvalidRadius(double radius, string reason)
    {
        var document = ReplacementAssistDocument(radius: radius);
        var assist = Assert.Single(InlineStepReplacementAssistReportBuilder.Build(document).Assists);
        Assert.False(assist.ReplacementReady);
        Assert.Contains(reason, assist.Reasons);
    }

    [Fact]
    public void InlineStep_ReplacementAssist_BlocksUnsupportedEndCondition()
    {
        var assist = Assert.Single(InlineStepReplacementAssistReportBuilder.Build(ReplacementAssistDocument(endCondition: "blind")).Assists);
        Assert.False(assist.ReplacementReady);
        Assert.Contains(InlineStepReplacementAssistReportBuilder.UnsupportedEndCondition, assist.Reasons);
    }

    [Theory]
    [InlineData(false, "cylindrical", 1d, InlineStepReplacementAssistReportBuilder.EvidenceThroughFalse)]
    [InlineData(true, "planar", 1d, InlineStepReplacementAssistReportBuilder.EvidenceSurfaceNotCylindrical)]
    [InlineData(true, "cylindrical", 2d, InlineStepReplacementAssistReportBuilder.EvidenceRadiusMismatch)]
    public void InlineStep_ReplacementAssist_BlocksEvidenceIncompatibility(bool through, string surfaceFamily, double evidenceRadius, string reason)
    {
        var document = ReplacementAssistDocument(evidenceThrough: through, surfaceFamily: surfaceFamily, evidenceRadius: evidenceRadius);
        var assist = Assert.Single(InlineStepReplacementAssistReportBuilder.Build(document).Assists);
        Assert.False(assist.ReplacementReady);
        Assert.Contains(reason, assist.Reasons);
    }

    private static FirmamentV2ParseResult ParseRecognizedHoleProposalFixture()
    {
        var repo = FindRepoRoot();
        var fixturePath = Path.Combine(repo, "fixtures/InlineStep/valid/inline-step-v2-recognized-hole-proposal-report.valid.firmfixture");
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(fixturePath), Path.GetDirectoryName(fixturePath));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        return parse;
    }

    private static FirmamentV2Document ReplacementAssistDocument(
        string regionKind = "holeShaft",
        string proposalKind = "holeShaft",
        string? placementTarget = "importedPart.face(\"#51\")",
        double radius = 1d,
        string endCondition = "throughAll",
        bool evidenceThrough = true,
        string surfaceFamily = "cylindrical",
        double evidenceRadius = 1d)
    {
        var map = new ImportedStepTopologyMap(new Dictionary<string, string> { ["#51"] = "face-51", ["#191"] = "face-191" }, new Dictionary<string, string>());
        var solid = new FirmamentV2SolidBinding("importedPart", "InlineStep", new FirmamentV2InlineStepRecord("canonical.step", "canonical.step", "hash", true, "canonical", map));
        var evidence = new FirmamentV2RecognitionEvidence([surfaceFamily], evidenceRadius, "+Z", null, evidenceThrough);
        var proposal = new FirmamentV2SemanticProposal(proposalKind, "mountHole", placementTarget, new FirmamentV2FaceLocalPoint2D(0d, 0d, FirmamentV2FaceLocalPoint2D.PlusZConvention), radius, endCondition);
        var region = new FirmamentV2RecognizedRegion("importedPart", "mountHole", regionKind, ["#191"], "high", evidence, proposal);
        return new FirmamentV2Document("AssistProbe", "mm", [solid], RecognizedRegions: [region], Replacements: []);
    }

    [Fact]
    public void InlineStep_ReplacementDeclaration_ParsesAndReferencesRecognizedRegion()
    {
        var repo = FindRepoRoot();
        var fixturePath = Path.Combine(repo, "fixtures/InlineStep/valid/inline-step-v2-replace-through-hole-step-verified.valid.firmfixture");
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(fixturePath), Path.GetDirectoryName(fixturePath));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var replacement = Assert.Single(parse.Document!.Replacements!);
        Assert.Equal("importedPart", replacement.ImportedBodyName);
        Assert.Equal("mountHole", replacement.RecognizedRegionName);
        Assert.Equal("holeShaft", replacement.ReplacementKind);
        Assert.Equal(1d, replacement.Radius);
        Assert.Equal("throughAll", replacement.EndCondition);
        Assert.Equal("importedPart.region(\"mountHole\")", replacement.TargetSource);
    }

    [Theory]
    [InlineData("replace missingPart.region(\"mountHole\") with hole<shaft> mountHole { on: importedPart.face(\"#191\") center: [0,0] radius: 1 end: throughAll hostSize: [10,8,6] }", FirmamentV2Parser.UnknownReplacementBody)]
    [InlineData("replace importedPart.region(\"missing\") with hole<shaft> mountHole { on: importedPart.face(\"#191\") center: [0,0] radius: 1 end: throughAll hostSize: [10,8,6] }", FirmamentV2Parser.UnknownReplacementRegion)]
    [InlineData("recognize importedPart { region mountHole { kind: datumPlane faces: [\"#191\"] confidence: high } } replace importedPart.region(\"mountHole\") with hole<shaft> mountHole { on: importedPart.face(\"#191\") center: [0,0] radius: 1 end: throughAll hostSize: [10,8,6] }", FirmamentV2Parser.ReplacementKindMismatch)]
    [InlineData("replace importedPart.region(\"mountHole\") with counterbore mountHole { on: importedPart.face(\"#191\") center: [0,0] radius: 1 end: throughAll hostSize: [10,8,6] }", FirmamentV2Parser.ReplacementUnsupportedKind)]
    [InlineData("replace importedPart.region(\"mountHole\") with hole<shaft> mountHole { on: importedPart.face(\"#999999\") center: [0,0] radius: 1 end: throughAll hostSize: [10,8,6] }", FirmamentV2Parser.ReplacementFaceUnresolved)]
    [InlineData("replace importedPart.region(\"mountHole\") with hole<shaft> mountHole { on: importedPart.face(\"#191\") center: [0,0] radius: -1 end: throughAll hostSize: [10,8,6] }", FirmamentV2Parser.ReplacementRadiusInvalid)]
    [InlineData("replace importedPart.region(\"mountHole\") with hole<shaft> mountHole { on: importedPart.face(\"#191\") center: [0,0] radius: 1 end: depth 2 hostSize: [10,8,6] }", FirmamentV2Parser.ReplacementEndUnsupported)]
    public void InlineStep_InvalidReplacement_RejectsDeterministically(string tail, string diagnostic)
    {
        var recognition = tail.Contains("recognize importedPart", StringComparison.Ordinal) ? string.Empty : "recognize importedPart { region mountHole { kind: holeShaft faces: [\"#191\"] confidence: high } }";
        var source = $$"""
model BadReplacement { units mm solid importedPart: InlineStep { path: "../testdata/canonical-through-hole.step" } {{recognition}} {{tail}} }
""";
        var parse = FirmamentV2Parser.Parse(source, Path.Combine(FindRepoRoot(), "fixtures/InlineStep/valid"));

        Assert.False(parse.IsSuccess);
        Assert.Contains(diagnostic, parse.Diagnostics);
    }

    [Fact]
    public void InlineStep_ReplacementFixture_ExportsReimportsAndKeepsSingleThroughHoleVolume()
    {
        var repo = FindRepoRoot();
        var fixturePath = Path.Combine(repo, "fixtures/InlineStep/valid/inline-step-v2-replace-through-hole-step-verified.valid.firmfixture");
        var outputPath = Path.Combine(Path.GetTempPath(), $"aetheris-inline-step-x4-{Guid.NewGuid():N}.step");
        try
        {
            var result = FirmamentBuildAndExport.Run(fixturePath, outputPath);
            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.Equal("inline-step-replacement", result.Value.Export.ExportedBodyCategory);
            var output = File.ReadAllText(outputPath);
            Assert.Contains("ADVANCED_FACE", output, StringComparison.Ordinal);
            Assert.DoesNotContain("trace-only", output, StringComparison.OrdinalIgnoreCase);
            var import = Step242Importer.ImportBody(output);
            Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.Equal(1, import.Value.Geometry.Surfaces.Count(entry => entry.Value.Kind.ToString() == "Cylinder"));
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void InlineStep_MigrationReport_RecognizedOnlyCountsResidualCoverage()
    {
        var repo = FindRepoRoot();
        var fixturePath = Path.Combine(repo, "fixtures/InlineStep/valid/inline-step-v2-recognized-face-datum-pmi-emits-in-step.valid.firmfixture");
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(fixturePath), Path.GetDirectoryName(fixturePath));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));

        var report = InlineStepMigrationReportBuilder.Build(parse.Document!, parse.Document!.Solids.Single());

        Assert.Equal("importedPart", report.ImportedBodyName);
        Assert.Equal(6, report.OriginalTopology.FaceCount);
        Assert.Equal(1, report.Recognized.RegionCount);
        Assert.Equal(1, report.Recognized.ReferencedFaceCount);
        Assert.Equal(0, report.Replacements.ReplacedFaceCount);
        Assert.Equal(report.OriginalTopology.FaceCount, report.Residual.ResidualFaceCount);
        Assert.Equal(1d / 6d, report.Coverage.RecognizedFaceRatio, precision: 12);
        Assert.Equal(0d, report.Coverage.ReplacedFaceRatio);
        Assert.Contains("recognized", report.ReplacementStates);
        Assert.Contains("residual-emitted", report.ReplacementStates);
    }

    [Fact]
    public void InlineStep_MigrationReport_RecognizedProposalCountsDoNotReplace()
    {
        var repo = FindRepoRoot();
        var fixturePath = Path.Combine(repo, "fixtures/InlineStep/valid/inline-step-v2-recognized-hole-proposal-report.valid.firmfixture");
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(fixturePath), Path.GetDirectoryName(fixturePath));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));

        var report = InlineStepMigrationReportBuilder.Build(parse.Document!, parse.Document!.Solids.Single());

        Assert.Equal(7, report.OriginalTopology.FaceCount);
        Assert.Equal(1, report.Recognized.RegionCount);
        Assert.Equal(1, report.Recognized.EvidenceCount);
        Assert.Equal(1, report.Recognized.ProposalCount);
        Assert.Equal(0, report.Recognized.ProposalVerifiedCount);
        Assert.Equal(1, report.Recognized.ProposalUnverifiedCount);
        Assert.Equal(0, report.Replacements.PlannedCount);
        Assert.Equal(0, report.Replacements.VerifiedCount);
        Assert.Equal(0, report.Replacements.EmittedCount);
        Assert.Equal(7, report.Residual.ResidualFaceCount);
        Assert.Equal("canonical-reexport", report.EmissionStrategy);
        Assert.False(report.ResidualSurgery);
    }

    [Fact]
    public void InlineStep_MigrationReport_VerifiedReplacementCountsReplacedFaces()
    {
        var repo = FindRepoRoot();
        var fixturePath = Path.Combine(repo, "fixtures/InlineStep/valid/inline-step-v2-replace-through-hole-step-verified.valid.firmfixture");
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(fixturePath), Path.GetDirectoryName(fixturePath));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));

        var report = InlineStepMigrationReportBuilder.Build(parse.Document!, parse.Document!.Solids.Single(), replacementsVerified: true, replacementsEmitted: true, emissionStrategy: "holeShaft-bounded-rebuild");

        Assert.Equal(7, report.OriginalTopology.FaceCount);
        Assert.Equal(1, report.Recognized.RegionCount);
        Assert.Equal(1, report.Replacements.PlannedCount);
        Assert.Equal(1, report.Replacements.VerifiedCount);
        Assert.Equal(1, report.Replacements.EmittedCount);
        Assert.Equal(1, report.Replacements.ReplacedFaceCount);
        Assert.Equal(6, report.Residual.ResidualFaceCount);
        Assert.Equal("holeShaft-bounded-rebuild", report.EmissionStrategy);
        Assert.False(report.ResidualSurgery);
        Assert.Contains("hybrid-step-verified", report.ReplacementStates);
    }

    [Fact]
    public void InlineStep_MigrationReport_DuplicateAndUnresolvedFacesAreDeterministic()
    {
        var map = new ImportedStepTopologyMap(new Dictionary<string, string>(StringComparer.Ordinal) { ["#1"] = "f1", ["#2"] = "f2" }, new Dictionary<string, string>(StringComparer.Ordinal));
        var inlineStep = new FirmamentV2InlineStepRecord("part.step", "/tmp/part.step", "hash", true, "Aetheris-canonical", map);
        var solid = new FirmamentV2SolidBinding("importedPart", "InlineStep", inlineStep);
        var document = new FirmamentV2Document("MigrationProbe", "mm", [solid], RecognizedRegions: [
            new FirmamentV2RecognizedRegion("importedPart", "a", "holeShaft", ["#1", "#1"], "high"),
            new FirmamentV2RecognizedRegion("importedPart", "b", "holeShaft", ["#2", "#999"], "high")]);

        var report = InlineStepMigrationReportBuilder.Build(document, solid);

        Assert.Equal(2, report.OriginalTopology.FaceCount);
        Assert.Equal(2, report.Recognized.RegionCount);
        Assert.Equal(2, report.Recognized.ReferencedFaceCount);
        Assert.Equal(1, report.Recognized.DuplicateReferencedFaceCount);
        Assert.Equal(1, report.Recognized.UnresolvedReferenceCount);
        Assert.Equal(1d, report.Coverage.RecognizedFaceRatio);
        Assert.Contains("inline-step-migration-duplicate-face-reference:#1", report.Diagnostics);
        Assert.Contains("inline-step-migration-unresolved-face:#999", report.Diagnostics);
    }

    [Fact]
    public void InlineStep_NonCanonicalStep_IsRejectedWithDeterministicDiagnostic()
    {
        var source = """
model BadInlineStep {
    units mm

    solid importedPart: InlineStep {
        path: "../../../testdata/step242/generated/v0-required/gen_box_v0.step"
    }
}
""";

        var parse = FirmamentV2Parser.Parse(source, Path.Combine(FindRepoRoot(), "fixtures/InlineStep/valid"));

        Assert.False(parse.IsSuccess);
        Assert.Contains(FirmamentV2Parser.InlineStepRequiresCanonical, parse.Diagnostics);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static int Count(string text, string needle) => text.Split(needle, StringSplitOptions.None).Length - 1;

    private static double ComputeBoxVolume(Aetheris.Kernel.Core.Brep.BrepBody body)
    {
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException("Missing vertex point.")).ToArray();
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        var minZ = points.Min(p => p.Z); var maxZ = points.Max(p => p.Z);
        return (maxX - minX) * (maxY - minY) * (maxZ - minZ);
    }
}
