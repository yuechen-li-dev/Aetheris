using Aetheris.FEA.Firmament;
using Aetheris.FEA.Mechanics;
using Aetheris.Kernel.StandardLibrary.Materials;

namespace Aetheris.FEA.Tests;

public sealed class MaterialCatalogFirmamentTests
{
    private const string Source = """
        model CatalogMaterialWitness {
            units mm
            solid coupon: Box { size: [80, 20, 2] }
            analysis LinearElastic CouponPull {
                body: coupon
                material: Standard.Materials.Aluminum.5052_H32
                fixed Clamp { region: coupon.face(-X)
                    components: [X, Y, Z] }
                force Pull { region: coupon.face(+X)
                    vector: [1000N, 0N, 0N] }
                lattice: [8, 2, 1]
            }
        }
        """;

    [Fact]
    public void FirmamentReference_ResolvesAndSurvivesLoweringAsFeaReadyMaterial()
    {
        var compilation = FirmamentAnalysisCompiler.Compile(Source);
        Assert.True(compilation.IsSuccess, string.Join("; ", compilation.Diagnostics.Select(x => x.Code + ":" + x.Message)));
        var material = Assert.Single(compilation.Analysis!.Materials);
        Assert.Equal("standard:aluminum/5052-h32", material.StableMaterialId);
        Assert.Equal(MaterialConstitutiveClass.LinearElasticIsotropic, material.ConstitutiveClass);
        Assert.Equal(70.3e9, material.YoungsModulusPascal);
        Assert.Equal(.33, material.PoissonRatio);
        Assert.Equal(2680, material.DensityKilogramsPerCubicMeter);
        Assert.Equal(193e6, material.YieldStrengthPascal);
        Assert.NotNull(material.CatalogMaterial);
        Assert.Equal("Pa", material.CatalogMaterial!.Structural!.YoungsModulus.UnitSymbol);
    }

    [Fact]
    public void CatalogMaterial_TravelsThroughTheRealLinearElasticSolver()
    {
        var analysis = FirmamentAnalysisCompiler.Compile(Source).Analysis!;
        var result = LinearElasticSolver.Solve(analysis, new(CutCellQuadraturePerAxis: 4, RelativeResidualTolerance: 1e-8));
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(x => x.Code + ":" + x.Message)));
        Assert.True(result.Solver.Converged);
        Assert.True(result.MaximumDisplacementMeters > 0);
    }

    [Fact]
    public void UnknownFirmamentMaterial_ProducesDeterministicDiagnostic()
    {
        var compilation = FirmamentAnalysisCompiler.Compile(Source.Replace("5052_H32", "Unobtainium_X9", StringComparison.Ordinal));
        Assert.False(compilation.IsSuccess);
        var diagnostic = Assert.Single(compilation.Diagnostics, x => x.Code == "firmament-material-unknown");
        Assert.Contains("Standard.Materials.Aluminum.Unobtainium_X9", diagnostic.Message);
    }

    [Theory]
    [InlineData(MaterialResolutionError.AmbiguousMaterial, "firmament-material-ambiguous")]
    [InlineData(MaterialResolutionError.MissingRequiredStructuralProperty, "fea-material-missing-structural-properties")]
    [InlineData(MaterialResolutionError.InvalidMaterialData, "firmament-material-invalid")]
    public void ProviderFailures_MapToStableFirmamentDiagnostics(MaterialResolutionError error, string expectedCode)
    {
        var compilation = FirmamentAnalysisCompiler.Compile(Source, materialResolver: new FailingResolver(error));
        Assert.False(compilation.IsSuccess);
        Assert.Contains(compilation.Diagnostics, x => x.Code == expectedCode);
    }

    private sealed class FailingResolver(MaterialResolutionError error) : IMaterialResolver
    {
        public MaterialResolutionResult Resolve(string reference) => MaterialResolutionResult.Failure(error, $"test failure for {reference}");
    }
}
