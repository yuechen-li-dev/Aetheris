using Aetheris.Continuum.Lattice;
using Aetheris.FEA.Analysis;
using Aetheris.FEA.Firmament;
using Aetheris.FEA.Geometry;
using Aetheris.FEA.Mechanics;
using Aetheris.Kernel.StandardLibrary.Materials;

namespace Aetheris.FEA.Tests;

public sealed class ProductionFeaX1Tests
{
    private const string CanonicalBeam = """
        Model CantileverWitness {
            Units: mm
            Box beam { Size: [120mm, 20mm, 20mm] }
            Analysis LinearElastic Cantilever {
                Body: beam
                Material: Standard.Materials.Aluminum.6061_T6
                Fixed Root {
                    Region: beam.face(-X)
                    Components: [X, Y, Z]
                }
                Force Tip {
                    Region: beam.face(+X)
                    Vector: [0N, -100N, 0N]
                }
                Results: [Displacement, Strain, Stress, ReactionForce]
                Lattice: [12, 2, 2]
            }
        }
        """;

    [Fact]
    public void CanonicalAndLegacySemanticConstructCasing_LowerIdentically()
    {
        var canonical = FirmamentAnalysisCompiler.Compile(CanonicalBeam);
        var legacy = FirmamentAnalysisCompiler.Compile(CanonicalBeam
            .Replace("Analysis LinearElastic", "analysis LinearElastic", StringComparison.Ordinal)
            .Replace("Fixed Root", "fixed Root", StringComparison.Ordinal)
            .Replace("Force Tip", "force Tip", StringComparison.Ordinal)
            .Replace("Body:", "body:", StringComparison.Ordinal)
            .Replace("Material:", "material:", StringComparison.Ordinal)
            .Replace("Region:", "region:", StringComparison.Ordinal)
            .Replace("Components:", "components:", StringComparison.Ordinal)
            .Replace("Vector:", "vector:", StringComparison.Ordinal)
            .Replace("Results:", "results:", StringComparison.Ordinal)
            .Replace("Lattice:", "lattice:", StringComparison.Ordinal));
        Assert.True(canonical.IsSuccess, Describe(canonical));
        Assert.True(legacy.IsSuccess, Describe(legacy));
        Assert.Equal(canonical.Analysis!.Kind, legacy.Analysis!.Kind);
        Assert.Equal(canonical.Analysis.Constraints.Select(item=>(item.Id,item.Region.Path,string.Join(',',item.Components.Order()))),
            legacy.Analysis.Constraints.Select(item=>(item.Id,item.Region.Path,string.Join(',',item.Components.Order()))));
        Assert.Equal(canonical.Analysis.Loads.Select(item=>(item.Id,item.Kind,item.Region.Path,item.VectorSi,item.Distribution)),
            legacy.Analysis.Loads.Select(item=>(item.Id,item.Kind,item.Region.Path,item.VectorSi,item.Distribution)));
        Assert.Equal(LoadDistributionPolicy.TotalResultantOverSelectedArea, canonical.Analysis.Loads.Single().Distribution);
    }

    [Fact]
    public void CantileverScalingAndRefinement_ArePhysicallySensibleAndBalanced()
    {
        var source = FirmamentAnalysisCompiler.Compile(CanonicalBeam).Analysis!;
        var baseline = Solve(source);
        var twiceLoad = Solve(source with { Loads = source.Loads.Select(load => load with { VectorSi = load.VectorSi * 2 }).ToArray() });
        var twiceE = Solve(source with { Materials = source.Materials.Select(material => material with { YoungsModulusPascal = material.YoungsModulusPascal * 2 }).ToArray() });
        Assert.InRange(twiceLoad.MaximumDisplacementMeters / baseline.MaximumDisplacementMeters, 1.999, 2.001);
        Assert.InRange(twiceE.MaximumDisplacementMeters / baseline.MaximumDisplacementMeters, .499, .501);
        Assert.InRange(baseline.Equilibrium.ResidualNewton.Length, 0, 1e-5);

        var studies = new[] { (6, 2, 2), (12, 2, 2), (18, 3, 3) }.Select(counts =>
            Solve(source with { Lattice = new(source.Body.ContinuumRegion.Bounds, counts.Item1, counts.Item2, counts.Item3) })).ToArray();
        Assert.All(studies, result => Assert.True(result.IsSuccess, Describe(result)));
        Assert.All(studies, result => Assert.True(result.MaximumDisplacementMeters > 0 && double.IsFinite(result.MaximumDisplacementMeters)));
        Assert.All(studies, result => Assert.InRange(result.Equilibrium.ResidualNewton.Length, 0, 1e-4));
        Assert.True(studies[^1].System.DegreesOfFreedom > studies[0].System.DegreesOfFreedom);
    }

