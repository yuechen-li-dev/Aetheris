using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CodeNativeAssemblyRehearsalTests
{
    private const string Root = "fixtures/Regression/CodeNativeAssembly/";

    [Fact]
    public void FastenerHasExactLeadInShaftAndCountersunkHead()
        => AssertFastenerShape(BuildBody("fastener.firmament"), 2, 1);

    [Fact]
    public void SectionChainWitnessKeepsItsExistingGeometry()
    {
        var chain = SectionChainAuthoringParser.CompileFile(PathOf("fastener-sections.firmament"));
        Assert.True(chain.IsSuccess, string.Join("\n", chain.Diagnostics));
        Assert.All(chain.Chain!.Sections, section => Assert.Equal(4, section.Profile.Spans.Count));
        AssertFastenerShape(chain.Materialization!.Body!, 8, 4);
    }

    [Theory]
    [InlineData("full")]
    [InlineData("-360deg")]
    public void AxisTouchingRevolveBuildsTheSameFlatEndedSolid(string angle)
    {
        var source = File.ReadAllText(PathOf("solid-fastener-revolve.firmament")).Replace("angle: full", "angle: " + angle, StringComparison.Ordinal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        AssertFastenerShape(Step242Importer.ImportBody(built.Value.StepText).Value, 2, 1);
        Assert.Equal(5, built.Value.Revolve!.SurfaceRoles.Count);
    }

    [Fact]
    public void AxisClosedStackUsesItsAuthoredWorldFrame()
    {
        var source = File.ReadAllText(PathOf("solid-fastener-revolve.firmament"))
            .Replace("direction: [0, 0, 1]", "direction: [0, 1, 0]", StringComparison.Ordinal)
            .Replace("on XZ", "on XY", StringComparison.Ordinal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        var body = Step242Importer.ImportBody(built.Value.StepText).Value;
        Assert.Contains(body.Geometry.Curves.Select(c => c.Value), c => c.Circle3 is { } circle && Near(circle.Radius, 6)
            && Near(circle.Center.Y, 16) && Near(circle.Center.X, 0) && Near(circle.Center.Z, 0));
        Assert.All(body.Geometry.Surfaces.Select(s => s.Value).Where(s => s.Cylinder is not null),
            s => Assert.True(Near(Math.Abs(s.Cylinder!.Value.Axis.ToVector().Y), 1)));
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
    }

    [Theory]
    [InlineData("5.7deg")]
    [InlineData("5.7mm tol 0.1mm")]
    public void PrimitiveCompositionCannotEraseWrongTypesOrUnsupportedTolerances(string replacement)
    {
        var source = File.ReadAllText(PathOf("fastener-library.firmament")) + "\n" + File.ReadAllText(PathOf("fastener.firmament"))
            .Replace("include \"fastener-library.firmament\";", "", StringComparison.Ordinal);
        source = source.Replace("shaftDiameter: 5.7mm", "shaftDiameter: " + replacement, StringComparison.Ordinal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        Assert.NotEmpty(built.Diagnostics);
    }

    private static void AssertFastenerShape(BrepBody body, int cones, int cylinders)
    {
        var supports = body.Geometry.Surfaces.Select(s => s.Value).ToArray();
        Assert.Equal(cones, supports.Count(s => s.Kind == SurfaceGeometryKind.Cone));
        Assert.Equal(cylinders, supports.Count(s => s.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(2, supports.Count(s => s.Kind == SurfaceGeometryKind.Plane));
        Assert.All(supports.Where(s => s.Cone is not null), s => Assert.Equal(Math.PI / 4, s.Cone!.Value.SemiAngleRadians, 10));
        var circles = body.Geometry.Curves.Select(c => c.Value).Where(c => c.Circle3 is not null).Select(c => c.Circle3!.Value).ToArray();
        Assert.Contains(circles, c => Near(c.Radius, 2.35) && Near(c.Center.Z, 0));
        Assert.Contains(circles, c => Near(c.Radius, 2.85) && Near(c.Center.Z, .5));
        Assert.Contains(circles, c => Near(c.Radius, 2.85) && Near(c.Center.Z, 12.85));
        Assert.Contains(circles, c => Near(c.Radius, 6) && Near(c.Center.Z, 16));
        var expected = Math.PI * (.5 / 3 * (2.35 * 2.35 + 2.35 * 2.85 + 2.85 * 2.85)
            + 12.35 * 2.85 * 2.85 + 3.15 / 3 * (2.85 * 2.85 + 2.85 * 6 + 36));
        var mass = BrepMassProperties.Evaluate(body, new(.001, Math.PI / 128, .001, 64, 128));
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent);
        Assert.NotNull(mass.ErrorBound);
        Assert.InRange(mass.ErrorBound.Value, 0, expected * .005);
        Assert.InRange(Math.Abs(mass.AbsoluteVolume - expected), 0, mass.ErrorBound.Value);
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
    }

    [Theory]
    [InlineData("5.7deg")]
    [InlineData("5.7mm tol 0.1mm")]
    public void SectionChainRejectsWrongDimensionsAndUnprojectedTolerance(string replacement)
    {
        var source = File.ReadAllText(PathOf("fastener-sections.firmament")).Replace("= 5.7mm", "= " + replacement, StringComparison.Ordinal);
        var result = SectionChainAuthoringParser.Compile(source);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Theory]
    [InlineData(3, 3, SurfaceGeometryKind.Cylinder)]
    [InlineData(3, 6, SurfaceGeometryKind.Cone)]
    public void AlignedCircularArcSpansRetainTheirExactAnalyticCarrier(double a, double b, SurfaceGeometryKind expected)
    {
        var patch = ArcPatch(a, b, 0, Math.PI / 2);
        Assert.Equal(expected, patch.ExactSurface.Kind);
        for (var i = 0; i <= 8; i++)
        {
            var point = patch.Evaluate(i / 8d, .4);
            Assert.Equal(a + (b - a) * .4, Math.Sqrt(point.X * point.X + point.Y * point.Y), 10);
            Assert.Equal(4, point.Z, 10);
        }
    }

    [Fact]
    public void DifferentArcPhaseOrSweepCannotBeClaimedAsAnExactCone()
    {
        Assert.NotEqual(SurfaceGeometryKind.Cone, ArcPatch(3, 6, .1, Math.PI / 2).ExactSurface.Kind);
        Assert.NotEqual(SurfaceGeometryKind.Cone, ArcPatch(3, 6, 0, Math.PI / 3).ExactSurface.Kind);
    }

    [Fact]
    public void HoleRelocationMovesOnlyTheFastenerPlacementAndPreservesAssemblyRoundTrip()
    {
        var baseline = Compile("assembly.firmasm");
        var moved = Compile("relocated.firmasm");
        Assert.Equal(2, baseline.Geometry!.Artifact.Definitions.Count);
        Assert.Equal(3, baseline.Ir!.Instances.Count);
        var fastener = baseline.Ir.Instances.Single(i => i.Path.ToString() == "MountedBracket.Fastener");
        var next = moved.Ir!.Instances.Single(i => i.Path.ToString() == fastener.Path.ToString());
        Assert.Equal(fastener.StableId, next.StableId);
        Assert.Equal(new[] { 76d, 10d, -4d }, fastener.ResolvedTransform!.Matrix.Skip(12).Take(3));
        Assert.Equal(new[] { 60d, 10d, -4d }, next.ResolvedTransform!.Matrix.Skip(12).Take(3));
        Assert.Equal(baseline.Geometry.Artifact.Definitions.Single(d => d.DefinitionIdentity.StartsWith("Fastener<")).StepSha256,
            moved.Geometry!.Artifact.Definitions.Single(d => d.DefinitionIdentity.StartsWith("Fastener<")).StepSha256);

        // Inspect the world-space conical carriers, not just the mate residual.
        var fastBody = baseline.Geometry.InstanceBodies[fastener.StableId];
        var hostId = baseline.Ir.Instances.Single(i => i.Path.ToString() == "MountedBracket.Bracket").StableId;
        var hostBody = baseline.Geometry.InstanceBodies[hostId];
        var headCones = fastBody.Geometry.Surfaces.Select(s => s.Value).Where(s => s.Cone is not null && Near(s.Cone.Value.Apex.Z, 6)).ToArray();
        Assert.Single(headCones);
        Assert.All(headCones,
            s => Assert.True(Near(s.Cone!.Value.Apex.X, 76) && Near(s.Cone.Value.Apex.Y, 10)));
        Assert.Contains(hostBody.Geometry.Surfaces.Select(s => s.Value), s => s.Cone is { } cone && Near(cone.Apex.X, 76) && Near(cone.Apex.Y, 10) && Near(cone.Apex.Z, 6) && Near(cone.SemiAngleRadians, Math.PI / 4));
        Assert.Contains(hostBody.Geometry.Surfaces.Select(s => s.Value), s => s.Cylinder is { } cylinder && Near(cylinder.Radius, 3));
        foreach (var height in new[] { 0d, 8.85, 9, 12 })
        {
            var opening = height <= 9 ? 3 : height - 6;
            var fastenerRadius = height <= 8.85 ? 2.85 : height - 6;
            Assert.True(opening - fastenerRadius >= -1e-9);
        }
        var exported = AssemblyIrAp242Exporter.Export(baseline);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics));
        var imported = Step242AssemblyImporter.Import(exported.Value);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
        Assert.Equal("SourceAssembly", imported.Value.Provenance);
        Assert.Equal(2, imported.Value.Definitions.Count(d => d.Geometry is not null));
        Assert.Equal(2, imported.Value.Occurrences.Count);
        Assert.Contains(imported.Value.Occurrences, o => o.Name == "Fastener" && Near(o.LocalTransform[12], 76) && Near(o.LocalTransform[14], -4));
    }

    [Fact]
    public void AssemblyExportsOnlyItsOwnExplicitPmiAndKeepsPartGeometry()
    {
        var baseline = Compile("assembly.firmasm");
        var original = AssemblyIrAp242Exporter.Export(baseline);
        var noInheritedPmi = Step242SemanticPmiInspector.Inspect(original.Value);
        Assert.True(noInheritedPmi.Success);
        Assert.Empty(noInheritedPmi.Items); // Bracket declares Datum A and shaftSize in its Part.
        var source = File.ReadAllText(PathOf("assembly.firmasm")).Replace("anchor: MountedBracket.Bracket;",
            "Pmi { Note Seating { Target: MountedBracket.Fastener; Text: \"Nominal countersunk seat; threadless demonstrator.\"; } }\n    anchor: MountedBracket.Bracket;", StringComparison.Ordinal);
        var explicitPmi = new AssemblyM1Pipeline().Compile(source, PathOf("assembly.firmasm"));
        Assert.True(explicitPmi.IsSuccess, string.Join("\n", explicitPmi.Diagnostics));
        Assert.Equal(baseline.Geometry!.Artifact.DeterministicSha256, explicitPmi.Geometry!.Artifact.DeterministicSha256);
        var exported = AssemblyIrAp242Exporter.Export(explicitPmi);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics));
        var inspection = Step242SemanticPmiInspector.Inspect(exported.Value);
        Assert.True(inspection.Success, string.Join("\n", inspection.Diagnostics));
        var note = Assert.Single(inspection.Items);
        Assert.Equal("Seating", note.Name);
        Assert.Equal("MountedBracket.Fastener", note.Target);
        Assert.Equal(0, inspection.DatumCount);
        Assert.Equal(0, inspection.DimensionCount);
    }

    [Theory]
    [InlineData("height: 10mm", "heigth: 10mm")]
    [InlineData("on: shaft.Top", "on: shaft.Bottom")]
    [InlineData("bottomRadius: 3mm", "bottomRadius: 4mm")]
    [InlineData("distance: 0.5mm", "distance: 3mm")]
    [InlineData("on: shaft.Bottom", "on: head.Bottom")]
    [InlineData("EdgeFinish<Chamfer>", "EdgeFinish<Fillet>")]
    [InlineData("Cylinder shaft", "Box shaft")]
    [InlineData("target: Boundary", "target: AllEdges")]
    public void UnsupportedCoaxialIntentRejectsInValidateAndBuild(string before, string after)
    {
        var source = CoaxialSource.Replace(before, after, StringComparison.Ordinal);
        Assert.NotEqual(CoaxialSource, source);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        var parsed = FirmamentV2Parser.Parse(source);
        var admitted = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parsed), source);
        Assert.Equal("invalid", admitted.Status);
        Assert.Contains(admitted.Diagnostics, d => d.Severity is "fatal" or "error");
    }

    [Theory]
    [InlineData("Boss ignored { profile: Missing; height: 1mm }")]
    [InlineData("Pmi { Datum ignored { target: face(+Z) } }")]
    public void CoaxialCompositionRejectsUnconsumedRootOperations(string statement)
    {
        var source = CoaxialSource.Replace("units: mm", "units: mm\n" + statement, StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d == "firmament-compose-coaxial-consumer-unsupported");
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void CoaxialRequirementsUseTheCommonCompileTimeBinder()
    {
        var source = CoaxialSource.Replace("units: mm", "units: mm\nRequire checked => true", StringComparison.Ordinal);
        var accepted = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(accepted.IsSuccess, string.Join("\n", accepted.Diagnostics));
        var rejected = FirmamentBuildAndExport.CompileSource(source.Replace("=> true", "=> false", StringComparison.Ordinal));
        Assert.False(rejected.IsSuccess);
        Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("requirement-failed", StringComparison.Ordinal) || d.Message.Contains("static-require-failed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("half", "[2.85mm, 12.85mm]")]
    [InlineData("full", "[2.85mm, 0.25mm]")]
    public void AxisTouchingPartialAndNonmonotoneProfilesRemainRejected(string angle, string station)
    {
        var source = File.ReadAllText(PathOf("solid-fastener-revolve.firmament"))
            .Replace("angle: full", "angle: " + angle, StringComparison.Ordinal)
            .Replace("[2.85mm, 12.85mm]", station, StringComparison.Ordinal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.StartsWith("firmament-revolve-axis-touch-materialization-not-supported", StringComparison.Ordinal));
    }

    private const string CoaxialSource = """
        Model Coaxial {
            units: mm
            Compose Body {
                Cylinder shaft { radius: 3mm; height: 10mm }
                Frustum head { on: shaft.Top; bottomRadius: 3mm; topRadius: 6mm; height: 3mm }
            }
            Modify Body { EdgeFinish<Chamfer> tip { on: shaft.Bottom; target: Boundary; distance: 0.5mm } }
        }
        """;

    private static RuledSurfacePatch ArcPatch(double radiusA, double radiusB, double phaseB, double sweepB)
    {
        var a = new RuledBoundary.Arc("a", new(0, 0, 0), Direction3D.Create(new Vector3D(0, 0, 1)), radiusA, Direction3D.Create(new Vector3D(1, 0, 0)), 0, Math.PI / 2);
        var b = new RuledBoundary.Arc("b", new(0, 0, 10), a.Normal, radiusB, a.ReferenceAxis, phaseB, sweepB);
        return RuledSurfaceLowering.Lower(new("test", RuledConstructionKind.RuledSurface, a, b,
            new("a", "test", "boundary-a"), new("b", "test", "boundary-b"))).Patch!;
    }
    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-8;
    private static BrepBody BuildBody(string file)
    {
        var built = FirmamentBuildAndExport.CompileSource(File.ReadAllText(PathOf(file)), Path.GetDirectoryName(PathOf(file)));
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
        return imported.Value;
    }
    private static string PathOf(string file) => FirmamentCorpusHarness.ResolveFixtureFullPath(Root + file);
    private static AssemblyM1CompilationResult Compile(string file)
    {
        var compiled = new AssemblyM1Pipeline().CompileFile(PathOf(file));
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
        return compiled;
    }
}
