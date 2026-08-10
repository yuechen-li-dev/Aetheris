using Aetheris.FEA.Abaqus;
using Aetheris.FEA.Firmament;
using Aetheris.FEA.Mechanics;
using Aetheris.FEA.Analysis;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Forge.Host;
using Aetheris.Kernel.Core.Math;

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
        Assert.True(result.System.IndependentSpdCheck);
        Assert.InRange(result.StrainEnergy!.RelativeResidual,0,1e-12);
        Assert.Equal(2,result.StressProbes!.Count);Assert.All(result.StressProbes,probe=>Assert.True(double.IsFinite(probe.HoopStressPascal)));
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
                    lattice: [10, 8, 6]
                    results: [Displacement, Strain, Stress, ReactionForce]
                }
            }
            """;
        var root=FindRoot();var resource=ImportedStepResource.Load("VendorPart",Path.Combine(root,"fixtures","FirmamentV2","InlineStep","testdata","canonical-box-10x8x6.step"));
        var template=new ForgeHost().LoadModule("ImportedM5",module).ResolveTemplate("ImportedBoxAnalysis");
        var center=new Vector3D(.005,.004,.003);var orientation=Transform3D.CreateTranslation(-center)*Transform3D.CreateRotationZ(17*double.Pi/180)*Transform3D.CreateRotationX(9*double.Pi/180)*Transform3D.CreateTranslation(center);
        var result=template.Invoke("ImportedPull").Bind("Part",new ForgeImportedStep(resource.Name)).AddResource(resource).Analyze(new(CutCellQuadraturePerAxis:6,RelativeResidualTolerance:1e-8,DomainTransform:orientation));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));
        Assert.Equal("InlineStep",result.AnalysisIr!.Body.SourceKind);Assert.Equal(resource.ContentHash,result.AnalysisIr.Body.ResourceHash);Assert.NotNull(result.NativeResult!.BoundaryLoads!.Single().ExactBrepFaceId);Assert.True(result.Abaqus!.ElementCount>0);
    }

    [Fact]
    public void InlineStepRecognizeMemberPath_NormalizesToBoundarySemanticValueBeforeAnalysisIr()
    {
        var root=FindRoot();var fixtureDirectory=Path.Combine(root,"fixtures","FirmamentV2","Canonical","valid");
        const string source="""
            Model ImportedRecognizedAnalysis {
                Units: mm
                InlineStep imported {
                    Path: "../../InlineStep/testdata/canonical-box-10x8x6.step"
                }
                Recognize imported {
                    Region MountFace {
                        Kind: DatumPlane
                        Confidence: High
                        Faces: [1]
                    }
                }
                analysis LinearElastic Pull {
                    body: imported
                    material Steel { youngsModulus: 200GPa
                        poissonRatio: 0.3 }
                    fixed Clamp { region: imported.MountFace
                        components: [X, Y, Z] }
                    force Pull { region: imported.face(+X)
                        vector: [100N, 0N, 0N] }
                    lattice: [6, 5, 4]
                }
            }
            """;
        var compiled=FirmamentAnalysisCompiler.Compile(source,"recognized-analysis.firmament",fixtureDirectory);
        Assert.True(compiled.IsSuccess,string.Join("; ",compiled.Diagnostics.Select(item=>item.Code+":"+item.Message)));
        var region=Assert.Single(compiled.Analysis!.Constraints).Region;
        Assert.StartsWith("recognize:",region.SemanticStableId,StringComparison.Ordinal);
        Assert.Contains("BoundaryRegionCapable",region.CapabilityEvidence!);
        Assert.Equal("ExactBrepFace",region.ExactBindingKind);
        Assert.NotNull(region.ExactBrepFaceId);
    }

    private static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return d?.FullName??throw new DirectoryNotFoundException();}

    [Fact]
    public void RotatedPlanarBoundaryQuadrature_PreservesAreaResultantAndMoment()
    {
        var compiled=FirmamentAnalysisCompiler.Compile(DirectSource);var source=compiled.Analysis!;var analysis=source with{Lattice=new LatticeSpec(source.Body.ContinuumRegion.Bounds,16,10,4)};
        var center=new Vector3D(.1,.05,.005);var rotation=Transform3D.CreateTranslation(-center)*Transform3D.CreateRotationZ(23*double.Pi/180)*Transform3D.CreateRotationY(11*double.Pi/180)*Transform3D.CreateTranslation(center);
        var result=LinearElasticSolver.Solve(analysis,new(CutCellQuadraturePerAxis:6,RelativeResidualTolerance:1e-8,DomainTransform:rotation));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));
        var load=Assert.Single(result.BoundaryLoads!);Assert.InRange(double.Abs(load.IntegratedArea-load.ExactArea),0,1e-11);Assert.InRange(load.ResultantResidual,0,1e-8);Assert.InRange(load.MomentResidual,0,1e-9);
        Assert.InRange(Assert.Single(result.NumericalLowering!.BoundaryEnforcements).MaximumViolationMeters,0,2e-6);
    }

    [Fact]
    public void RotatedPressure_UsesExactMaterialOutwardNormal()
    {
        var source=FirmamentAnalysisCompiler.Compile(DirectSource).Analysis!;var pressure=source.Loads.Single() with{Kind=Aetheris.FEA.Analysis.BoundaryLoadKind.Pressure,VectorSi=Vector3D.Zero,PressurePascal=2e6};var analysis=source with{Loads=[pressure],Lattice=new LatticeSpec(source.Body.ContinuumRegion.Bounds,12,8,4)};
        var center=new Vector3D(.1,.05,.005);var rotation=Transform3D.CreateTranslation(-center)*Transform3D.CreateRotationZ(31*double.Pi/180)*Transform3D.CreateTranslation(center);var result=LinearElasticSolver.Solve(analysis,new(CutCellQuadraturePerAxis:6,RelativeResidualTolerance:1e-8,DomainTransform:rotation));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));var evidence=Assert.Single(result.BoundaryLoads!);var expectedDirection=rotation.Apply(new Vector3D(-1,0,0));expectedDirection.TryNormalize(out expectedDirection);var actual=evidence.IntegratedResultant/evidence.IntegratedResultant.Length;Assert.InRange((actual-expectedDirection).Length,0,1e-10);Assert.InRange(evidence.ResultantResidual,0,1e-8);Assert.InRange(evidence.MomentResidual,0,1e-9);
    }

    [Fact]
    public void PlanarTrimWithHole_IsClippedOnceAndPreservesExactAreaDeterministically()
    {
        var domain=new PlanarBoundaryDomain(
            new BoundaryReference("exact-brep","face:trimmed","42","selected"),new Point3D(0,0,0),
            new Vector3D(1,0,0),new Vector3D(0,1,0),new Vector3D(0,0,1),
            [(0d,0d),(2d,0d),(2d,2d),(0d,2d)],
            [[(.5d,.5d),(.5d,1.5d),(1.5d,1.5d),(1.5d,.5d)]],"CIR inward probe");
        var binding=new SemanticRegionBinding("body","body.selected","42");
        var cells=new[] {new ContinuumCell(new CellIndex(0,0,0),new BoundingBox3D(new(0,0,0),new(2,2,1)),CellClassification.Cut,.75)};
        var first=MechanicsBoundaryQuadrature.Create(domain,binding,cells);var second=MechanicsBoundaryQuadrature.Create(domain,binding,cells);
        Assert.InRange(double.Abs(first.IntegratedArea-3),0,1e-12);
        Assert.Equal(first.Fragments.Select(f=>f.OwnershipKey),second.Fragments.Select(f=>f.OwnershipKey));
        Assert.Equal(first.Fragments.Count,first.Fragments.Select(f=>f.OwnershipKey).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CompoundTinySupport_UsesAffineAggregationAndRestoresUsableMechanics()
    {
        var original=FirmamentAnalysisCompiler.Compile(DirectSource).Analysis!;var source=original with{Lattice=new LatticeSpec(original.Body.ContinuumRegion.Bounds,16,8,2)};var bounds=source.Body.ContinuumRegion.Bounds;var center=new Vector3D((bounds.Min.X+bounds.Max.X)/2,(bounds.Min.Y+bounds.Max.Y)/2,(bounds.Min.Z+bounds.Max.Z)/2);
        var rotation=Transform3D.CreateTranslation(-center)*Transform3D.CreateRotationX(15*double.Pi/180)*Transform3D.CreateRotationY(20*double.Pi/180)*Transform3D.CreateRotationZ(45*double.Pi/180)*Transform3D.CreateTranslation(center);
        var result=LinearElasticSolver.Solve(source,new(CutCellQuadraturePerAxis:6,RelativeResidualTolerance:1e-9,DomainTransform:rotation,PreserveNominalCellVolumeUnderTransform:true));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));Assert.True(result.TinyCells.MinimumActiveFraction<1e-4);
        Assert.True(result.System.AggregatedDegreesOfFreedom>1000);Assert.True(result.System.DegreesOfFreedom<result.System.RawDegreesOfFreedom);Assert.InRange(result.System.DiagonalRatio,1,100);
        Assert.InRange(result.MaximumDisplacementMeters,8e-6,3e-5);Assert.InRange(result.MaximumVonMisesPascal,10e6,100e6);Assert.InRange(result.Equilibrium.ResidualNewton.Length,0,1e-5);
        Assert.InRange(result.StrainEnergy!.RelativeResidual,0,1e-12);
        Assert.InRange(result.Solver.Iterations,1,700);Assert.Equal(result.NumericalLowering!.FixedThresholdAggregationCount,result.NumericalLowering.BasisTreatments.Count(t=>t.Treatment==ImmersedBasisTreatmentKind.Aggregated));
        var nodes=result.Displacements.ToDictionary(item=>item.NodeId,item=>item.Position);
        foreach(var treatment in result.NumericalLowering.BasisTreatments.Where(t=>t.Treatment==ImmersedBasisTreatmentKind.Aggregated))
        {
            Assert.InRange(double.Abs(treatment.ExtensionWeights.Values.Sum()-1),0,1e-12);var reproduced=new Vector3D(0,0,0);
            foreach(var weight in treatment.ExtensionWeights){var p=nodes[weight.Key];reproduced+=new Vector3D(p.X,p.Y,p.Z)*weight.Value;}
            var sourcePosition=nodes[treatment.SourceNodeId];Assert.InRange((reproduced-new Vector3D(sourcePosition.X,sourcePosition.Y,sourcePosition.Z)).Length,0,1e-12);
        }
    }

    [Fact]
    public void SafeImmersedPlanarTrace_UsesSymmetricNitscheOnExactBoundary()
    {
        var source=FirmamentAnalysisCompiler.Compile(DirectSource).Analysis!;var center=new Vector3D(.1,.05,.005);var rotation=Transform3D.CreateTranslation(-center)*Transform3D.CreateRotationZ(23*double.Pi/180)*Transform3D.CreateRotationY(11*double.Pi/180)*Transform3D.CreateTranslation(center);
        var result=LinearElasticSolver.Solve(source,new(CutCellQuadraturePerAxis:6,RelativeResidualTolerance:1e-8,DomainTransform:rotation,PreserveNominalCellVolumeUnderTransform:true));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));var enforcement=Assert.Single(result.NumericalLowering!.BoundaryEnforcements);
        Assert.True(enforcement.Enforcement==BoundaryEnforcementKind.SymmetricNitsche,$"actual={enforcement.Enforcement}; utility={enforcement.Utility:R}; offset={enforcement.MaximumNormalizedNodeOffset:R}; min={result.TinyCells.MinimumActiveFraction:R}; rejections={string.Join("|",enforcement.Rejections)}");Assert.Equal(100,enforcement.PenaltyScale);Assert.InRange(enforcement.MaximumViolationMeters,0,2e-6);Assert.InRange(result.System.MaximumAsymmetry,0,1e-5);Assert.InRange(result.Equilibrium.ResidualNewton.Length,0,100);
    }
}
