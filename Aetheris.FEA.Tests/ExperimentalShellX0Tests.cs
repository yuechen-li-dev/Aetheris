using Aetheris.FEA.Analysis;
using Aetheris.FEA.Firmament;
using Aetheris.FEA.Mechanics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FEA.Tests;

public sealed class ExperimentalShellX0Tests
{
    [Fact]
    public void ModeIsExplicitAndProductionDefaultIsPreserved()
    {
        var shell=CompileFixture("Canonical","FEA","ExperimentalShell","flat-cantilever.firmament");
        Assert.Equal(AnalysisMode.ExperimentalShell,shell.Mode);
        Assert.NotNull(shell.ExperimentalShell);
        Assert.Equal(3,shell.ExperimentalShell.PolynomialOrder);
        Assert.Equal(1e-12,shell.ExperimentalShell.FictitiousStiffnessFactor);

        var production=FirmamentAnalysisCompiler.Compile(FlatSource.Replace("Mode: ExperimentalShell",string.Empty,StringComparison.Ordinal)
            .Replace("Thickness: 2mm",string.Empty,StringComparison.Ordinal)
            .Replace("MasterGrid: [4, 1]",string.Empty,StringComparison.Ordinal)
            .Replace("Order: 3",string.Empty,StringComparison.Ordinal)
            .Replace("AlphaFictitious: 1e-12",string.Empty,StringComparison.Ordinal)
            .Replace("MaxSubdivisionDepth: 4",string.Empty,StringComparison.Ordinal));
        Assert.True(production.IsSuccess,Describe(production));
        Assert.Equal(AnalysisMode.ProductionSolid,production.Analysis!.Mode);
        Assert.Null(production.Analysis.ExperimentalShell);
    }

    [Fact]
    public void InvalidSettingsHaveStableDiagnostics()
    {
        var missing=CompileFixtureResult("Invalid","FEA","ExperimentalShell","missing-thickness.firmament");
        Assert.Contains(missing.Diagnostics,item=>item.Code=="thinwall-thickness-missing");
        var order=CompileFixtureResult("Invalid","FEA","ExperimentalShell","invalid-order.firmament");
        Assert.Contains(order.Diagnostics,item=>item.Code=="thinwall-basis-order-invalid");
        var zero=CompileFixtureResult("Invalid","FEA","ExperimentalShell","zero-thickness.firmament");
        Assert.Contains(zero.Diagnostics,item=>item.Code=="thinwall-thickness-invalid");
        var rigid=CompileFixtureResult("Invalid","FEA","ExperimentalShell","rigid-body.firmament");
        Assert.Contains(rigid.Diagnostics,item=>item.Code=="fea-rigid-body-mode");
        var singular=CompileFixture("Invalid","FEA","ExperimentalShell","singular-map.firmament");
        var singularResult=LinearElasticSolver.Solve(singular);
        Assert.False(singularResult.IsSuccess);
        Assert.Contains(singularResult.Diagnostics,item=>item.Code=="thinwall-map-singular");
    }

    [Fact]
    public void FlatCantileverPRefinementApproachesBeamReference()
    {
        var baseline=FirmamentAnalysisCompiler.Compile(FlatSource).Analysis!;
        var studies=new[]{1,2,3}.Select(order=>Solve(baseline with{ExperimentalShell=baseline.ExperimentalShell! with{PolynomialOrder=order}})).ToArray();
        const double reference=0.00125; // F L^3 / (3 E I), I=b h^3/12.
        var errors=studies.Select(result=>double.Abs(result.MaximumDisplacementMeters-reference)/reference).ToArray();
        Assert.True(errors[2]<errors[1]&&errors[1]<errors[0],string.Join(", ",errors.Select(value=>value.ToString("R"))));
        Assert.InRange(errors[2],0,.05);
        Assert.All(studies,result=>Assert.InRange(result.Equilibrium.ResidualNewton.Length,0,1e-5));
        Assert.All(studies,result=>Assert.InRange(result.StrainEnergy!.RelativeResidual,0,1e-8));
    }

    [Fact]
    public void PerforatedPlateUsesCutSubcellsAndIsDeterministic()
    {
        var analysis=CompileFixture("Canonical","FEA","ExperimentalShell","cut-boundary-plate.firmament");
        var first=Solve(analysis);var second=Solve(analysis);
        Assert.Equal(4,first.ExperimentalShell!.CutCells);
        Assert.True(first.ExperimentalShell.CutSubcellCount>0);
        Assert.InRange(first.ExperimentalShell.MinimumPhysicalCutFraction,.75,.9);
        Assert.Equal(first.ExperimentalShell.DeterministicDiscretizationHash,second.ExperimentalShell!.DeterministicDiscretizationHash);
        Assert.Equal(first.MaximumDisplacementMeters,second.MaximumDisplacementMeters,14);
        Assert.InRange(first.Equilibrium.ResidualNewton.Length,0,1e-6);
        Assert.InRange(first.StrainEnergy!.RelativeResidual,0,1e-8);
    }

