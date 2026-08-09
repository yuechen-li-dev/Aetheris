namespace Aetheris.Continuum.Experiments.Attention;

public sealed record AttentionE1MethodResult(string Matrix,string Scenario,string Method,int PointsPerAxis,int Unknowns,int OperatorNonzeros,
    int UndirectedInteractions,double InteractionsPerUnknown,long PreconditionerFlops,long MemoryBytes,double SetupMilliseconds,double RuntimeMilliseconds,
    double MatvecMilliseconds,double PreconditionerMilliseconds,int Iterations,double RelativeResidual,double RelativeSolutionError,IReadOnlyList<ResidualSample> ResidualHistory);
public sealed record AttentionE1ContractResult(string Method,double RelativeSymmetryDefect,double MinimumEnergy,bool Passed);
public sealed record AttentionE1ModeResult(string Method,string Mode,double ReductionFactor);
public sealed record AttentionE1Benchmark(string Schema,string Hypothesis,string Pde,string Discretization,string BudgetRule,string ResultClassification,
    IReadOnlyList<AttentionE1MethodResult> Methods,IReadOnlyList<AttentionE1ContractResult> Contracts,IReadOnlyList<AttentionE1ModeResult> ModeAnalysis,
    IReadOnlyList<InteractionSample> InteractionSamples,IReadOnlyList<object> InformationAudit);

public static class AttentionE1Experiments
{
    public const int EdgeBudget=8;
    public const double RelativeTolerance=1e-8;
    private static readonly ContinuumFieldMask[] Ablations=[ContinuumFieldMask.None,ContinuumFieldMask.Geometry,ContinuumFieldMask.Material,
        ContinuumFieldMask.Authority,ContinuumFieldMask.Geometry|ContinuumFieldMask.Material,ContinuumFieldMask.Geometry|ContinuumFieldMask.Authority,
        ContinuumFieldMask.Material|ContinuumFieldMask.Authority,ContinuumFieldMask.Geometry|ContinuumFieldMask.Material|ContinuumFieldMask.Authority];

