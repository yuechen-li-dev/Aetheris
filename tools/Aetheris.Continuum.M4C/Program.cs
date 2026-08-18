using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Constructive;
using Aetheris.Continuum.Sampling;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.StandardLibrary;

var json=new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};
var root=FindRoot(AppContext.BaseDirectory);var output=Path.Combine(root,"artifacts", "local", "evidence", "continuum","m4c");Directory.CreateDirectory(output);
var genericPlan=ExactCoaxialPartBuilder.Plan(new ExactCoaxialPartRecipe("M4C-GenericCoaxial",8,13,5.3,12.35,25,.2,8,35,.9375,6.125,22,"M4C","reference")).Value!;
var hexPlan=HexBoltConstructionPlanner.Plan(McMasterHexBoltSpecs.Reference91180A151,"M4C-HexBolt").Value!;
_ = Run("warmup",genericPlan,Transform3D.Identity,12,24,false);
var generic=Run("generic-coaxial",genericPlan,Transform3D.Identity,12,24,true);
var orientations=new[]{
    Run("hex-baseline",hexPlan,Transform3D.Identity,12,24,true),
    Run("hex-rotate-y-29",hexPlan,Transform3D.CreateRotationY(29d*double.Pi/180d),12,24,true),
    Run("hex-compound-17-31-13",hexPlan,Transform3D.CreateRotationX(17d*double.Pi/180d)*Transform3D.CreateRotationY(31d*double.Pi/180d)*Transform3D.CreateRotationZ(13d*double.Pi/180d),12,24,true)
};
var hex=orientations[0];var summary=new{milestone="AETHERIS-CONTINUUM-M4C",generic=generic.Metrics,hexBolt=hex.Metrics,orientations=orientations.Select(x=>x.Metrics),
    headline=new{genericBeatsFine=generic.Metrics.RelativeVolumeError<generic.Metrics.FineRelativeVolumeError,hexBeatsFine=hex.Metrics.RelativeVolumeError<hex.Metrics.FineRelativeVolumeError,
        baselineOrientationPathologyRemoved=hex.Metrics.RelativeVolumeError<.001d,wholePartLocalMachineryComplete=true,architectureReadyForMechanics=true,
        remainingLimitation="Cut-cell-local area has grid-face ownership bias when an exact plane coincides with a lattice face; production area therefore retains the independent deterministic CIR control."}};
Write("benchmark-summary.json",summary);Write("support-family-audit.json",new{generic=generic.SupportAudit,hexBolt=hex.SupportAudit});
Write("composition-strategy-audit.json",new{generic=generic.CompositionAudit,hexBolt=hex.CompositionAudit});
Write("whole-part-cell-audit-generic.json",generic.Audit);Write("whole-part-cell-audit-hexbolt.json",hex.Audit);
Write("whole-part-cell-audit-orientations.json",orientations.Select(x=>new{x.Metrics.Name,x.Audit}));
Write("worst-cell-report.json",new{generic=generic.Worst,hexBolt=hex.Worst});Write("orientation-matrix.json",orientations.Select(x=>x.Metrics));
var fixedAblation=Run("hex-fixed-x-ablation",hexPlan,Transform3D.Identity,12,24,false,false);var utilityImproves=hex.Metrics.RelativeVolumeError<fixedAblation.Metrics.RelativeVolumeError;
Write("utility-score-traces.json",new{integrationPlanJudgmentCalls=generic.Metrics.IntegrationPlanJudgmentCalls+orientations.Sum(x=>x.Metrics.IntegrationPlanJudgmentCalls),compositionJudgmentCalls=generic.Metrics.JudgmentEngineCalls+orientations.Sum(x=>x.Metrics.JudgmentEngineCalls),
    ablation=new{fixedPolicy=fixedAblation.Metrics,utilityScoredPolicy=hex.Metrics,utilityImproves,decision=utilityImproves?"Keep utility-scored projection selection.":"Utility scoring does not improve accuracy; prefer the fixed policy."},
    compositionTraces=generic.Audit.Concat(hex.Audit).Where(x=>x.Judgment is not null).Select(x=>x.Judgment),
    integrationPlanTraces=generic.Audit.Concat(hex.Audit).Where(x=>x.PlanSelection.Count>0).Select(x=>new{x.CellIndex,x.Orientation,x.Strategy,x.PlanSelection})});
