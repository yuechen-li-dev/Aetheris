using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentImportedHoleRevisionTests
{
    static string DirectoryPath
    {
        get
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
            return Path.Combine(root!.FullName, "fixtures", "Regression", "InlineStep", "production");
        }
    }
    static string Source => File.ReadAllText(Path.Combine(DirectoryPath, "ctc01-hole-move.firmament"));
    static FirmamentV2ValidationReport Validate(string source) => FirmamentBuildAdmissionValidation.Admit(
        FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source, DirectoryPath), "ctc01-hole-move.firmament", sourceText: source), source, DirectoryPath);

    [Fact]
    public void RecoveryPlan_UsesNamedLayoutAndRebuildsAfterOnlyTargetChanges()
    {
        var inspection = FirmamentImportedFeatures.Inspect(File.ReadAllText(StepPath), .01).Value;
        var selected = Assert.Single(inspection.Discovery.Candidates, c => c.CanPlan && c.Center is {} p && System.Math.Abs(p.X+160)<1e-6 && System.Math.Abs(p.Y-45)<1e-6 && System.Math.Abs(c.Diameter-25)<1e-6);
        var planned = FirmamentImportedFeatures.PlanMove(inspection, selected.Id, "MountHole", new(130.04,45.0000000000001,-1e-14), true,
            clearanceMillimetres: .11);
        Assert.True(planned.IsSuccess, string.Join(" | ", planned.Diagnostics.Select(d => d.Message)));
        Assert.Equal("Built", planned.Value.Admission);
        Assert.Contains("center: RevisionLayout.Targets.MountHole", planned.Value.Source);
        Assert.Contains("MountHole: Point3(130mm, 45mm, 0mm)", planned.Value.Source);
        Assert.Contains("diameter: 25mm", planned.Value.Source);
        Assert.Contains("importTolerance: 0.01mm", planned.Value.Source);
        Assert.DoesNotContain("999999", planned.Value.Source);
        Assert.Equal(.1, planned.Value.AuthoringIncrementMillimetres);
        Assert.Equal(25, planned.Value.Revision.Diameter);
        Assert.Equal(-172.7, planned.Value.Revision.Within.Min.X);
        Assert.Equal(32.3, planned.Value.Revision.Within.Min.Y);
        Assert.Contains("MountHole.X - 12.7mm", planned.Value.Source);
        Assert.DoesNotContain("-0mm", planned.Value.Source);
        var physical = Step242Importer.ImportBody(planned.Value.Export.StepText).Value;
        var pmi = Assert.Single(Step242SemanticPmiInspector.Inspect(planned.Value.Export.StepText).Items, item => item.Kind == "Diameter");
        Assert.Equal(25, pmi.Value);
        Assert.All(pmi.GeometricFaceEntityIds, id =>
        {
            var face = Assert.Single(physical.Bindings.FaceBindings, b => b.SourceStepEntityId == id);
            var wall = physical.GetFaceSurface(face.FaceId).Cylinder!.Value;
            Assert.Equal(130, wall.Origin.X);
            Assert.Equal(45, wall.Origin.Y);
            Assert.Equal(12.5, wall.Radius);
        });
        Assert.NotNull(planned.Value.Revision.DestinationWithin);
        var edited = planned.Value.Source.Replace("MountHole: Point3(130mm, 45mm, 0mm)", "MountHole: Point3(-130mm, 45mm, 0mm)");
        Assert.NotEqual(planned.Value.Source, edited);
        var rebuilt = FirmamentBuildAndExport.CompileSource(edited, "", new Dictionary<string,string> { ["input.step"] = File.ReadAllText(StepPath) });
        Assert.True(rebuilt.IsSuccess, string.Join(" | ", rebuilt.Diagnostics.Select(d => d.Message)));
        Assert.Equal(-130, rebuilt.Value.ImportedHoleRevision!.DestinationWithin!.Min.X + (rebuilt.Value.ImportedHoleRevision.DestinationWithin.Max.X-rebuilt.Value.ImportedHoleRevision.DestinationWithin.Min.X)/2, 6);
        Assert.Equal(115, rebuilt.Value.ImportedHoleRevision.PreservedFaces);
        var occupied = planned.Value.Source.Replace("MountHole: Point3(130mm, 45mm, 0mm)", "MountHole: Point3(160mm, 45mm, 0mm)");
        var resources = new Dictionary<string, string> { ["input.step"] = File.ReadAllText(StepPath) };
        Assert.False(FirmamentBuildAndExport.CompileSource(occupied, "", resources).IsSuccess);
        Assert.False(FirmamentImportedFeatures.PlanMove(inspection, selected.Id, "MountHole", new(130,45,0), false).IsSuccess);
        Assert.False(FirmamentImportedFeatures.PlanMove(inspection, selected.Id, "Not.AnIdentifier", new(130,45,0), true).IsSuccess);
    }

    [Fact]
    public void RecoveredGoldenFixture_ValidateAndBuildAgree()
    {
        var source = File.ReadAllText(Path.Combine(DirectoryPath,"ctc01-recovered-hole-move.firmament"));
        Assert.Equal("valid", Validate(source).Status);
        var build = FirmamentBuildAndExport.CompileSource(source,DirectoryPath);
        Assert.True(build.IsSuccess,string.Join(" | ",build.Diagnostics.Select(d=>d.Message)));
        Assert.Equal(115,build.Value.ImportedHoleRevision!.PreservedFaces);
        var invalid = source.Replace("through-hole:step-764+765", "through-hole:step-stale");
        Assert.Equal("invalid",Validate(invalid).Status);
        Assert.False(FirmamentBuildAndExport.CompileSource(invalid,DirectoryPath).IsSuccess);
        var conflicting = source.Replace("kind: HoleShaft", "kind: HoleShaft\n            faces: [1, 2]");
        Assert.Equal("invalid", Validate(conflicting).Status);
        Assert.False(FirmamentBuildAndExport.CompileSource(conflicting, DirectoryPath).IsSuccess);
        var missingBounds = source.Replace("within: [", "within: [null,");
        Assert.Equal("invalid", Validate(missingBounds).Status);
        Assert.False(FirmamentBuildAndExport.CompileSource(missingBounds, DirectoryPath).IsSuccess);
    }

    [Fact]
    public void GoldenPath_AdmitsBuildsAndExportsQualifiedMovedHoleWithSemanticPmi()
    {
        var source = Source;
        var validation = Validate(source);
        Assert.Equal("valid", validation.Status); Assert.Equal("Built", validation.ValidationLevel);
        var build = FirmamentBuildAndExport.CompileSource(source, DirectoryPath);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(d => d.Message)));
        var export = build.Value; var report = Assert.IsType<FirmamentImportedHoleRevisionReport>(export.ImportedHoleRevision);
        Assert.True(report.SourceSemanticsExcluded); Assert.Equal(.01, report.ImportToleranceMillimetres);
        Assert.Equal(115, report.PreservedFaces); Assert.Equal(312, report.PreservedEdges);
        Assert.Equal(25, report.Diameter); Assert.Equal(50, report.Depth);
        Assert.Equal("Ctc01Revision", export.DocumentName);
        Assert.Equal(25, Assert.Single(export.DimensionInspection!).Value);
        Assert.True(export.InlineStepMigration!.ResidualSurgery);
        Assert.Contains(build.Diagnostics, d => d.Severity == Core.Diagnostics.KernelDiagnosticSeverity.Warning && d.Message.StartsWith("firmament-inline-step-source-semantics-excluded"));
        var imported = Step242Importer.ImportBody(export.StepText);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(ImportQualificationStatus.Qualified, imported.Value.ImportQualification!.GeometryStatus);
        Assert.Equal(636, imported.Value.Bindings.PcurveBindings.Count());
        Assert.Equal(.01, imported.Value.ImportQualification.SourceDistanceAccuracyMillimetres);
        var pmi = Step242SemanticPmiInspector.Inspect(export.StepText);
        var hole = Assert.Single(pmi.Items, i => i.Kind == "Diameter");
        Assert.Equal(25, hole.Value); Assert.Equal(50, hole.Depth); Assert.Equal(2, hole.GeometricFaceEntityIds.Count);
        Assert.All(hole.GeometricFaceEntityIds, id =>
        {
            var binding = Assert.Single(imported.Value.Bindings.FaceBindings, b => b.SourceStepEntityId == id);
            imported.Value.TryGetFaceSurfaceGeometry(binding.FaceId, out var support);
            Assert.Equal(-130, support!.Cylinder!.Value.Origin.X, 6);
        });
        Assert.True(export.RuntimeAdmissionDocument!.Solid.InlineStep!.TopologyMap.TryResolveFaceEntity("#764", out var mapped));
        Assert.Equal("face-118", mapped);
    }

    [Fact]
    public void AuthoredPmi_ResolvesOriginalRegionToReplacementWalls()
    {
        var source = Source[..Source.LastIndexOf('}')] + """
            Pmi { HoleDiameter MovedSize { target: Source.region("MountHole") value: 25mm } }
        }
        """;
        var build = FirmamentBuildAndExport.CompileSource(source, DirectoryPath);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(d => d.Message)));
        var inspection = Step242SemanticPmiInspector.Inspect(build.Value.StepText);
        var size = Assert.Single(inspection.Items, item => item.Kind == "Diameter" && item.Name.Contains("MovedSize"));
        Assert.Equal(25, size.Value);
        Assert.Equal(2, size.GeometricFaceEntityIds.Count);
        var body = Step242Importer.ImportBody(build.Value.StepText).Value;
        Assert.All(size.GeometricFaceEntityIds, id => {
            var binding = Assert.Single(body.Bindings.FaceBindings, b => b.SourceStepEntityId == id);
            Assert.True(body.TryGetFaceSurfaceGeometry(binding.FaceId, out var support));
            Assert.Equal(-130, support!.Cylinder!.Value.Origin.X, 6);
        });
    }

    static string StepPath => Path.GetFullPath(Path.Combine(DirectoryPath, "../../../../testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp"));

    [Fact]
    public void BytePin_RemainsExactAndExplicit()
    {
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(StepPath))).ToLowerInvariant();
        var source = Source.Replace("textSha256:", "sha256:").Replace("341cea888fb316673ed683e59391c11b4f96cff2542d9387e135f06682dbd745", digest);
        var build = FirmamentBuildAndExport.CompileSource(source, DirectoryPath);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(d => d.Message)));
        Assert.Equal("sha256-bytes", build.Value.ImportedHoleRevision!.SourceHashKind);
        Assert.Equal(digest, build.Value.ImportedHoleRevision.SourceHash);
        Assert.Equal(digest, build.Value.ImportedHoleRevision.SourceByteHash);
    }

    [Fact]
    public void BytePin_DoesNotSilentlyNormalizeLineEndings()
    {
        var text = File.ReadAllText(StepPath).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", "\r\n", StringComparison.Ordinal);
        var resources = new Dictionary<string, string> { ["testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp"] = text };
        var build = FirmamentBuildAndExport.CompileSource(Source.Replace("textSha256:", "sha256:"), "fixtures/Regression/InlineStep/production", resources);
        Assert.False(build.IsSuccess);
        Assert.Contains(build.Diagnostics, d => d.Message.Contains("firmament-inline-step-source-pin-mismatch", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TextPin_IsPortableAcrossExplicitLfAndCrlfResourceSnapshots(bool crlf)
    {
        var text = File.ReadAllText(StepPath).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (crlf) text = text.Replace("\n", "\r\n", StringComparison.Ordinal);
        var resources = new Dictionary<string, string> { ["testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp"] = text };
        var build = FirmamentBuildAndExport.CompileSource(Source, "fixtures/Regression/InlineStep/production", resources);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(d => d.Message)));
        Assert.Equal("sha256-utf8-lf", build.Value.ImportedHoleRevision!.SourceHashKind);
        Assert.Equal("341cea888fb316673ed683e59391c11b4f96cff2542d9387e135f06682dbd745", build.Value.ImportedHoleRevision.SourceHash);
        Assert.Equal(25, build.Value.ImportedHoleRevision.Diameter);
    }

    [Theory]
    [InlineData("pin")]
    [InlineData("tolerance")]
    [InlineData("annotations")]
    [InlineData("diameter")]
    [InlineData("partial")]
    [InlineData("outside")]
    [InlineData("evidence")]
    [InlineData("on")]
    [InlineData("field")]
    [InlineData("bounds-field")]
    [InlineData("import-field")]
    [InlineData("pin-conflict")][InlineData("named-face")][InlineData("object-type")]
    [InlineData("no-pin")]
    [InlineData("notes")]
    public void InvalidIntent_ValidateAndBuildRejectWithLocatedDiagnostics(string change)
    {
        var source = change switch
        {
            "pin" => Source.Replace("341cea88", "041cea88"),
            "tolerance" => Source.Replace("importTolerance: 0.01mm", "importTolerance: 0.001mm"),
            "annotations" => Source.Replace("geometryOnly: true", "geometryOnly: false"),
            "diameter" => Source.Replace("diameter: 25mm", "diameter: 30mm"),
            "partial" => Source.Replace("[\"#764\", \"#765\"]", "[\"#764\"]"),
            "outside" => Source.Replace("Point3(-130mm, 45mm, 0mm)", "Point3(200mm, 45mm, 0mm)"),
            "evidence" => Source.Replace("radius: 12.5mm", "radius: 13mm"),
            "on" => Source.Replace("Source.Face(98)", "Source.Face(4)"),
            "field" => Source.Replace("diameter: 25mm", "diammeter: 25mm"),
            "pin-conflict" => Source.Replace("textSha256:", "sha256: \"0000000000000000000000000000000000000000000000000000000000000000\"\n        textSha256:"),
            "named-face" => Source.Replace("Source.Face(98)", "Source.Face(ignored: 98)"),
            "object-type" => Source.Replace("within: {", "within: Nonsense {"),
            "no-pin" => Source.Replace("textSha256:", "// textSha256:"),
            "notes" => Source.Replace("radius: 12.5mm", "notes: [12]\n                radius: 12.5mm"),
            "bounds-field" => Source.Replace("min: Point3", "minimum: Point3"),
            _ => Source.Replace("geometryOnly: true", "geometryOnyl: true")
        };
        var build = FirmamentBuildAndExport.CompileSource(source, DirectoryPath);
        var validation = Validate(source);
        Assert.False(build.IsSuccess);
        Assert.Equal("invalid", validation.Status);
        Assert.Contains(validation.Diagnostics, d => d.Severity is "error" or "fatal");
        Assert.Contains(validation.Diagnostics, d => d.SourceSpan is not null);
        Assert.DoesNotContain(validation.Diagnostics, d => d.Code.Contains("toon", StringComparison.OrdinalIgnoreCase));
    }
}