    public static AttentionE1Benchmark Run(bool includeScaling=true)
    {
        WarmUp();var rows=new List<AttentionE1MethodResult>();
        var primaryConfig=new AttentionE1Configuration();var primary=HeterogeneousAnisotropicSystem.Create(primaryConfig);
        var coefficient=InteractionGraphBuilder.Build(primary.System,ContinuumFieldMask.None,true,EdgeBudget);
        var all=InteractionGraphBuilder.Build(primary.System,ContinuumFieldMask.Geometry|ContinuumFieldMask.Material|ContinuumFieldMask.Authority,true,EdgeBudget);
        Add(rows,"benchmark","primary",primary,new E1IdentityPreconditioner(primary.System.UnknownCount));
        Add(rows,"benchmark","primary",primary,new E1JacobiPreconditioner(primary.System));
        Add(rows,"benchmark","primary",primary,coefficient);
        Add(rows,"benchmark","primary",primary,InteractionGraphBuilder.BuildE0CompactControl(primary.System));
        Add(rows,"benchmark","primary",primary,new GeometricTwoLevelPreconditioner(primary.System));
        foreach(var mask in Ablations)Add(rows,"ablation","primary",primary,InteractionGraphBuilder.Build(primary.System,mask,true,EdgeBudget));
        Add(rows,"weight-ablation","primary",primary,InteractionGraphBuilder.Build(primary.System,ContinuumFieldMask.Geometry|ContinuumFieldMask.Material|ContinuumFieldMask.Authority,false,EdgeBudget));
        Add(rows,"weight-ablation","primary",primary,all);

        foreach(var contrast in new[]{1d,10d,100d,1000d})RunPair(rows,"contrast",$"contrast-{contrast:R}",new(primaryConfig.PointsPerAxis,contrast,16d,30d,AuthorityConfiguration.Opposed));
        foreach(var ratio in new[]{1d,4d,16d,64d})RunPair(rows,"anisotropy",$"anisotropy-{ratio:R}",new(primaryConfig.PointsPerAxis,100d,ratio,0d,AuthorityConfiguration.Opposed));
        foreach(var angle in new[]{0d,30d,45d})RunPair(rows,"orientation",$"orientation-{angle:R}",new(primaryConfig.PointsPerAxis,100d,16d,angle,AuthorityConfiguration.Opposed));
        foreach(var authority in Enum.GetValues<AuthorityConfiguration>())RunPair(rows,"authority",$"authority-{authority}",new(primaryConfig.PointsPerAxis,100d,16d,30d,authority));
        if(includeScaling)
        {
            foreach(var size in new[]{16,32})RunScaling(rows,size);
            var fieldPrimary=rows.Single(r=>r.Matrix=="ablation"&&r.Method.StartsWith("graph-Geometry+Material+Authority-selection+weight"));
            var coefficientPrimary=rows.Single(r=>r.Matrix=="ablation"&&r.Method.StartsWith("graph-coefficient-only"));
            if(fieldPrimary.Iterations<=coefficientPrimary.Iterations&&fieldPrimary.RuntimeMilliseconds<=1.25d*coefficientPrimary.RuntimeMilliseconds)RunScaling(rows,64);
        }

        var contracts=new IE1Preconditioner[]{coefficient,all,InteractionGraphBuilder.BuildE0CompactControl(primary.System),new GeometricTwoLevelPreconditioner(primary.System)}.Select(p=>Contract(primary.System,p)).ToArray();
        var modes=new[]{coefficient,all}.SelectMany(p=>new[]{Mode(primary.System,p,"low-global",LowMode),Mode(primary.System,p,"anisotropy-aligned",AnisotropyMode),Mode(primary.System,p,"interface-localized",InterfaceMode),Mode(primary.System,p,"high-grid",HighMode)}).ToArray();
        var coeffRow=rows.Single(r=>r.Matrix=="ablation"&&r.Method.StartsWith("graph-coefficient-only"));var fieldRow=rows.Single(r=>r.Matrix=="ablation"&&r.Method.StartsWith("graph-Geometry+Material+Authority-selection+weight"));
        var classification=fieldRow.Iterations<coeffRow.Iterations&&fieldRow.RuntimeMilliseconds<coeffRow.RuntimeMilliseconds?"useful-prior":fieldRow.Iterations<coeffRow.Iterations?"classical-equivalent (iteration-only; cost gate failed)":"negative/classical-equivalent";
        return new("aetheris-continuum-attention-e1-v1",
            "At identical 8-interaction/unknown graph budget, explicit geometry/material/authority semantics select an SPD sparse inverse-interaction graph that converges faster than a graph selected only from local K coefficients.",
            "-div(A(x) grad u)=f; two regions separated by x+0.35y=0.675; material scale contrast; isotropic axial energy plus an explicit lattice-representable in-plane principal-direction bond",
            "cell-centred SPD directional-energy discretization, harmonic bond coefficients, homogeneous Dirichlet cube; exact discrete manufactured reference",
            "Every graph-selection comparator has exactly 8 interactions/unknown in aggregate (4N undirected edges), a hard local degree bound of 12, and identical application formula, storage layout, and FLOP count.",classification,rows,contracts,modes,
            InteractionGraphBuilder.Sample(primary.System,all),InformationAudit());
    }