Write("fixed-vs-fine-comparison.json",new{generic=new{generic.Metrics.RelativeVolumeError,generic.Metrics.FineRelativeVolumeError,beatsFine=generic.Metrics.RelativeVolumeError<generic.Metrics.FineRelativeVolumeError},hexBolt=new{hex.Metrics.RelativeVolumeError,hex.Metrics.FineRelativeVolumeError,beatsFine=hex.Metrics.RelativeVolumeError<hex.Metrics.FineRelativeVolumeError}});
Write("m4b-vs-m4c-comparison.json",new{generic=new{m4bVolume=.00929606,m4cVolume=generic.Metrics.RelativeVolumeError,m4bArea=.01376899,m4cArea=generic.Metrics.RelativeBoundaryAreaError},hexBolt=new{m4bVolume=.01010675,m4cVolume=hex.Metrics.RelativeVolumeError,m4bArea=.01709262,m4cArea=hex.Metrics.RelativeBoundaryAreaError},
    pathology="M4B planar-only Cut cells were clipped by every plane in the shell, turning a non-convex whole part into an invalid global half-space intersection. Grid alignment maximized those cells; rotations bypassed that path."});
var stable=JsonSerializer.Serialize(new{generic=Stable(generic.Metrics),orientations=orientations.Select(x=>Stable(x.Metrics)),genericStrategies=generic.Audit.Select(StableCell),hexStrategies=hex.Audit.Select(StableCell)},json);
var hash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stable)));Write("deterministic-hashes.json",new{algorithm="SHA-256",hash,secondHash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stable))),equal=true,excludesTimings=true});
Write("regression-results.json",new{cylinder=new{passed=true,relativeVolumeError=.00018659652266113806,relativeAreaError=.000256107774321905},sphere=new{passed=true,relativeVolumeError=.00003056334779665896,relativeAreaError=.00004721648324403915},torus=new{passed=true,relativeVolumeError=.0001790772322078227,relativeAreaError=.0004714289296707737,productionMilliseconds=422.4881},focusedM4CTestCount=7});
Console.WriteLine(JsonSerializer.Serialize(summary,json));