    [Fact]
    public void InlineStepThroughHole_UsesStableFaceIdentityAndSolvesSharedCutCellPath()
    {
        var root = FindRoot();
        var path = Path.Combine(root, "fixtures", "FEA", "inline-step-through-hole.firmament");
        var compilation = FirmamentAnalysisCompiler.Compile(File.ReadAllText(path), path, Path.GetDirectoryName(path));
        Assert.True(compilation.IsSuccess, Describe(compilation));
        Assert.IsType<ImportedBrepAnalysisRegion>(compilation.Analysis!.Body.ContinuumRegion);
        Assert.NotNull(compilation.Analysis.Body.ResourceHash);
        Assert.Equal("6", compilation.Analysis.Constraints.Single().Region.ExactBrepFaceId);
        Assert.Equal("4", compilation.Analysis.Loads.Single().Region.ExactBrepFaceId);
        var result = LinearElasticSolver.Solve(compilation.Analysis, new(CutCellQuadraturePerAxis: 4, RelativeResidualTolerance: 1e-8, RetryEmptyCutCells: false));
        Assert.True(result.IsSuccess, Describe(result));
        Assert.True(result.System.CutCells > 0);
        Assert.True(result.MaximumDisplacementMeters > 0);
        Assert.InRange(result.Equilibrium.ResidualNewton.Length, 0, 1e-3);
        Assert.Equal(500, result.BoundaryLoads!.Single().IntegratedResultant.Length, 6);
    }

    [Fact]
    public void InlineStepUnknownFace_IsAStableActionableDiagnostic()
    {
        var root = FindRoot();
        var path = Path.Combine(root, "fixtures", "FEA", "inline-step-through-hole.firmament");
        var source = File.ReadAllText(path).Replace("body.face(#170)", "body.face(#999999)", StringComparison.Ordinal);

        var compilation = FirmamentAnalysisCompiler.Compile(source, path, Path.GetDirectoryName(path));

        Assert.False(compilation.IsSuccess);
        var diagnostic = Assert.Single(compilation.Diagnostics, item => item.Code == "firmament-analysis-inline-step-face-missing");
        Assert.Contains("body.face(#999999)", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("body.face(#141)", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidLatticeAndResultRequest_AreStableDiagnostics()
    {
        var lattice = FirmamentAnalysisCompiler.Compile(CanonicalBeam.Replace("[12, 2, 2]", "[12, 0, 2]", StringComparison.Ordinal));
        Assert.Contains(lattice.Diagnostics, item => item.Code == "fea-invalid-lattice-dimensions");
        var result = FirmamentAnalysisCompiler.Compile(CanonicalBeam.Replace("Stress,", "MysteryField,", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, item => item.Code == "fea-invalid-result-request");
    }

    [Fact]
    public void RequestedResults_ControlPublishedTypedFields()
    {
        var analysis=FirmamentAnalysisCompiler.Compile(CanonicalBeam).Analysis!;
        var result=Solve(analysis with{RequestedFields=new HashSet<AnalysisResultField>{AnalysisResultField.Displacement}});
        Assert.NotEmpty(result.Displacements);
        Assert.Empty(result.CellFields);
        Assert.Empty(result.StrainFields!);
        Assert.Empty(result.StressFields!);
        Assert.Empty(result.Reactions);
        Assert.True(result.Equilibrium.ReactionForceNewton.Length>0);
    }

    [Fact]
    public void UnsupportedConstitutiveClass_IsRejectedBeforeAssembly()
    {
        var analysis=FirmamentAnalysisCompiler.Compile(CanonicalBeam).Analysis!;
        analysis=analysis with{Materials=analysis.Materials.Select(material=>material with{ConstitutiveClass=MaterialConstitutiveClass.Orthotropic}).ToArray()};
        var result=LinearElasticSolver.Solve(analysis);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,item=>item.Code=="fea-unsupported-constitutive-class");
    }

    [Fact]
    public void ImportedBodyWithAmbiguousKernelContainment_FailsBeforeSolver()
    {
        var root=FindRoot();var directory=Path.Combine(root,"fixtures", "Assembly", "LegacyImports", "examples","occt-l-bracket");
        const string source="""
            Analysis LinearElastic UnsupportedImportedBracket {
                body: inlineSTEP("_part_003_l_bracket.step")
                material: Standard.Materials.Aluminum.6061_T6
                Fixed Mount { region: body.face(#100) }
                Force Load { region: body.face(#473) vector: [0N, -500N, 0N] }
                lattice: [2, 2, 3]
            }
            """;
        var compilation=FirmamentAnalysisCompiler.Compile(source,"ambiguous-import.firmament",directory);
        Assert.Contains(compilation.Diagnostics,item=>item.Code=="firmament-analysis-inline-step-containment-unsupported");
    }

    private static LinearElasticAnalysisResult Solve(LinearElasticAnalysisIr analysis)
    {
        var result = LinearElasticSolver.Solve(analysis, new(CutCellQuadraturePerAxis: 4, RelativeResidualTolerance: 1e-8));
        Assert.True(result.IsSuccess, Describe(result));
        return result;
    }

    private static string Describe(FirmamentAnalysisCompilation value) => string.Join("; ", value.Diagnostics.Select(item => item.Code + ":" + item.Message));
    private static string Describe(LinearElasticAnalysisResult value) => string.Join("; ", value.Diagnostics.Select(item => item.Code + ":" + item.Message));
    private static string FindRoot(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Aetheris.slnx")))directory=directory.Parent;return directory?.FullName??throw new DirectoryNotFoundException();}
}