    private static void RunPair(List<AttentionE1MethodResult> rows,string matrix,string scenario,AttentionE1Configuration c)
    {var p=HeterogeneousAnisotropicSystem.Create(c);Add(rows,matrix,scenario,p,new E1JacobiPreconditioner(p.System));Add(rows,matrix,scenario,p,InteractionGraphBuilder.Build(p.System,ContinuumFieldMask.None,true,EdgeBudget));Add(rows,matrix,scenario,p,InteractionGraphBuilder.Build(p.System,ContinuumFieldMask.Geometry|ContinuumFieldMask.Material|ContinuumFieldMask.Authority,true,EdgeBudget));}
    private static void RunScaling(List<AttentionE1MethodResult> rows,int n){var p=HeterogeneousAnisotropicSystem.Create(new(n));Add(rows,"scaling",$"n-{n}",p,InteractionGraphBuilder.Build(p.System,ContinuumFieldMask.None,true,EdgeBudget));Add(rows,"scaling",$"n-{n}",p,InteractionGraphBuilder.Build(p.System,ContinuumFieldMask.Geometry|ContinuumFieldMask.Material|ContinuumFieldMask.Authority,true,EdgeBudget));Add(rows,"scaling",$"n-{n}",p,new GeometricTwoLevelPreconditioner(p.System));}
    private static void Add(List<AttentionE1MethodResult> rows,string matrix,string scenario,AttentionE1Problem p,IE1Preconditioner preconditioner)
    {var runs=Enumerable.Range(0,3).Select(_=>AttentionE1Pcg.Solve(p,preconditioner,RelativeTolerance)).OrderBy(x=>x.RuntimeMilliseconds).ToArray();var result=runs[1];rows.Add(new(matrix,scenario,preconditioner.Name,p.System.PointsPerAxis,p.System.UnknownCount,p.System.NonzeroCount,preconditioner.UndirectedEdges,preconditioner.InteractionsPerUnknown,preconditioner.EstimatedFlopsPerApply,preconditioner.EstimatedStorageBytes,preconditioner.SetupMilliseconds,result.RuntimeMilliseconds,result.MatvecMilliseconds,result.PreconditionerMilliseconds,result.Iterations,result.RelativeResidual,result.RelativeSolutionError,result.ResidualHistory));}
    private static AttentionE1ContractResult Contract(HeterogeneousAnisotropicSystem s,IE1Preconditioner p){var x=Enumerable.Range(0,s.UnknownCount).Select(i=>Math.Sin((i+1)*.31)).ToArray();var y=Enumerable.Range(0,s.UnknownCount).Select(i=>Math.Cos((i+1)*.23)).ToArray();var px=new double[x.Length];var py=new double[y.Length];p.Apply(x,px);p.Apply(y,py);var a=AttentionE1Pcg.Dot(x,py);var b=AttentionE1Pcg.Dot(y,px);var defect=Math.Abs(a-b)/Math.Max(1d,Math.Max(Math.Abs(a),Math.Abs(b)));var ex=AttentionE1Pcg.Dot(x,px);var ey=AttentionE1Pcg.Dot(y,py);return new(p.Name,defect,Math.Min(ex,ey),defect<1e-11&&ex>0&&ey>0);}
    private static AttentionE1ModeResult Mode(HeterogeneousAnisotropicSystem s,IE1Preconditioner p,string name,Func<HeterogeneousAnisotropicSystem,int,double> factory){var e=Enumerable.Range(0,s.UnknownCount).Select(i=>factory(s,i)).ToArray();var norm=AttentionE1Pcg.Norm(e);for(var i=0;i<e.Length;i++)e[i]/=norm;var r=new double[e.Length];var z=new double[e.Length];s.Apply(e,r);p.Apply(r,z);for(var i=0;i<e.Length;i++)e[i]-=z[i];return new(p.Name,name,AttentionE1Pcg.Norm(e));}
    private static double LowMode(HeterogeneousAnisotropicSystem s,int q){var(i,j,k)=s.Coordinates(q);var n=s.PointsPerAxis;return Math.Sin(Math.PI*(i+.5)/n)*Math.Sin(Math.PI*(j+.5)/n)*Math.Sin(Math.PI*(k+.5)/n);}
    private static double HighMode(HeterogeneousAnisotropicSystem s,int q){var(i,j,k)=s.Coordinates(q);return((i+j+k)&1)==0?1d:-1d;}
    private static double AnisotropyMode(HeterogeneousAnisotropicSystem s,int q){var(i,j,k)=s.Coordinates(q);var f=s.Fields[q];var phase=(f.MaterialAxisX*(i+.5)+f.MaterialAxisY*(j+.5))/s.PointsPerAxis;return Math.Sin(2d*Math.PI*phase)*Math.Sin(Math.PI*(k+.5)/s.PointsPerAxis);}
    private static double InterfaceMode(HeterogeneousAnisotropicSystem s,int q){var f=s.Fields[q];return Math.Exp(-Math.Abs(f.InterfaceSignedDistance)/(2d*s.Spacing))*(f.MaterialId==0?1d:-1d);}
    private static IReadOnlyList<object> InformationAudit()=>new object[]{
        new{feature="cell position/lattice direction",classification="already encoded in local K graph coordinates",use="candidate displacement and distance"},
        new{feature="face conductance and diagonal",classification="already encoded in local K",use="coefficient-only path-strength score and all graph weights"},
        new{feature="interface distance/normal",classification="available from Aetheris geometry/material semantics; cheaply inferable only with a wider coefficient neighborhood",use="geometry tangential selection"},
        new{feature="material identity",classification="available before assembly; usually cheaply inferable from sharp K changes",use="cross-interface selection penalty"},
        new{feature="principal material axis",classification="available before assembly and directly encoded by the strong off-axis bond in this K",use="material-aligned selection"},
        new{feature="geometry/material confidence authority",classification="semantic policy not encoded in K; contextual but not physical PDE data",use="ownership-consistency selection/weighting"}};
    private static void WarmUp(){var p=HeterogeneousAnisotropicSystem.Create(new(4,10,4,30));_ = AttentionE1Pcg.Solve(p,new E1JacobiPreconditioner(p.System),1e-4,20);}
}
