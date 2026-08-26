using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class KnotPathTests
{
    [Theory]
    [InlineData("trefoil.firmament", WireKnotFamily.Trefoil, null, null)]
    [InlineData("figure-eight.firmament", WireKnotFamily.FigureEight, null, null)]
    [InlineData("torus-knot-3-5.firmament", WireKnotFamily.TorusKnot, 3, 5)]
    public void NamedFamiliesRetainPeriodicSemanticIdentity(string fixture, WireKnotFamily family, int? p, int? q)
    {
        var parsed = Parse(fixture); var knot = Assert.IsType<WireKnotPathAir>(Assert.Single(parsed.Operations));
        Assert.Equal(family, knot.Family); Assert.Equal(p, knot.P); Assert.Equal(q, knot.Q); Assert.Equal(1, knot.ComponentCount); Assert.True(knot.Closed); Assert.Equal(0d, knot.SeamParameter);
        Assert.True((knot.Evaluate(0d) - knot.Evaluate(1d)).Length < 1e-10);
        Assert.True(Dot(knot.Tangent(0d), knot.Tangent(1d)) > 1d - 1e-12);
        Assert.True(knot.ApproximationError.MaxMm <= knot.ApproximationToleranceMm);
        Assert.True(knot.Qualification.MinimumNonlocalDistanceMm > 0d); Assert.True(knot.Qualification.MinimumCurvatureRadiusMm > 0d);
        Assert.True(knot.Qualification.TubeRadiusLimitMm > parsed.WireRadiusMm);
    }

    [Theory]
    [InlineData("trefoil.firmament")]
    [InlineData("figure-eight.firmament")]
    [InlineData("torus-knot-3-5.firmament")]
    public void ClosedTubeHasNoCapsAndExportsPcurveCompletePolynomialStep(string fixture)
    {
        var built = WireFormBRepMaterializer.Build(Parse(fixture)); Assert.True(built.IsSuccess, Messages(built.Diagnostics));
        Assert.DoesNotContain(built.Value.Body.Geometry.Surfaces, surface => surface.Value.Kind == SurfaceGeometryKind.Plane);
        Assert.All(built.Value.Body.Topology.Edges, edge => Assert.Equal(2, built.Value.Body.Topology.Coedges.Count(use => use.EdgeId == edge.Id)));
        var pcurves = BrepPcurveValidator.Validate(built.Value.Body, 1e-6, true); Assert.True(pcurves.IsValid, string.Join(Environment.NewLine, pcurves.Diagnostics)); Assert.Equal(built.Value.Body.Topology.Coedges.Count(), pcurves.PcurveCount);
        Assert.All(built.Value.Body.Geometry.Surfaces, surface => Assert.Equal(SurfaceGeometryKind.BSplineSurfaceWithKnots, surface.Value.Kind));
        var step = Step242Exporter.ExportBody(built.Value.Body); Assert.True(step.IsSuccess, Messages(step.Diagnostics)); Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", step.Value, StringComparison.Ordinal);
        var imported = Step242Importer.ImportBody(step.Value); Assert.True(imported.IsSuccess, Messages(imported.Diagnostics));
        Assert.All(imported.Value!.Topology.Edges, edge => Assert.Equal(2, imported.Value.Topology.Coedges.Count(use => use.EdgeId == edge.Id)));
    }

    [Fact]
    public void ParallelTransportHolonomyIsMeasuredAndCorrectedAtTheSeam()
    {
        var knot = Assert.IsType<WireKnotPathAir>(Parse("trefoil.firmament").Operations.Single());
        Assert.True(Math.Abs(knot.FrameClosure.RawClosureRotationRadians) > 0.1d);
        Assert.Equal(-knot.FrameClosure.RawClosureRotationRadians, knot.FrameClosure.AppliedCorrectionRadians, 12);
        Assert.True(knot.FrameClosure.FinalClosureErrorRadians < 1e-12);
    }

    [Theory]
    [InlineData("torus-knot-non-coprime.firmament", "wireform-knot-not-single-component")]
    [InlineData("invalid-pq.firmament", "wireform-knot-parameters-invalid")]
    [InlineData("degenerate-scale.firmament", "wireform-knot-scale-invalid")]
    public void InvalidKnotAuthoringFailsTyped(string fixture, string code)
    {
        var parsed = WireFormAuthoring.Parse(File.ReadAllText(InvalidFixture(fixture))); Assert.False(parsed.IsSuccess); Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Message.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void TubeLimitAdmitsThinAndNearLimitButRejectsTooThickWithoutStep()
    {
        foreach (var fixture in new[] { "trefoil-thin.firmament", "trefoil-near-limit.firmament" }) Assert.True(WireFormBRepMaterializer.Build(Parse(fixture)).IsSuccess);
        var source = File.ReadAllText(InvalidFixture("trefoil-wire-too-thick.firmament")); var parsed = WireFormAuthoring.Parse(source); Assert.True(parsed.IsSuccess);
        var diagnostics = WireFormBRepMaterializer.Validate(parsed.Value); Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("wireform-knot-tube-self-intersection", StringComparison.Ordinal) && diagnostic.Contains("admitted maximum", StringComparison.Ordinal));
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void UniformScaleAndRigidOrientationPreserveIntrinsicQualification()
    {
        var canonicalSource = File.ReadAllText(Fixture("trefoil.firmament")); var canonical = Assert.IsType<WireKnotPathAir>(WireFormAuthoring.Parse(canonicalSource).Value.Operations.Single());
        var doubled = Assert.IsType<WireKnotPathAir>(WireFormAuthoring.Parse(canonicalSource.Replace("Diameter: 6mm", "Diameter: 12mm", StringComparison.Ordinal).Replace("Scale: 20mm", "Scale: 40mm", StringComparison.Ordinal)).Value.Operations.Single());
        Assert.Equal(2d, doubled.LengthMm / canonical.LengthMm, 9); Assert.Equal(2d, doubled.Qualification.MinimumNonlocalDistanceMm / canonical.Qualification.MinimumNonlocalDistanceMm, 9); Assert.Equal(2d, doubled.Qualification.MinimumCurvatureRadiusMm / canonical.Qualification.MinimumCurvatureRadiusMm, 9);
        var rotated = Assert.IsType<WireKnotPathAir>(Parse("rotated-trefoil.firmament").Operations.Single());
        Assert.Equal(canonical.LengthMm, rotated.LengthMm, 9); Assert.Equal(canonical.Qualification.MinimumNonlocalDistanceMm, rotated.Qualification.MinimumNonlocalDistanceMm, 9); Assert.Equal(canonical.Qualification.MinimumCurvatureRadiusMm, rotated.Qualification.MinimumCurvatureRadiusMm, 9);
        Assert.True((canonical.Evaluate(0d) - rotated.Evaluate(0d)).Length > 1d);
    }

    [Fact]
    public void RepeatExportAndStockAccountingAreDeterministic()
    {
        var source = File.ReadAllText(Fixture("trefoil.firmament")); var first = FirmamentBuildAndExport.CompileSource(source); var second = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(first.IsSuccess, Messages(first.Diagnostics)); Assert.True(second.IsSuccess, Messages(second.Diagnostics)); Assert.Equal(first.Value.StepText, second.Value.StepText);
        var report = first.Value.WireForm!; Assert.Equal("Trefoil", report.KnotFamily); Assert.Equal(0, report.Planes); Assert.Equal(0, report.RationalProductSurfaces); Assert.Equal(0, report.FacetedFallback); Assert.True(report.PcurveCount > 0); Assert.True(report.MaximumPcurveErrorMm < 1e-6);
        Assert.Equal(Math.PI * 9d * report.TotalWireLengthMm, report.VolumeMm3, 8); Assert.True(report.MassKilograms > 0d);
    }

    private static WireFormFeatureAir Parse(string fixture)
    {
        var result = WireFormAuthoring.Parse(File.ReadAllText(Fixture(fixture))); Assert.True(result.IsSuccess, Messages(result.Diagnostics)); return result.Value;
    }
    private static double Dot(Aetheris.Kernel.Core.Math.Direction3D a, Aetheris.Kernel.Core.Math.Direction3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static string Messages(IEnumerable<Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic> diagnostics) => string.Join(Environment.NewLine, diagnostics.Select(x => x.Message));
    private static string Fixture(string name) => Path.Combine(RepoRoot(), "fixtures", "Canonical", "WireForm", "Knot", name);
    private static string InvalidFixture(string name) => Path.Combine(RepoRoot(), "fixtures", "Invalid", "WireForm", "Knot", name);
    private static string RepoRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
