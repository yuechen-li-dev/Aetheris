using Aetheris.Continuum.Experiments.Attention;

namespace Aetheris.Continuum.Tests.Experiments.Attention;

public sealed class AttentionE1Tests
{
    [Fact] public void AnisotropicTwoMaterialAssemblyIsSymmetricPositiveAndUsesHarmonicInterfaceFaces()
    {var p=HeterogeneousAnisotropicSystem.Create(new(8,100,16,30));var s=p.System;Assert.Contains(s.Edges,e=>s.Fields[e.A].MaterialId!=s.Fields[e.B].MaterialId);foreach(var e in s.Edges.Take(100))Assert.Equal(e.Conductance,s.DirectConductance(e.B,e.A),12);var x=Enumerable.Range(0,s.UnknownCount).Select(i=>Math.Sin((i+1)*.19)).ToArray();var kx=new double[x.Length];s.Apply(x,kx);Assert.True(Dot(x,kx)>0);}
    [Fact] public void FieldsAreDeterministicAndHaveDistinctSemantics()
    {var a=HeterogeneousAnisotropicSystem.Create(new(8,100,16,30)).System;var b=HeterogeneousAnisotropicSystem.Create(new(8,100,16,30)).System;Assert.Equal(a.Fields,b.Fields);Assert.Contains(a.Fields,f=>f.GeometryConfidence>.9);Assert.Contains(a.Fields,f=>f.MaterialConfidence<.3);Assert.Contains(a.Fields,f=>f.MaterialId==0);Assert.Contains(a.Fields,f=>f.MaterialId==1);}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)]
    public void EveryAblationEnforcesExactBudgetAndSpd(int bits)
    {var s=HeterogeneousAnisotropicSystem.Create(new(8,10,4,30)).System;var p=InteractionGraphBuilder.Build(s,(ContinuumFieldMask)bits,true);Assert.Equal(8d,p.InteractionsPerUnknown);var x=Enumerable.Range(0,s.UnknownCount).Select(i=>Math.Sin((i+1)*.31)).ToArray();var y=Enumerable.Range(0,s.UnknownCount).Select(i=>Math.Cos((i+1)*.17)).ToArray();var px=new double[x.Length];var py=new double[y.Length];p.Apply(x,px);p.Apply(y,py);Assert.True(Dot(x,px)>0);Assert.True(Math.Abs(Dot(x,py)-Dot(y,px))/Math.Max(1,Math.Abs(Dot(x,py)))<1e-11);}
    [Fact] public void GraphSelectionIsBitwiseDeterministic()
    {var s=HeterogeneousAnisotropicSystem.Create(new(8)).System;var a=InteractionGraphBuilder.Build(s,ContinuumFieldMask.Geometry|ContinuumFieldMask.Material|ContinuumFieldMask.Authority,true);var b=InteractionGraphBuilder.Build(s,ContinuumFieldMask.Geometry|ContinuumFieldMask.Material|ContinuumFieldMask.Authority,true);Assert.Equal(a.Edges,b.Edges);}
    [Fact] public void BaselinesConvergeToIndependentDiscreteReference()
    {var problem=HeterogeneousAnisotropicSystem.Create(new(8,10,4,30));IE1Preconditioner[] methods=[new E1JacobiPreconditioner(problem.System),InteractionGraphBuilder.Build(problem.System,ContinuumFieldMask.None,true),new GeometricTwoLevelPreconditioner(problem.System)];foreach(var m in methods){var r=AttentionE1Pcg.Solve(problem,m,1e-8,400);Assert.True(r.RelativeResidual<1e-8,$"{m.Name}: {r.RelativeResidual}");Assert.True(r.RelativeSolutionError<1e-7);}}
    private static double Dot(IReadOnlyList<double>a,IReadOnlyList<double>b){double s=0;for(var i=0;i<a.Count;i++)s+=a[i]*b[i];return s;}
}
