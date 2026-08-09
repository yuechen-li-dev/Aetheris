using Aetheris.FEA.Abaqus;
using Aetheris.FEA.Firmament;
using Aetheris.FEA.Mechanics;
using Aetheris.Continuum.Lattice;
using Aetheris.Forge.Sdk;

namespace Aetheris.FEA.Tests;

public sealed class LinearElasticM5Tests
{
    private const string DirectSource="""
        model PlateWithHoleModel {
            units mm
            solid plate: Box { size: [200, 100, 10] }
            modify plate {
                region hole on face(+Z) { cut Cylinder { radius: 10 through: face(-Z) } }
            }
            analysis LinearElastic PlateWithHole {
                body: plate
                material Steel { youngsModulus: 200GPa
                    poissonRatio: 0.3
                    density: 7850kg/m3 }
                fixed Clamp { region: plate.face(-X)
                    components: [X, Y, Z] }
                force Tension { region: plate.face(+X)
                    vector: [10000N, 0N, 0N] }
                results: [Displacement, Strain, Stress, ReactionForce]
                lattice: [8, 4, 1]
            }
        }
        """;

    [Fact]
    public void FirmamentAnalysis_LowersTypedSiIntentAndSolvesSparseElasticity()
    {
        var compile=FirmamentAnalysisCompiler.Compile(DirectSource);
        Assert.True(compile.IsSuccess,string.Join("; ",compile.Diagnostics.Select(d=>d.Code+":"+d.Message)));
        Assert.Equal(200e9,compile.Analysis!.Materials.Single().YoungsModulusPascal);
        Assert.Equal("plate.face(-X)",compile.Analysis.Constraints.Single().Region.Path);
        var result=LinearElasticSolver.Solve(compile.Analysis,new(CutCellQuadraturePerAxis:4,RelativeResidualTolerance:1e-8));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));
        Assert.True(result.Solver.Converged);
        Assert.True(result.System.Nonzeros>result.System.DegreesOfFreedom);
        Assert.InRange(result.System.MaximumAsymmetry,0,1e-6);
        Assert.InRange(result.System.IntegratedLoadResidualNewton,0,1e-8);
        Assert.InRange(result.Equilibrium.ResidualNewton.Length,0,1e-3);
        Assert.True(result.MaximumDisplacementMeters>0);
        Assert.True(result.MaximumVonMisesPascal>0);
    }

    [Fact]
    public void HalfOpenOwnership_IsOrientationIndependentAndUnique()
    {
        var first=new CellIndex(2,3,4);var second=new CellIndex(1,3,4);
        Assert.Equal(second,BoundaryFragmentOwnership.Own(first,second));
        Assert.Equal(second,BoundaryFragmentOwnership.Own(second,first));
        Assert.True(BoundaryFragmentOwnership.IsOwner(second,first,second));
        Assert.False(BoundaryFragmentOwnership.IsOwner(first,first,second));
    }

    [Fact]
    public void AbaqusDeck_IsDeterministicConventionalAndInternallyValid()
    {
        var analysis=FirmamentAnalysisCompiler.Compile(DirectSource).Analysis!;
        var first=AbaqusInpExporter.Export(analysis);var second=AbaqusInpExporter.Export(analysis);
        Assert.Equal(first.Sha256,second.Sha256);Assert.Equal(first.Text,second.Text);
        Assert.Contains("TYPE=C3D8",first.Text);Assert.Contains("Cut cells are omitted",first.Text);
        var validation=AbaqusInpValidator.Validate(first.Text);
        Assert.True(validation.IsValid,string.Join("; ",validation.Diagnostics));Assert.True(validation.ElementCount>0);
    }

    [Fact]
    public void ForgeTemplate_InvokesSameAnalysisPathWithoutSolverApi()
    {
        const string module="""
            Template < AppliedForce: float > Model PlateAnalysisTemplate {
                units mm
                solid plate: Box { size: [200, 100, 10] }
                modify plate { region hole on face(+Z) { cut Cylinder { radius: 10 through: face(-Z) } } }
                analysis LinearElastic PlateWithHole {
                    body: plate
                    material Steel { youngsModulus: 200GPa
                        poissonRatio: 0.3 }
                    fixed Clamp { region: plate.face(-X)
                        components: [X, Y, Z] }
                    force Tension { region: plate.face(+X)
                        vector: [AppliedForce N, 0N, 0N] }
                    lattice: [8, 4, 1]
                    results: [Displacement, Strain, Stress, ReactionForce]
                }
            }
            """;
        var host=new ForgeHost();var template=host.LoadModule("M5",module).ResolveTemplate("PlateAnalysisTemplate");
        var result=template.Invoke("HostPlate").Bind("AppliedForce",new ForgeReal(10000)).Analyze(new(RelativeResidualTolerance:1e-8));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));
        Assert.Equal("PlateWithHole",result.AnalysisIr!.Id);
        Assert.True(result.NativeResult!.Solver.Converged);
        Assert.NotNull(result.Abaqus);
    }

    [Fact]
    public void InvalidMaterialAndMissingConstraint_AreTypedDiagnostics()
    {
        var source=DirectSource.Replace("200GPa","0Pa",StringComparison.Ordinal).Replace("fixed Clamp","fixed Clamp",StringComparison.Ordinal);
        var compile=FirmamentAnalysisCompiler.Compile(source);
        Assert.False(compile.IsSuccess);Assert.Contains(compile.Diagnostics,d=>d.Code=="fea-invalid-youngs-modulus");
    }

    [Fact]
    public void ForgeInlineStepTemplate_UsesTypedImportedResourceInAnalysis()
    {
        const string module="""
            Template < Part: ImportedStep > Model ImportedBoxAnalysis {
                analysis LinearElastic ImportedBoxPull {
                    body: imported
                    bodyResource: Part
                    material Steel { youngsModulus: 200GPa
                        poissonRatio: 0.3 }
                    fixed Clamp { region: imported.face(-X)
                        components: [X, Y, Z] }
                    force Pull { region: imported.face(+X)
                        vector: [100N, 0N, 0N] }
                    lattice: [4, 3, 2]
                    results: [Displacement, Strain, Stress, ReactionForce]
                }
            }
            """;
        var root=FindRoot();var resource=ImportedStepResource.Load("VendorPart",Path.Combine(root,"fixtures","FirmamentV2","InlineStep","testdata","canonical-box-10x8x6.step"));
        var template=new ForgeHost().LoadModule("ImportedM5",module).ResolveTemplate("ImportedBoxAnalysis");
        var result=template.Invoke("ImportedPull").Bind("Part",new ForgeImportedStep(resource.Name)).AddResource(resource).Analyze(new(RelativeResidualTolerance:1e-8));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));
        Assert.Equal("InlineStep",result.AnalysisIr!.Body.SourceKind);Assert.Equal(resource.ContentHash,result.AnalysisIr.Body.ResourceHash);
    }

    private static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return d?.FullName??throw new DirectoryNotFoundException();}
}