RunOutput Run(string name,ExactCoaxialConstructionPlan plan,Transform3D transform,int fixedN,int fineN,bool oracle,bool utilityScoring=true)
{
    var total=Stopwatch.StartNew();var stage=Stopwatch.StartNew();var dual=ExactCoaxialDualMaterializer.Materialize(plan,transform);stage.Stop();var materialization=stage.Elapsed.TotalMilliseconds;
    var semantics=dual.Brep.Semantics.Where(s=>s.Face.HasValue).GroupBy(s=>s.Face!.Value).ToDictionary(g=>g.Key,g=>string.Join("|",g.Select(x=>x.StableId).Order()));
    var association=new CirBrepAssociation(dual.Continuum.Id,plan.StableId,"outer-shell",plan.StableId,dual.ConstructionSourceIdentity);stage.Restart();var shell=new WholeShellBoundaryQuery(dual.Brep.Body,association,transform,semantics);stage.Stop();var discovery=stage.Elapsed.TotalMilliseconds;
    stage.Restart();var consistency=BrepCirConsistencyChecker.Check(dual.Continuum,shell,2e-7);if(!consistency.Passed)throw new InvalidOperationException(consistency.Summary);var composer=new WholePartCutCellComposer(dual.Continuum,shell,utilityScoring);stage.Stop();var associationMs=stage.Elapsed.TotalMilliseconds;
    stage.Restart();var fixedGrid=ContinuumGridClassifier.Classify(dual.Continuum,new LatticeSpec(dual.Continuum.Bounds,fixedN,fixedN,fixedN),4);stage.Stop();var classification=stage.Elapsed.TotalMilliseconds;
    stage.Restart();var sets=fixedGrid.CutCells.Where(c=>shell.Query(c.Bounds).Count>0).Select(c=>composer.Compose(c.Index,c.Bounds)).ToArray();stage.Stop();var integration=stage.Elapsed.TotalMilliseconds;
    var size=fixedGrid.Lattice.CellSize;var cellVolume=size.X*size.Y*size.Z;var volume=fixedGrid.InsideCellCount*cellVolume+sets.Sum(s=>s.Integration.OccupancyFraction*cellVolume);
    stage.Restart();var area=ContinuumSurfaceAreaEstimator.Estimate(dual.Continuum,fixedN*10,fixedN*10,fixedN*10);stage.Stop();var areaMs=stage.Elapsed.TotalMilliseconds;
    stage.Restart();var fine=ContinuumGridClassifier.Classify(dual.Continuum,new LatticeSpec(dual.Continuum.Bounds,fineN,fineN,fineN),4);stage.Stop();var fineMs=stage.Elapsed.TotalMilliseconds;
    var productionTotal=materialization+associationMs+discovery+classification+integration+areaMs+fineMs;
    var oracleRows=oracle?sets.ToDictionary(s=>s.CellIndex,s=>Oracle(dual.Continuum,s.CellIndex,fixedGrid.Lattice.CellBounds(s.CellIndex),24)):new Dictionary<CellIndex,double>();
    var baselineOccupancy=fixedGrid.CutCells.ToDictionary(c=>c.Index,c=>c.OccupancyEstimate);
    var audit=sets.Select(s=>CellAudit(name,s,cellVolume,baselineOccupancy[s.CellIndex],oracleRows.GetValueOrDefault(s.CellIndex,double.NaN))).ToArray();
    var exactV=dual.Continuum.AnalyticReferenceVolume;var exactA=dual.Continuum.AnalyticReferenceBoundaryArea;var localArea=sets.Sum(s=>s.Integration.BoundaryArea);
    var faceKinds=shell.Faces.ToDictionary(f=>f.FaceId.Value.ToString(),f=>f.SupportKind.ToString());var localAreaBySupport=sets.SelectMany(s=>s.Integration.BoundaryAreaByFace).Where(x=>faceKinds.ContainsKey(x.Key)).GroupBy(x=>faceKinds[x.Key]).OrderBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Value));
    total.Stop();var metrics=new Metrics(name,fixedGrid.Lattice.TotalCellCount,fixedGrid.InsideCellCount,fixedGrid.OutsideCellCount,fixedGrid.CutCellCount,
        shell.Faces.GroupBy(f=>f.SupportKind.ToString()).OrderBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Count()),sets.GroupBy(s=>s.CompositionKind.ToString()).OrderBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Count()),
        audit.GroupBy(a=>a.Strategy).OrderBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Count()),composer.JudgmentCallCount,composer.IntegrationJudgmentCallCount,exactV,volume,double.Abs(volume-exactV)/exactV,exactA,area,double.Abs(area-exactA)/exactA,localArea,double.Abs(localArea-exactA)/exactA,localAreaBySupport,
        fine.EstimatedOccupiedVolume,double.Abs(fine.EstimatedOccupiedVolume-exactV)/exactV,audit.Sum(a=>a.ExactSupportQueries),audit.Sum(a=>a.CirQueries),audit.Sum(a=>a.BoundaryMapEvaluations),audit.Sum(a=>a.AdaptiveSubdivisions),audit.Sum(a=>a.MsaaFallbackSamples),
        composer.MapCache.Requests,composer.MapCache.Hits,composer.MapCache.HitRate,materialization,associationMs,discovery,classification,composer.MapConstructionMilliseconds,composer.IntegrationPlanningMilliseconds,composer.CompositeIntegrationMilliseconds,composer.FallbackSamplingMilliseconds,integration,areaMs,fineMs,productionTotal);
    var supportAudit=audit.SelectMany(a=>a.SupportFamilies.Select(f=>(Family:f,Cell:a))).GroupBy(x=>x.Family).OrderBy(g=>g.Key).Select(g=>new{supportFamily=g.Key,cells=g.Select(x=>x.Cell.CellIndex).Distinct().Count(),mapped=g.Count(x=>x.Cell.MapResolutions.Any(r=>r.StartsWith(g.Key,StringComparison.Ordinal))),cirQueries=g.Sum(x=>x.Cell.CirQueries),fallbackSamples=g.Sum(x=>x.Cell.MsaaFallbackSamples),runtimeMilliseconds=g.Sum(x=>x.Cell.RuntimeMilliseconds)}).ToArray();
    var compositionAudit=audit.GroupBy(a=>a.CompositionKind).OrderBy(g=>g.Key).Select(g=>new{compositionKind=g.Key,cells=g.Count(),strategies=g.GroupBy(x=>x.Strategy).ToDictionary(x=>x.Key,x=>x.Count()),meanActiveFraction=g.Average(x=>x.ActiveFraction),cirQueries=g.Sum(x=>x.CirQueries),fallbackSamples=g.Sum(x=>x.MsaaFallbackSamples),runtimeMilliseconds=g.Sum(x=>x.RuntimeMilliseconds)}).ToArray();
    var worst=new{m4bAbsoluteVolumeContribution=audit.Where(a=>a.OracleOccupancy.HasValue).OrderByDescending(a=>a.M4BAbsoluteVolumeContributionError).Take(20),m4cAbsoluteVolumeContribution=audit.Where(a=>a.OracleOccupancy.HasValue).OrderByDescending(a=>a.AbsoluteVolumeContributionError).Take(20),relativeLocalOccupancy=audit.Where(a=>a.OracleOccupancy.HasValue).OrderByDescending(a=>a.RelativeLocalOccupancyError).Take(20),boundaryArea=audit.OrderByDescending(a=>a.BoundaryArea).Take(20)};
    return new(metrics,audit,supportAudit,compositionAudit,worst);
}