    [Fact]
    public void CutIntegrationAndAlphaStudiesRemainSeparated()
    {
        var baseline=CompileFixture("Canonical","FEA","ExperimentalShell","cut-boundary-plate.firmament");
        var alphaStudies=new[]{1e-8,1e-10,1e-12}.Select(alpha=>Solve(baseline with{ExperimentalShell=baseline.ExperimentalShell! with{FictitiousStiffnessFactor=alpha}})).ToArray();
        Assert.All(alphaStudies,result=>Assert.True(result.ExperimentalShell!.CutSubcellCount>0));
        var alphaSpread=(alphaStudies.Max(result=>result.StrainEnergy!.AlgebraicJoule)-alphaStudies.Min(result=>result.StrainEnergy!.AlgebraicJoule))/alphaStudies[^1].StrainEnergy!.AlgebraicJoule;
        Assert.InRange(alphaSpread,0,1e-6);

        var depth3=Solve(baseline with{ExperimentalShell=baseline.ExperimentalShell! with{MaxSubdivisionDepth=3}});
        var order4=Solve(baseline with{ExperimentalShell=baseline.ExperimentalShell! with{QuadratureOrder=4}});
        Assert.True(depth3.ExperimentalShell!.CutSubcellCount<alphaStudies[^1].ExperimentalShell!.CutSubcellCount);
        Assert.InRange(RelativeDifference(depth3.StrainEnergy!.AlgebraicJoule,alphaStudies[^1].StrainEnergy!.AlgebraicJoule),0,.02);
        Assert.InRange(RelativeDifference(order4.StrainEnergy!.AlgebraicJoule,alphaStudies[^1].StrainEnergy!.AlgebraicJoule),0,.002);
    }

    [Fact]
    public void ThinnessSweepKeepsCellAndDofCountsFixed()
    {
        var studies=new[]{10,50,100}.Select(ratio=>
        {
            var thickness=100d/ratio;var token=thickness.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"mm";
            var source=FlatSource.Replace("2mm]",token+"]",StringComparison.Ordinal).Replace("Thickness: 2mm","Thickness: "+token,StringComparison.Ordinal);
            var compilation=FirmamentAnalysisCompiler.Compile(source);Assert.True(compilation.IsSuccess,Describe(compilation));
            return(ratio,thickness,result:Solve(compilation.Analysis!));
        }).ToArray();
        Assert.Single(studies.Select(item=>item.result.System.DegreesOfFreedom).Distinct());
        foreach(var study in studies)
        {
            var h=study.thickness/1000;var reference=10*.1*.1*.1/(3*200e9*(.02*h*h*h/12));
            Assert.InRange(RelativeDifference(study.result.MaximumDisplacementMeters,reference),0,.05);
            Assert.InRange(study.result.Equilibrium.ResidualNewton.Length,0,1e-6);
        }
    }

    [Fact]
    public void NonfiniteExperimentalInputIsRejectedBeforeAssembly()
    {
        var analysis=FirmamentAnalysisCompiler.Compile(FlatSource).Analysis!;
        var invalid=analysis with{Loads=analysis.Loads.Select(load=>load with{VectorSi=new(double.NaN,0,0)}).ToArray()};
        var result=LinearElasticSolver.Solve(invalid);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,item=>item.Code=="fea-invalid-load");
        Assert.Equal(0,result.System.DegreesOfFreedom);
    }

    [Fact]
    public void CylindricalMapHasPositiveAnalyticJacobianAndRejectsInconsistentSpan()
    {
        var bounds=new BoundingBox3D(new(0,0,0),new(.1,.1*double.Pi/2,.002));
        var settings=new ExperimentalShellSettings(.002,2,2,2,GeometryMap:ThinWallGeometryMapKind.Cylindrical,CylinderRadiusMeters:.1,CylinderAngleRadians:double.Pi/2);
        var map=new ThinWallGeometryMap(settings,bounds);
        foreach(var t in new[]{-1d,0d,1d})
            Assert.True(ThinWallGeometryMap.ElementJacobianDeterminant(map.Evaluate(.05,.05,t),.05,bounds.Max.Y/2)>0);
        var exception=Assert.Throws<ThinWallMapException>(()=>new ThinWallGeometryMap(settings,new(new(0,0,0),new(.1,.2,.002))));
        Assert.Equal("thinwall-map-singular",exception.Code);
    }

    private static LinearElasticAnalysisResult Solve(LinearElasticAnalysisIr analysis)
    {
        var result=LinearElasticSolver.Solve(analysis,new(RelativeResidualTolerance:1e-8,MaximumIterations:10000));
        Assert.True(result.IsSuccess,Describe(result));return result;
    }

    private static LinearElasticAnalysisIr CompileFixture(params string[] parts)
    {
        var result=CompileFixtureResult(parts);Assert.True(result.IsSuccess,Describe(result));return result.Analysis!;
    }

    private static FirmamentAnalysisCompilation CompileFixtureResult(params string[] parts)
    {
        var path=Path.Combine([FindRoot(),"fixtures",..parts]);
        return FirmamentAnalysisCompiler.Compile(File.ReadAllText(path),path,Path.GetDirectoryName(path));
    }

    private static string FlatSource=>File.ReadAllText(Path.Combine(FindRoot(),"fixtures","Canonical","FEA","ExperimentalShell","flat-cantilever.firmament"));
    private static double RelativeDifference(double a,double b)=>double.Abs(a-b)/double.Max(double.Abs(b),1e-30);
    private static string Describe(FirmamentAnalysisCompilation value)=>string.Join("; ",value.Diagnostics.Select(item=>item.Code+":"+item.Message));
    private static string Describe(LinearElasticAnalysisResult value)=>string.Join("; ",value.Diagnostics.Select(item=>item.Code+":"+item.Message));
    private static string FindRoot(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Aetheris.slnx")))directory=directory.Parent;return directory?.FullName??throw new DirectoryNotFoundException();}
}
