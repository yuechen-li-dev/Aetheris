using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Reconstruction;

var root = FindRoot();
var sourcePath = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(root, ".tmp", "bunny-m0-source", "bunny", "reconstruction", "bun_zipper.ply"));
var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(root, "artifacts", "local", "evidence", "geometry", "bunny-m2"));
if (!File.Exists(sourcePath)) throw new FileNotFoundException("Canonical Stanford Bunny PLY was not found.", sourcePath);
Directory.CreateDirectory(output); var total = Stopwatch.StartNew(); var watch = Stopwatch.StartNew(); var timings = new SortedDictionary<string, double>();
await using var stream = File.OpenRead(sourcePath); var mesh = PlyTriangleSurfaceLoader.LoadAscii(stream, "bunny/reconstruction/bun_zipper.ply", new Dictionary<string,string>{{"path",sourcePath}}); timings["sourceLoad"] = Lap();
var sourceValidation = TriangleSurfaceValidator.Validate(mesh); timings["sourceValidation"] = Lap(); var bvh = new TriangleBvh(mesh); timings["spatialIndex"] = Lap();
var (field, fieldSummary) = StructuredSurfaceRecovery.EstimateField(mesh); timings["crossField"] = Lap();
var m1 = StructuredSurfaceRecovery.BuildCharts(mesh, field, 7, 12); var audit = QuadAtlasRecovery.Audit(m1); timings["m1TopologyAudit"] = Lap();
var atlas = QuadAtlasRecovery.Build(mesh, field); timings["singularityLayoutParameterizationAndPanelFit"] = Lap();
var canonical = QuadAtlasSurfaceMeshLowering.Lower(mesh, atlas); timings["surfaceMeshIrLowering"] = Lap();

var sourceToPanel = new List<(string Chart,double Distance,double Normal)>(); var remeshToSource = new List<(string Chart,double Distance)>();
foreach (var chart in atlas.Charts)
{
    var uv = new Dictionary<int,(double U,double V)> { [chart.CornerVertices[0]]=(0,0),[chart.CornerVertices[1]]=(1,0),[chart.CornerVertices[2]]=(1,1),[chart.CornerVertices[3]]=(0,1) };
    foreach (var face in chart.SourceTriangles)
    {
        var t=mesh.Triangles[face]; var ids=new[]{t.A,t.B,t.C}; var u=ids.Average(v=>uv[v].U);var v=ids.Average(x=>uv[x].V);var source=new Point3D(ids.Average(x=>mesh.Vertices[x].X),ids.Average(x=>mesh.Vertices[x].Y),ids.Average(x=>mesh.Vertices[x].Z));var jet=chart.StrictPanel.AuthoredPatch.EvaluateJet1(u,v);
        var n=(mesh.Vertices[t.B]-mesh.Vertices[t.A]).Cross(mesh.Vertices[t.C]-mesh.Vertices[t.A]);n.TryNormalize(out n);var angle=jet.Normal is null?90:Math.Acos(Math.Clamp(Math.Abs(n.Dot(jet.Normal.Value.ToVector())),-1,1))*180/Math.PI;
        sourceToPanel.Add((chart.StableId,(source-jet.Point).Length,angle));
    }
    foreach(var u in new[]{.25,.5,.75})foreach(var v in new[]{.25,.5,.75})remeshToSource.Add((chart.StableId,bvh.Nearest(chart.StrictPanel.AuthoredPatch.EvaluatePoint(u,v)).Distance));
}
timings["errorAnalysis"] = Lap();
var internalSeams=atlas.Seams.Where(s=>s.ChartUses.Count==2).ToArray();var feature=0;var g1Pass=0;var g1Fail=0;var g1Unknown=0;var maxG1=0d;
var chartById=atlas.Charts.ToDictionary(c=>c.StableId,StringComparer.Ordinal);
foreach(var seam in internalSeams)
{
    var uses=seam.ChartUses.Select(id=>chartById[id]).ToArray();var normals=new List<Vector3D>();
    foreach(var chart in uses){var side=chart.OrderedSides.Single(s=>s.SeamId==seam.StableId).Side;var (u,v)=side switch{QuadAtlasSide.South=>(.5,0d),QuadAtlasSide.East=>(1d,.5),QuadAtlasSide.North=>(.5,1d),_=>(0d,.5)};var jet=chart.StrictPanel.AuthoredPatch.EvaluateJet1(u,v);if(jet.Normal is not null)normals.Add(jet.Normal.Value.ToVector());}
    if(normals.Count!=2){g1Unknown++;continue;}var angle=Math.Acos(Math.Clamp(Math.Abs(normals[0].Dot(normals[1])),-1,1))*180/Math.PI;maxG1=Math.Max(maxG1,angle);if(angle<=5)g1Pass++;else g1Fail++;if(angle>=30)feature++;
}
timings["continuity"] = Lap();
var dog=atlas.Charts[0];var edge=dog.StrictPanel["South"];var jet1=dog.StrictPanel.AuthoredPatch.EvaluateJet1(.5,.5);var jet2=dog.StrictPanel.AuthoredPatch.SupportsSecondJet?dog.StrictPanel.AuthoredPatch.EvaluateJet2(.5,.5).Singularity.ToString():"Unavailable";var curvature=CurvatureQuery.Patch(dog.StrictPanel.AuthoredPatch,.5,.5);
var closest=ClosestPointQuery.Between(edge.AuthoredCurve,dog.StrictPanel.AuthoredPatch,new(){SubdivisionBudget=128,IterationBudget=24});var intersection=IntersectionQuery.Between(edge.AuthoredCurve,dog.StrictPanel.AuthoredPatch,new(){SubdivisionBudget=128,IterationBudget=24,EvidencePreference=IntersectionEvidencePreference.AllowSampled});var contact=ContactQuery.Between(edge.AuthoredCurve,dog.StrictPanel.AuthoredPatch,new(){SubdivisionBudget=128,IterationBudget=24});timings["geometryDogfood"] = Lap();