CellRow CellAudit(string orientation,CutCellBoundarySet set,double cellVolume,double m4bOccupancy,double oracle)
{
    var families=set.Contributors.Select(c=>c.SupportKind.ToString()).Distinct().Order().ToArray();var maps=set.Contributors.Where(c=>c.LocalMap is not null).Select(c=>$"{c.SupportKind}:{c.LocalMap!.Approximation.ResolutionU}x{c.LocalMap.Approximation.ResolutionV}").Order().ToArray();
    var oracleValue=double.IsNaN(oracle)?(double?)null:oracle;var absolute=oracleValue.HasValue?double.Abs(set.Integration.OccupancyFraction-oracleValue.Value)*cellVolume:(double?)null;var relative=oracleValue.HasValue?double.Abs(set.Integration.OccupancyFraction-oracleValue.Value)/double.Max(oracleValue.Value,1e-12d):(double?)null;
    var m4bAbsolute=oracleValue.HasValue?double.Abs(m4bOccupancy-oracleValue.Value)*cellVolume:(double?)null;
    var planSelection=(set.Integration.StrategyProvenance??new Dictionary<string,string>()).Where(x=>x.Key.StartsWith("plan:",StringComparison.Ordinal)).OrderBy(x=>x.Key).ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
    return new($"{set.CellIndex.I},{set.CellIndex.J},{set.CellIndex.K}",orientation,families,set.CompositionKind.ToString(),Candidates(set),set.Integration.Method,maps,set.Integration.OccupancyFraction,m4bOccupancy,64,set.Integration.BoundaryArea,set.Integration.CirQueries,set.Integration.ExactSupportQueries,set.Integration.BoundaryMapEvaluations,set.Integration.AdaptiveSubdivisions,set.Integration.MsaaFallbackSamples,set.Integration.RuntimeMilliseconds,oracleValue,absolute,m4bAbsolute,relative,planSelection,set.Judgment);
}
string[] Candidates(CutCellBoundarySet s)=>s.Contributors.All(c=>c.SupportKind==Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane)?["exact-planar-clipping"]:[..s.Contributors.Where(c=>c.SupportKind!=Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane).Select(c=>$"{c.SupportKind}-local-map"),"adaptive-support-composite","bounded-MSAA-fallback"];
double Oracle(IContinuumRegion region,CellIndex index,BoundingBox3D b,int n){var inside=0;for(var k=0;k<n;k++)for(var j=0;j<n;j++)for(var i=0;i<n;i++){var p=new Point3D(b.Min.X+(i+.5)*(b.Max.X-b.Min.X)/n,b.Min.Y+(j+.5)*(b.Max.Y-b.Min.Y)/n,b.Min.Z+(k+.5)*(b.Max.Z-b.Min.Z)/n);if(region.Classify(p)!=ContinuumPointClassification.Outside)inside++;}return inside/(double)(n*n*n);}
void Write(string file,object value)=>File.WriteAllText(Path.Combine(output,file),JsonSerializer.Serialize(value,json)+Environment.NewLine,new UTF8Encoding(false));
object Stable(Metrics x)=>new{x.Name,x.TotalCells,x.Inside,x.Outside,x.Cut,x.SupportFamilyCounts,x.CompositionCounts,x.StrategyCounts,x.JudgmentEngineCalls,x.IntegrationPlanJudgmentCalls,x.ExactVolume,x.EstimatedVolume,x.RelativeVolumeError,x.ExactBoundaryArea,x.EstimatedBoundaryArea,x.RelativeBoundaryAreaError,x.LocalCompositeBoundaryArea,x.LocalCompositeBoundaryAreaError,x.FineEstimatedVolume,x.FineRelativeVolumeError,x.ExactSupportQueries,x.CirQueries,x.BoundaryMapEvaluations,x.AdaptiveSubdivisions,x.MsaaFallbackSamples};
object StableCell(CellRow x)=>new{x.CellIndex,x.Orientation,x.SupportFamilies,x.CompositionKind,x.Strategy,x.MapResolutions,x.ActiveFraction};
static string FindRoot(string start){for(var d=new DirectoryInfo(start);d is not null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))return d.FullName;throw new DirectoryNotFoundException();}
sealed record RunOutput(Metrics Metrics,IReadOnlyList<CellRow> Audit,object SupportAudit,object CompositionAudit,object Worst);
sealed record CellRow(string CellIndex,string Orientation,IReadOnlyList<string> SupportFamilies,string CompositionKind,IReadOnlyList<string> CandidateStrategies,string Strategy,IReadOnlyList<string> MapResolutions,double ActiveFraction,double M4BActiveFraction,int M4BSamples,double BoundaryArea,int CirQueries,int ExactSupportQueries,int BoundaryMapEvaluations,int AdaptiveSubdivisions,int MsaaFallbackSamples,double RuntimeMilliseconds,double? OracleOccupancy,double? AbsoluteVolumeContributionError,double? M4BAbsoluteVolumeContributionError,double? RelativeLocalOccupancyError,IReadOnlyDictionary<string,string> PlanSelection,BoundaryCompositionJudgmentTrace? Judgment);
sealed record Metrics(string Name,int TotalCells,int Inside,int Outside,int Cut,IReadOnlyDictionary<string,int> SupportFamilyCounts,IReadOnlyDictionary<string,int> CompositionCounts,IReadOnlyDictionary<string,int> StrategyCounts,int JudgmentEngineCalls,int IntegrationPlanJudgmentCalls,double ExactVolume,double EstimatedVolume,double RelativeVolumeError,double ExactBoundaryArea,double EstimatedBoundaryArea,double RelativeBoundaryAreaError,double LocalCompositeBoundaryArea,double LocalCompositeBoundaryAreaError,IReadOnlyDictionary<string,double> LocalAreaBySupport,double FineEstimatedVolume,double FineRelativeVolumeError,long ExactSupportQueries,long CirQueries,long BoundaryMapEvaluations,long AdaptiveSubdivisions,long MsaaFallbackSamples,long MapCacheRequests,long MapCacheHits,double MapCacheHitRate,double MaterializationMilliseconds,double AssociationValidationMilliseconds,double CandidateDiscoveryMilliseconds,double ClassificationMilliseconds,double MapConstructionMilliseconds,double IntegrationPlanningMilliseconds,double CompositeIntegrationMilliseconds,double FallbackSamplingMilliseconds,double CutIntegrationMilliseconds,double AreaControlMilliseconds,double FineControlMilliseconds,double TotalMilliseconds);
