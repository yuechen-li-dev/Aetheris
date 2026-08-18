using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Constructive;
using Aetheris.Continuum.Sampling;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.StandardLibrary;

var options=new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};
var root=FindRoot(AppContext.BaseDirectory);var output=Path.Combine(root,"artifacts", "local", "evidence", "continuum","m4b");Directory.CreateDirectory(output);
var genericRecipe=new ExactCoaxialPartRecipe("M4B-GenericCoaxial",8,13,5.3,12.35,25,.2,8,35,.9375,6.125,22,"M4B","reference");
var genericPlan=ExactCoaxialPartBuilder.Plan(genericRecipe).Value!;var hexPlan=HexBoltConstructionPlanner.Plan(McMasterHexBoltSpecs.Reference91180A151,"M4B-HexBolt").Value!;
_ = Run("warmup",genericPlan,Transform3D.Identity,8,16);
var generic=Run("generic-coaxial",genericPlan,Transform3D.Identity,12,24);
var orientations=new[]{
    Run("hex-baseline",hexPlan,Transform3D.Identity,12,24),
    Run("hex-rotate-y-29",hexPlan,Transform3D.CreateRotationY(29d*double.Pi/180d),12,24),
    Run("hex-compound-17-31-13",hexPlan,Transform3D.CreateRotationX(17d*double.Pi/180d)*Transform3D.CreateRotationY(31d*double.Pi/180d)*Transform3D.CreateRotationZ(13d*double.Pi/180d),12,24)
};
var deterministicA=Run("determinism-a",hexPlan,Transform3D.Identity,8,16);var deterministicB=Run("determinism-a",hexPlan,Transform3D.Identity,8,16);
var projection=JsonSerializer.Serialize(new{generic=Stable(generic),orientations=orientations.Select(Stable)},options);var hash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(projection)));
Write("generic-coaxial-metrics.json",generic);Write("hexbolt-metrics.json",orientations[0]);Write("orientation-matrix.json",orientations);
Write("dual-lowering-evidence.json",new{generic.ConstructionSourceIdentity,generic.AssociationPassed,generic.ConsistencyProbeCount,generic.SupportFamilyCounts});
Write("complete-association-evidence.json",new{generic.AssociationPassed,hexBolt=orientations.All(x=>x.AssociationPassed),rows=orientations.Select(x=>new{x.Name,x.AssociationPassed,x.MaximumConsistencyResidual,x.ConsistencyProbeCount})});
Write("deterministic-hashes.json",new{algorithm="SHA-256",hash,repeatedIdentity=deterministicA.ConstructionSourceIdentity==deterministicB.ConstructionSourceIdentity,repeatedMetrics=JsonSerializer.Serialize(Stable(deterministicA),options)==JsonSerializer.Serialize(Stable(deterministicB),options),excludesPerformanceTimings=true});
Write("architecture-refactoring-disposition.json",new{milestone="AETHERIS-CONTINUUM-M4B",authority="typed construction -> BRep + CIR",sdfNames="source-broken to Sdf*",genericBrepToCir="not implemented",serialization="deferred; diagnostics JSON is not interchange"});
Console.WriteLine(JsonSerializer.Serialize(new{output,generic,orientations,hash},options));