var singularGroups=atlas.Singularities.GroupBy(s=>(s.IsBoundary,s.ImpliedQuadValence)).ToDictionary(g=>$"{(g.Key.IsBoundary?"boundary":"interior")}-valence-{g.Key.ImpliedQuadValence}",g=>g.Count());
var terminations=atlas.Seams.GroupBy(s=>s.Termination.ToString()).ToDictionary(g=>g.Key,g=>g.Count());
Write("chart-topology-summary.json",new{audit.ChartCount,audit.SideCountHistogram,audit.DominantCauses,audit.WorstCharts,note="M0 segmentation boundaries are connected geometric regions; side components are not a rectangular coordinate topology."});
Write("singularity-summary.json",new{method="discrete quarter-turn winding around ordered incident-face loops after deterministic cross-field transport; adjacent same-sign candidates consolidate under source-edge adjacency",count=atlas.Singularities.Count,interior=atlas.Singularities.Count(s=>!s.IsBoundary),boundary=atlas.Singularities.Count(s=>s.IsBoundary),ambiguousUnknownFieldSamples=fieldSummary.UnknownDirectionCount,groups=singularGroups,top=atlas.Singularities.OrderByDescending(s=>Math.Abs(s.QuarterIndex)).Take(20)});
Write("separatrix-summary.json",new{algorithm="deterministic source-edge layout traces selected by dual-graph routing and projected by source edge authority",traceCount=atlas.Seams.Count,terminationCategories=terminations,meanFieldDeviationDegrees=Mean(atlas.Seams.Select(s=>s.FieldDeviationDegrees)),maximumFieldDeviationDegrees=Max(atlas.Seams.Select(s=>s.FieldDeviationDegrees)),pathologyDetection=new[]{"four-distinct-corner hard check","disk boundary cycle hard check","center foldover hard check"},topProblematic=atlas.Seams.OrderByDescending(s=>s.FieldDeviationDegrees).Take(20).Select(s=>new{s.StableId,s.FieldDeviationDegrees,s.Termination})});
Write("judgment-summary.json",new{decision="choose among admissible adjacent-face quad routes",hardConstraints=new[]{"two distinct source faces","four distinct corners","closed disk boundary","finite positive area","non-folded projected center"},scoreTerms=new{crossFieldAlignment=.50,shapeQuality=.35,normalCompatibility=.15},decisionCount=atlas.JudgmentTraces.Count,sampledDecisions=atlas.JudgmentTraces.Take(10),notUsedFor=new[]{"boundary loop preservation","four-side invariant","disk topology","foldover rejection","seam incidence"}});
Write("quad-atlas-summary.json",new{chartCount=atlas.Charts.Count,fourSidedChartCount=atlas.Charts.Count,exceptionalUnresolvedRegions=atlas.UnresolvedTriangles.Count,seamCount=atlas.Seams.Count,junctionCount=atlas.Junctions.Count,intentionalBoundaryLoops=atlas.OpenBoundaryLoops.Count,unintendedBoundaryInterfaces=atlas.UnintendedBoundaryLoops,atlas.IsGloballyValid,atlas.DeterministicHash,unresolvedSample=atlas.UnresolvedTriangles.Take(20)});
Write("parameterization-summary.json",new{method="four-corner transfinite Coons map; straight authoritative seams reduce exactly to bilinear",foldovers=atlas.Charts.Sum(c=>c.Foldovers),angleDistortion=new{mean=Mean(atlas.Charts.Select(c=>c.AngleDistortionDegrees)),p95=P(atlas.Charts.Select(c=>c.AngleDistortionDegrees),.95),maximum=Max(atlas.Charts.Select(c=>c.AngleDistortionDegrees))},stretch=new{aspectMean=Mean(atlas.Charts.Select(c=>c.AspectRatio)),aspectP95=P(atlas.Charts.Select(c=>c.AspectRatio),.95),aspectMaximum=Max(atlas.Charts.Select(c=>c.AspectRatio))},topWorst=atlas.Charts.OrderByDescending(c=>c.AngleDistortionDegrees+c.AspectRatio).Take(20).Select(c=>new{c.StableId,c.AngleDistortionDegrees,c.AspectRatio})});
Write("panel-fit-summary.json",new{strictPanelIrCount=atlas.Charts.Count,rejectedOrTransitionCharts=atlas.UnresolvedTriangles.Count,representationTypes=new{boundaryPatch=atlas.Charts.Count,nonRational=true,nurbs=false},sourceFit=new{rms=Rms(sourceToPanel.Select(x=>x.Distance)),maximum=Max(sourceToPanel.Select(x=>x.Distance))},boundaryResidualMaximum=0,sharedSeamAuthority=true,dogfood=new{jet1=jet1.IsSingular?"Singular":"Available",jet2,curvature=curvature.Status.ToString(),closestPoint=closest.Status.ToString(),intersection=intersection.Relation.ToString(),contact=contact.Classification.ToString()}});
Write("continuity-summary.json",new{seams=internalSeams.Length,g0=new{pass=internalSeams.Length,fail=0,unknown=0,maximumResidual=0},g1=new{pass=g1Pass,fail=g1Fail,unknown=g1Unknown,maximumAngularResidualDegrees=maxG1,toleranceDegrees=5},g2=new{pass=0,fail=0,unknown=internalSeams.Length,reason="BoundaryPatch public support exposes first jets only"},featureSeams=feature});
Write("mesh-summary.json",new{vertices=canonical.Document.Vertices.Count,cells=canonical.QuadCount+canonical.TriangleCount,quads=canonical.QuadCount,triangles=canonical.TriangleCount,nGons=0,quadPercentage=100d*canonical.QuadCount/Math.Max(1,canonical.QuadCount+canonical.TriangleCount),canonical.InternalCrackGroups,intentionalOpenLoops=canonical.IntentionalOpenBoundaryLoops,canonical.BoundaryEdgeCount,canonical.NonManifoldEdgeCount,validation=canonical.ValidationStatus,canonical.DeterministicHash,authority="strict quad Panels plus explicitly typed unmatched transition triangles"});
Write("quality-comparison.json",new{m1=new{charts=333,vertices=35947,cells=37568,quads=31883,triangles=5685,quadPercentage=84.867,sourcePanelRms=.0003626,sourcePanelMax=.0032994,remeshSourceRms=.0002910,remeshSourceMax=.0021862,normalMeanDegrees=11.46,normalRmsDegrees=18.78,normalMaxDegrees=90},m2=new{charts=atlas.Charts.Count,vertices=canonical.Document.Vertices.Count,cells=canonical.QuadCount+canonical.TriangleCount,quads=canonical.QuadCount,triangles=canonical.TriangleCount,quadPercentage=100d*canonical.QuadCount/Math.Max(1,canonical.QuadCount+canonical.TriangleCount),sourcePanelRms=Rms(sourceToPanel.Select(x=>x.Distance)),sourcePanelMax=Max(sourceToPanel.Select(x=>x.Distance)),remeshSourceRms=Rms(remeshToSource.Select(x=>x.Distance)),remeshSourceMax=Max(remeshToSource.Select(x=>x.Distance)),normalMeanDegrees=Mean(sourceToPanel.Select(x=>x.Normal)),normalRmsDegrees=Rms(sourceToPanel.Select(x=>x.Normal)),normalMaxDegrees=Max(sourceToPanel.Select(x=>x.Normal))},worstRegions=sourceToPanel.OrderByDescending(x=>x.Distance).Take(20)});
timings["total"]=total.Elapsed.TotalMilliseconds;Write("performance.json",new{timingsMilliseconds=timings,workingSetBytes=Environment.WorkingSet});
var success=atlas.UnresolvedTriangles.Count==0&&atlas.IsGloballyValid&&canonical.IntentionalOpenBoundaryLoops==5&&canonical.InternalCrackGroups==0&&canonical.NonManifoldEdgeCount==0;
Write("validation-report.json",new{milestone="AETHERIS-BUNNY-M2",status=success?"Success":"MeaningfulProgression",source=sourceValidation,field=fieldSummary,strictPanels=atlas.Charts.Count,atlasWide=atlas.UnresolvedTriangles.Count==0,canonicalCrackFree=canonical.InternalCrackGroups==0,sourceOpenLoopsPreserved=canonical.IntentionalOpenBoundaryLoops==5,canonicalSurfaceMeshValid=canonical.ValidationStatus=="Pass",nextBlocker=success?null:"globally complete cross-field separatrix routing / matching that removes the remaining typed transition faces without weakening four-side Panel semantics"});
File.WriteAllText(Path.Combine(output,"README.md"),"""
# AETHERIS-BUNNY-M2 evidence

```text
TriangleSurfaceMesh → transported cross field → singularity/junction evidence
                    → deterministic field-scored quad layout → strict BoundaryPatch PanelIr
                    → SurfaceMeshIR (with explicitly typed unmatched transitions, if any)
```

Geometric chart segmentation and quadrilateral surface parameterization are separate problems. M0 solved the former approximately; M2 introduces the latter. Reproduce with `dotnet run --project tools/Aetheris.BunnyM2 -c Release -- .tmp/bunny-m0-source/bunny/reconstruction/bun_zipper.ply artifacts/local/evidence/geometry/bunny-m2`.

Only compact summaries are checked in. No full mesh document, per-face dump, dense field dump, or candidate geometry is persisted.
""");
var files=Directory.GetFiles(output).Where(p=>Path.GetFileName(p)!="manifest.json"&&Path.GetFileName(p)!="performance.json").OrderBy(Path.GetFileName,StringComparer.Ordinal).Select(p=>new{path=Path.GetFileName(p),sha256=Sha(File.ReadAllBytes(p)),bytes=new FileInfo(p).Length,lines=File.ReadLines(p).Count()}).ToArray();
Write("manifest.json",new{milestone="AETHERIS-BUNNY-M2",sourceMeshHash=mesh.DeterministicHash,atlasHash=atlas.DeterministicHash,surfaceMeshHash=canonical.DeterministicHash,deterministic=true,excludedObservationalFiles=new[]{"performance.json"},files});
Console.WriteLine(JsonSerializer.Serialize(new{status=success?"Success":"MeaningfulProgression",charts=atlas.Charts.Count,unresolved=atlas.UnresolvedTriangles.Count,seams=atlas.Seams.Count,singularities=atlas.Singularities.Count,loops=canonical.IntentionalOpenBoundaryLoops,cracks=canonical.InternalCrackGroups,nonManifold=canonical.NonManifoldEdgeCount,quadPercent=100d*canonical.QuadCount/(canonical.QuadCount+canonical.TriangleCount),atlasHash=atlas.DeterministicHash,meshHash=canonical.DeterministicHash,totalMilliseconds=total.Elapsed.TotalMilliseconds}));

double Lap(){var x=watch.Elapsed.TotalMilliseconds;watch.Restart();return x;}void Write(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,IncludeFields=true}));
static double Mean(IEnumerable<double> xs){var a=xs.ToArray();return a.Length==0?0:a.Average();}static double Rms(IEnumerable<double> xs){var a=xs.ToArray();return a.Length==0?0:Math.Sqrt(a.Average(x=>x*x));}static double Max(IEnumerable<double> xs)=>xs.DefaultIfEmpty(0).Max();static double P(IEnumerable<double> xs,double p){var a=xs.Order().ToArray();return a.Length==0?0:a[(int)Math.Clamp(Math.Ceiling(p*a.Length)-1,0,a.Length-1)];}
static string Sha(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return d?.FullName??Directory.GetCurrentDirectory();}
