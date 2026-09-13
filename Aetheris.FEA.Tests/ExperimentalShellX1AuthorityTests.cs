using Aetheris.FEA.Analysis;
using Aetheris.SheetMetal;

namespace Aetheris.FEA.Tests;

public sealed class ExperimentalShellX1AuthorityTests
{
    [Fact]
    public void CompilerDerivesNativeThicknessAndMaterialWithoutDuplicateAnalysisAuthority()
    {
        var path=Path.Combine(FindRoot(),"fixtures","Canonical","FEA","ExperimentalShell","native-l-bracket.firmament");
        var compiled=Aetheris.FEA.Firmament.FirmamentAnalysisCompiler.Compile(File.ReadAllText(path),path,Path.GetDirectoryName(path));
        Assert.True(compiled.IsSuccess,string.Join("; ",compiled.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        Assert.Equal(AnalysisGeometrySourceKind.NativeSheetMetal,compiled.Analysis!.Body.GeometrySource);
        Assert.Equal(.0015,compiled.Analysis.ExperimentalShell!.ThicknessMeters,12);
        Assert.NotNull(compiled.Analysis.Materials.Single().CatalogMaterial);
        Assert.Equal(3,compiled.Analysis.MidsurfacePatchGraph!.Patches.Count);
        foreach(var patch in compiled.Analysis.MidsurfacePatchGraph.Patches)
        {
            var b=patch.ParameterDomain.Bounds;var mapped=ThinWallAnalysisAuthority.Evaluate(patch,(b.Min.X+b.Max.X)/2,(b.Min.Y+b.Max.Y)/2,0);
            Assert.True(mapped.DerivativeR.Cross(mapped.DerivativeS).Dot(mapped.DerivativeT)>0,$"{patch.Identity}: R={mapped.DerivativeR}, S={mapped.DerivativeS}, T={mapped.DerivativeT}");
        }
    }

    [Fact]
    public void NativeSingleBendSolvesWithSharedC0TraceDegreesOfFreedom()
    {
        var path=Path.Combine(FindRoot(),"fixtures","Canonical","FEA","ExperimentalShell","native-l-bracket.firmament");var compiled=Aetheris.FEA.Firmament.FirmamentAnalysisCompiler.Compile(File.ReadAllText(path),path,Path.GetDirectoryName(path));
        Assert.True(compiled.IsSuccess,string.Join("; ",compiled.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        var result=Aetheris.FEA.Mechanics.LinearElasticSolver.Solve(compiled.Analysis!,new(RelativeResidualTolerance:1e-8,MaximumIterations:10000));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        Assert.Equal(585,result.System.DegreesOfFreedom);
        Assert.Equal(90,result.ExperimentalShell!.NativeSheetMetal!.SharedDisplacementDegreesOfFreedom);
        Assert.Equal(2,result.ExperimentalShell.NativeSheetMetal.InterfaceCount);
        Assert.InRange(result.Equilibrium.ResidualNewton.Length,0,1e-5);
        Assert.InRange(result.StrainEnergy!.RelativeResidual,0,1e-6);
        Assert.True(double.IsFinite(result.MaximumDisplacementMeters)&&result.MaximumDisplacementMeters>0);
    }

    [Fact]
    public void NativeFlatPanelMatchesFrozenSyntheticFlatSolution()
    {
        var native=Compile("Canonical","FEA","ExperimentalShell","native-flat-cantilever.firmament");var synthetic=Compile("Canonical","FEA","ExperimentalShell","flat-cantilever.firmament");
        var nativeResult=Aetheris.FEA.Mechanics.LinearElasticSolver.Solve(native,new(RelativeResidualTolerance:1e-8,MaximumIterations:10000));var syntheticResult=Aetheris.FEA.Mechanics.LinearElasticSolver.Solve(synthetic,new(RelativeResidualTolerance:1e-8,MaximumIterations:10000));
        Assert.True(nativeResult.IsSuccess,string.Join("; ",nativeResult.Diagnostics.Select(x=>x.Code+":"+x.Message)));Assert.True(syntheticResult.IsSuccess);
        Assert.InRange(Math.Abs(nativeResult.MaximumDisplacementMeters-syntheticResult.MaximumDisplacementMeters)/syntheticResult.MaximumDisplacementMeters,0,1e-7);
        Assert.InRange(Math.Abs(nativeResult.StrainEnergy!.AlgebraicJoule-syntheticResult.StrainEnergy!.AlgebraicJoule)/syntheticResult.StrainEnergy.AlgebraicJoule,0,1e-7);
    }

    [Fact]
    public void NativeAuthorityConflictsFailWithTypedDiagnostics()
    {
        var material=CompileResult("Invalid","FEA","ExperimentalShell","native-sheetmetal-unresolved-material.firmament");Assert.Contains(material.Diagnostics,x=>x.Code=="firmament-material-unknown");
        var thickness=CompileResult("Invalid","FEA","ExperimentalShell","native-sheetmetal-thickness-override.firmament");Assert.Contains(thickness.Diagnostics,x=>x.Code=="thinwall-thickness-source-mismatch");
        var partialEdge=CompileResult("Invalid","FEA","ExperimentalShell","native-sheetmetal-partial-edge-interface.firmament");Assert.Contains(partialEdge.Diagnostics,x=>x.Code=="thinwall-sheetmetal-interface-mismatch");
    }

    [Fact]
    public void NativeTwoBendChannelRetainsOpeningsAndConformingInterfaces()
    {
        var analysis=Compile("Canonical","FEA","ExperimentalShell","native-u-channel.firmament");var graph=analysis.MidsurfacePatchGraph!;
        Assert.Equal(5,graph.Patches.Count);Assert.Equal(2,graph.BendPatchCount);Assert.Equal(2,graph.OpeningCount);Assert.Equal(4,graph.Interfaces.Count);
        var result=Aetheris.FEA.Mechanics.LinearElasticSolver.Solve(analysis,new(RelativeResidualTolerance:1e-8,MaximumIterations:10000));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        Assert.Equal(180,result.ExperimentalShell!.NativeSheetMetal!.SharedDisplacementDegreesOfFreedom);
        Assert.InRange(result.Equilibrium.ResidualNewton.Length,0,1e-5);
        var repeat=Aetheris.FEA.Mechanics.LinearElasticSolver.Solve(analysis,new(RelativeResidualTolerance:1e-8,MaximumIterations:10000));
        Assert.True(repeat.IsSuccess);
        Assert.Equal(result.MaximumDisplacementMeters,repeat.MaximumDisplacementMeters);
        Assert.Equal(result.ExperimentalShell.DeterministicDiscretizationHash,repeat.ExperimentalShell!.DeterministicDiscretizationHash);
        Assert.Equal(result.Solver.ResidualHistory,repeat.Solver.ResidualHistory);
    }

    [Fact]
    public void CurvedTrimmedShellAssemblesDensityBodyForceWithAlphaAwareVolumeQuadrature()
    {
        var analysis=Compile("Canonical","FEA","ExperimentalShell","curved-body-force-shell.firmament");
        var result=Aetheris.FEA.Mechanics.LinearElasticSolver.Solve(analysis,new(RelativeResidualTolerance:1e-8,MaximumIterations:10000));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        var body=result.BodyLoads!.Single();
        Assert.Equal(7850,body.DensityKilogramsPerCubicMeter);
        Assert.True(body.IntegratedVolumeCubicMeters>0);
        Assert.Equal(0,body.IntegratedResultantNewton.X,12);
        Assert.Equal(0,body.IntegratedResultantNewton.Y,12);
        Assert.True(body.IntegratedResultantNewton.Z<0);
        Assert.InRange(result.Equilibrium.ResidualNewton.Length,0,1e-5);
        Assert.True(result.ExperimentalShell!.CutCells>0);
        var missingDensity=analysis with{Materials=analysis.Materials.Select(x=>x with{DensityKilogramsPerCubicMeter=null}).ToArray()};
        var rejected=Aetheris.FEA.Mechanics.LinearElasticSolver.Solve(missingDensity);
        Assert.False(rejected.IsSuccess);
        Assert.Contains(rejected.Diagnostics,x=>x.Code=="fea-body-force-density-missing");
    }

    [Fact]
    public void OffsetCutRemainsFiniteWithoutChangingTheX0CutCellMethod()
    {
        var path=Path.Combine(FindRoot(),"fixtures","Canonical","FEA","ExperimentalShell","cut-boundary-plate.firmament");
        var source=File.ReadAllText(path).Replace("Point2(0mm, 0mm)","Point2(137mm, 83mm)",StringComparison.Ordinal);
        var compiled=Aetheris.FEA.Firmament.FirmamentAnalysisCompiler.Compile(source,path,Path.GetDirectoryName(path));
        Assert.True(compiled.IsSuccess,string.Join("; ",compiled.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        var result=Aetheris.FEA.Mechanics.LinearElasticSolver.Solve(compiled.Analysis!,new(RelativeResidualTolerance:1e-8,MaximumIterations:10000));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        Assert.True(double.IsFinite(result.StrainEnergy!.AlgebraicJoule));
        Assert.True(result.ExperimentalShell!.CutCells>0);
        Assert.InRange(result.Equilibrium.ResidualNewton.Length,0,1e-5);
    }

    private static LinearElasticAnalysisIr Compile(params string[] parts){var result=CompileResult(parts);Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(x=>x.Code+":"+x.Message)));return result.Analysis!;}
    private static Aetheris.FEA.Firmament.FirmamentAnalysisCompilation CompileResult(params string[] parts){var path=Path.Combine(new[]{FindRoot(),"fixtures"}.Concat(parts).ToArray());return Aetheris.FEA.Firmament.FirmamentAnalysisCompiler.Compile(File.ReadAllText(path),path,Path.GetDirectoryName(path));}

    [Fact]
    public void NativeSingleBendProjectsExactPanelBendPanelGraph()
    {
        var path=Path.Combine(FindRoot(),"fixtures","Canonical","SheetMetal","l-bracket.firmament");
        var authored=SheetMetalFirmament.Compile(File.ReadAllText(path),path);
        Assert.True(authored.IsSuccess,string.Join("; ",authored.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        var projection=ThinWallAnalysisAuthority.Project(authored.Part!,new(path,0,0,"SheetMetal LBracket"));
        Assert.True(projection.IsSuccess,string.Join("; ",projection.Diagnostics.Select(x=>x.Code+":"+x.Message)));
        var graph=projection.Graph!;
        Assert.Equal(ThinWallAnalysisSourceKind.NativeSheetMetal,graph.AnalysisSource);
        Assert.Equal(.0015,graph.ThicknessMeters,12);
        Assert.Equal(2,graph.PanelPatchCount);
        Assert.Equal(1,graph.BendPatchCount);
        Assert.Equal(2,graph.Interfaces.Count);
        Assert.All(graph.Interfaces,x=>Assert.InRange(x.MaximumMismatchMeters,0,1e-10));
        var bend=graph.Patches.Single(x=>x.Kind==MidsurfacePatchKind.CylindricalBend);
        Assert.Equal(.00275,bend.SurfaceMap.RadiusMeters,12);
        Assert.Equal(Math.PI/2,Math.Abs(bend.SurfaceMap.AngleRadians),12);
        foreach(var point in new[]{(R:0d,S:0d),(R:bend.ParameterDomain.Bounds.Max.X,S:0d),(R:0d,S:bend.ParameterDomain.Bounds.Max.Y),(R:bend.ParameterDomain.Bounds.Max.X,S:bend.ParameterDomain.Bounds.Max.Y)})
        {
            var mapped=ThinWallAnalysisAuthority.Evaluate(bend,point.R,point.S,0);
            Assert.True(mapped.DerivativeR.Cross(mapped.DerivativeS).Dot(mapped.DerivativeT)>0);
        }
    }

    private static string FindRoot(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Aetheris.slnx")))directory=directory.Parent;return directory?.FullName??throw new DirectoryNotFoundException();}
}