RunMetrics Run(string name,ExactCoaxialConstructionPlan plan,Transform3D transform,int fixedN,int fineN)
{
    var total=Stopwatch.StartNew();var stage=Stopwatch.StartNew();var dual=ExactCoaxialDualMaterializer.Materialize(plan,transform);stage.Stop();var materializeMs=stage.Elapsed.TotalMilliseconds;
    var semantics=dual.Brep.Semantics.Where(s=>s.Face.HasValue).GroupBy(s=>s.Face!.Value).ToDictionary(g=>g.Key,g=>string.Join("|",g.Select(x=>x.StableId).Order()));
    stage.Restart();var association=new CirBrepAssociation(dual.Continuum.Id,plan.StableId,"outer-shell",plan.StableId,dual.ConstructionSourceIdentity);var shell=new WholeShellBoundaryQuery(dual.Brep.Body,association,transform,semantics);stage.Stop();var discoveryMs=stage.Elapsed.TotalMilliseconds;
    stage.Restart();var consistency=BrepCirConsistencyChecker.Check(dual.Continuum,shell,2e-7);if(!consistency.Passed)throw new InvalidOperationException(name+": "+consistency.Summary+Environment.NewLine+string.Join(Environment.NewLine,consistency.Probes.Where(p=>!p.Passed)));var composer=new WholePartCutCellComposer(dual.Continuum,shell);stage.Stop();var associationMs=stage.Elapsed.TotalMilliseconds;
    stage.Restart();var fixedGrid=ContinuumGridClassifier.Classify(dual.Continuum,new LatticeSpec(dual.Continuum.Bounds,fixedN,fixedN,fixedN),4);stage.Stop();var classificationMs=stage.Elapsed.TotalMilliseconds;
    stage.Restart();var sets=fixedGrid.CutCells.Where(c=>shell.Query(c.Bounds).Count>0).Select(c=>composer.Compose(c.Index,c.Bounds)).ToArray();stage.Stop();var integrationMs=stage.Elapsed.TotalMilliseconds;
    stage.Restart();var fine=ContinuumGridClassifier.Classify(dual.Continuum,new LatticeSpec(dual.Continuum.Bounds,fineN,fineN,fineN),4);stage.Stop();var fineMs=stage.Elapsed.TotalMilliseconds;
    var fixedVolume=fixedGrid.EstimatedOccupiedVolume;
    var area=ContinuumSurfaceAreaEstimator.Estimate(dual.Continuum,fixedN*8,fixedN*8,fixedN*8);var exactV=dual.Continuum.AnalyticReferenceVolume;var exactA=dual.Continuum.AnalyticReferenceBoundaryArea;
    var support=shell.Faces.GroupBy(f=>f.SupportKind.ToString()).OrderBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Count());var composition=sets.GroupBy(s=>s.CompositionKind.ToString()).OrderBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Count());
    total.Stop();return new(name,dual.ConstructionSourceIdentity,consistency.Passed,consistency.Probes.Count,consistency.MaximumExtentResidual,fixedGrid.Lattice.TotalCellCount,fixedGrid.InsideCellCount,fixedGrid.Cells.Count(c=>c.Classification==CellClassification.Outside),fixedGrid.CutCellCount,support,composition,composer.JudgmentCallCount,exactV,fixedVolume,double.Abs(fixedVolume-exactV),double.Abs(fixedVolume-exactV)/exactV,exactA,area,double.Abs(area-exactA)/exactA,fine.EstimatedOccupiedVolume,double.Abs(fine.EstimatedOccupiedVolume-exactV)/exactV,materializeMs,associationMs,discoveryMs,classificationMs,integrationMs,fineMs,total.Elapsed.TotalMilliseconds);
}
void Write(string file,object value)=>File.WriteAllText(Path.Combine(output,file),JsonSerializer.Serialize(value,options)+Environment.NewLine,new UTF8Encoding(false));
object Stable(RunMetrics x)=>new{x.Name,x.ConstructionSourceIdentity,x.AssociationPassed,x.ConsistencyProbeCount,x.MaximumConsistencyResidual,x.TotalCells,x.Inside,x.Outside,x.Cut,x.SupportFamilyCounts,x.CompositionCounts,x.JudgmentEngineCalls,x.ExactVolume,x.EstimatedVolume,x.AbsoluteVolumeError,x.RelativeVolumeError,x.ExactBoundaryArea,x.EstimatedBoundaryArea,x.RelativeBoundaryAreaError,x.FineEstimatedVolume,x.FineRelativeVolumeError};
static string FindRoot(string start){for(var d=new DirectoryInfo(start);d is not null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))return d.FullName;throw new DirectoryNotFoundException();}
sealed record RunMetrics(string Name,string ConstructionSourceIdentity,bool AssociationPassed,int ConsistencyProbeCount,double MaximumConsistencyResidual,int TotalCells,int Inside,int Outside,int Cut,IReadOnlyDictionary<string,int> SupportFamilyCounts,IReadOnlyDictionary<string,int> CompositionCounts,int JudgmentEngineCalls,double ExactVolume,double EstimatedVolume,double AbsoluteVolumeError,double RelativeVolumeError,double ExactBoundaryArea,double EstimatedBoundaryArea,double RelativeBoundaryAreaError,double FineEstimatedVolume,double FineRelativeVolumeError,double BrepAndCirMaterializationMilliseconds,double AssociationValidationMilliseconds,double CandidateDiscoveryMilliseconds,double ClassificationMilliseconds,double CutCellIntegrationMilliseconds,double FineControlMilliseconds,double TotalMilliseconds);
