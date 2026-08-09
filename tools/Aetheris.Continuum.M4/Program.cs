using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

var root=FindRoot(AppContext.BaseDirectory);var output=Path.Combine(root,"docs","continuum","artifacts","m4");Directory.CreateDirectory(output);
var options=new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};
var baseline=Run("baseline",Transform3D.CreateTranslation(new(.031,-.027,.019)),16,true);
var orientations=new[]{baseline,Run("rotate-y-21",Transform3D.CreateRotationY(21d*Math.PI/180d)*Transform3D.CreateTranslation(new(.031,-.027,.019)),16,false),
    Run("compound-13-23-11",Transform3D.CreateRotationX(13d*Math.PI/180d)*Transform3D.CreateRotationY(23d*Math.PI/180d)*Transform3D.CreateRotationZ(11d*Math.PI/180d)*Transform3D.CreateTranslation(new(.031,-.027,.019)),16,false)};
var repeated=Run("baseline",Transform3D.CreateTranslation(new(.031,-.027,.019)),16,false);
var baselineGeometry=JsonSerializer.Serialize(Geometry(baseline));var repeatedGeometry=JsonSerializer.Serialize(Geometry(repeated));
var adversarial=RunAdversarial();
var benchmark=new{schema="aetheris-continuum-m4-v1",milestone="AETHERIS-CONTINUUM-M4",fixture="exact closed oriented BRep box / convex planar whole-shell proof",
    authority=new{occupancy="CIR/SDF",boundary="exact BRep topology/support/trim",materialSide="scale-relative CIR probes around exact BRep points",arbitration="JudgmentEngine only after direct bounded topology rules"},
    baseline,fixedLatticeBeatsFineBruteForce=baseline.RelativeVolumeError<baseline.FineBruteForceRelativeVolumeError,
    deterministicGeometry=baselineGeometry==repeatedGeometry,adversarialOrientation=adversarial,
    limitations=new[]{"Production whole-shell non-planar integration is not complete; non-planar contributors currently use a bounded CIR fallback.","The M3 root-fillet BRep body and analytic CIR fixture are not the same complete solid, so M4 consistency correctly rejects using that pair as whole-part authority.","HexBolt and CTC-01 were not attempted because Plane/Cylinder/Cone/Torus trimmed whole-shell integration remains the next blocker."}};
Write("benchmark-summary.json",benchmark);Write("orientation-matrix.json",orientations.Select(Matrix));Write("whole-part-diagnostics.json",baseline.Diagnostics);
Write("composition-kind-counts.json",orientations.Select(x=>new{x.Orientation,x.CompositionCounts,x.JudgmentCalls}));
Write("material-side-evidence.json",baseline.MaterialSideEvidence);Write("judgment-traces.json",baseline.Diagnostics.Where(d=>d.Judgment is not null).Select(d=>d.Judgment));
Write("adversarial-orientation-tests.json",adversarial);Write("fixed-vs-fine-comparison.json",new{fixedCells=baseline.TotalCells,fixedError=baseline.RelativeVolumeError,
    fineCells=baseline.FineTotalCells,fineBruteForceError=baseline.FineBruteForceRelativeVolumeError,fixedBeatsFine=baseline.RelativeVolumeError<baseline.FineBruteForceRelativeVolumeError});
Write("deterministic-hashes.json",new{algorithm="SHA-256",identical=baselineGeometry==repeatedGeometry,hash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(baselineGeometry)))});
Console.WriteLine(JsonSerializer.Serialize(new{output,baseline=Matrix(baseline),deterministic=baselineGeometry==repeatedGeometry,adversarial},options));

RunData Run(string orientation,Transform3D transform,int n,bool fine)
{
    var body=BrepPrimitives.CreateBox(2,1.5,1).Value!;var region=new ExactBrepBoxContinuumRegion(new("m4-box"),2,1.5,1,transform);
    var semantics=body.Topology.Faces.ToDictionary(f=>f.Id,f=>$"DatumFace:{f.Id.Value}");var assoc=new CirBrepAssociation(region.Id,"exact-box-body",body.ShellRepresentation!.OuterShellId.Value.ToString(),"m4-whole-part");
    var setupStart=Stopwatch.GetTimestamp();var shell=new WholeShellBoundaryQuery(body,assoc,transform,semantics);var composer=new WholePartCutCellComposer(region,shell);var setupMs=Stopwatch.GetElapsedTime(setupStart).TotalMilliseconds;
    var lattice=new LatticeSpec(Expand(region.Bounds,.173),n,n,n);var cellVolume=lattice.CellSize.X*lattice.CellSize.Y*lattice.CellSize.Z;var volume=0d;var area=0d;var inside=0;var outside=0;var queryMs=0d;var compositionMs=0d;var diagnostics=new List<CellDiagnostic>();
    var classifyStart=Stopwatch.GetTimestamp();var rows=lattice.Indices().Select(i=>(Index:i,Bounds:lattice.CellBounds(i),Class:ContinuumGridClassifier.ClassifyCell(region,lattice.CellBounds(i)))).ToArray();var classifyMs=Stopwatch.GetElapsedTime(classifyStart).TotalMilliseconds;
    foreach(var row in rows)
    {
        if(row.Class==CellClassification.Inside){inside++;volume+=cellVolume;continue;}if(row.Class==CellClassification.Outside){outside++;continue;}
        var start=Stopwatch.GetTimestamp();_ = shell.Query(row.Bounds);queryMs+=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        start=Stopwatch.GetTimestamp();var set=composer.Compose(row.Index,row.Bounds);compositionMs+=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        volume+=set.Integration.OccupancyFraction*cellVolume;area+=set.Integration.BoundaryArea;
        diagnostics.Add(new(row.Index,set.CompositionKind,set.Contributors.Select(c=>c.Boundary.ExactBrepFaceId!).ToArray(),
            set.Contributors.SelectMany(c=>c.EdgeIds).Distinct().Select(e=>e.Value.ToString()).ToArray(),set.Contributors.SelectMany(c=>c.VertexIds).Distinct().Select(v=>v.Value.ToString()).ToArray(),
            set.Contributors.Select(c=>c.MaterialSide).ToArray(),set.CompositeBoundaryMaps.Select(m=>m.GetType().Name).ToArray(),set.Judgment,
            set.Integration.Method,set.Integration.OccupancyFraction,set.Integration.BoundaryArea,set.Integration.BoundaryAreaByFace));
    }
    var counts=diagnostics.GroupBy(d=>d.CompositionKind).OrderBy(g=>g.Key).ToDictionary(g=>g.Key.ToString(),g=>g.Count());
    int? fineCells=null;double? fineError=null;if(fine){var fl=new LatticeSpec(Expand(region.Bounds,.173),32,32,32);fineCells=fl.TotalCellCount;var cv=fl.CellSize.X*fl.CellSize.Y*fl.CellSize.Z;var fv=0d;
        foreach(var i in fl.Indices()){var b=fl.CellBounds(i);var c=ContinuumGridClassifier.ClassifyCell(region,b);if(c==CellClassification.Inside)fv+=cv;else if(c==CellClassification.Cut)fv+=Sample(region,b,4)*cv;}fineError=Math.Abs(fv-region.ExactVolume)/region.ExactVolume;}
    return new(orientation,lattice.TotalCellCount,inside,outside,diagnostics.Count,counts,volume,region.ExactVolume,Math.Abs(volume-region.ExactVolume)/region.ExactVolume,
        area,region.ExactBoundaryArea,Math.Abs(area-region.ExactBoundaryArea)/region.ExactBoundaryArea,composer.JudgmentCallCount,diagnostics.Count==0?0:(double)composer.JudgmentCallCount/diagnostics.Count,
        composer.JudgmentRuntimeMilliseconds,setupMs,classifyMs,queryMs,compositionMs,fineCells,fineError,composer.Consistency.Probes,diagnostics);
}

object RunAdversarial()
{
    var source=BrepPrimitives.CreateBox(2,1.5,1).Value!;var bindings=new BrepBindingModel();foreach(var e in source.Bindings.EdgeBindings)bindings.AddEdgeBinding(e);foreach(var f in source.Bindings.FaceBindings)bindings.AddFaceBinding(f with{SameSense=!f.SameSense});
    var body=new BrepBody(source.Topology,source.Geometry,bindings,vertexPoints:null,shellRepresentation:source.ShellRepresentation);var transform=Transform3D.CreateRotationY(.27);var region=new ExactBrepBoxContinuumRegion(new("m4-box"),2,1.5,1,transform);
    var assoc=new CirBrepAssociation(region.Id,"reversed-face-orientation-box",body.ShellRepresentation!.OuterShellId.Value.ToString());var shell=new WholeShellBoundaryQuery(body,assoc,transform);var composer=new WholePartCutCellComposer(region,shell);
    var evidence=shell.Faces.Select(f=>composer.Compose(default,Around(Average(f.OuterTrimVertices),1e-3)).Contributors.Single().MaterialSide).ToArray();
    return new{sameSenseWasReversed=true,allSidesResolvedByCir=evidence.All(e=>e.Status==MaterialSideStatus.Resolved),faceIdentityPreserved=evidence.Select(e=>e.FaceId).Distinct().Count()==6,evidence};
}

double Sample(IContinuumRegion r,BoundingBox3D b,int n){var inside=0;for(var k=0;k<n;k++)for(var j=0;j<n;j++)for(var i=0;i<n;i++){var p=new Point3D(Lerp(b.Min.X,b.Max.X,(i+.5)/n),Lerp(b.Min.Y,b.Max.Y,(j+.5)/n),Lerp(b.Min.Z,b.Max.Z,(k+.5)/n));if(r.Classify(p)!=ContinuumPointClassification.Outside)inside++;}return inside/(double)(n*n*n);}
object Matrix(RunData x)=>new{x.Orientation,x.TotalCells,x.Inside,x.Outside,x.Cut,x.CompositionCounts,x.JudgmentCalls,x.JudgmentPercentage,x.EstimatedVolume,x.RelativeVolumeError,x.EstimatedArea,x.RelativeAreaError,x.SetupMilliseconds,x.ClassificationMilliseconds,x.CandidateQueryMilliseconds,x.CompositionAndIntegrationMilliseconds};
object Geometry(RunData x)=>new{x.Orientation,x.TotalCells,x.Inside,x.Outside,x.Cut,x.CompositionCounts,x.EstimatedVolume,x.EstimatedArea,x.Diagnostics};
void Write(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,options)+Environment.NewLine,new UTF8Encoding(false));
static string FindRoot(string start){for(var d=new DirectoryInfo(start);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))return d.FullName;throw new DirectoryNotFoundException();}
static BoundingBox3D Expand(BoundingBox3D b,double r)=>new(b.Min-new Vector3D(r,r,r),b.Max+new Vector3D(r,r,r));static BoundingBox3D Around(Point3D p,double r)=>new(p-new Vector3D(r,r,r),p+new Vector3D(r,r,r));
static Point3D Average(IReadOnlyList<Point3D> p)=>new(p.Average(x=>x.X),p.Average(x=>x.Y),p.Average(x=>x.Z));static double Lerp(double a,double b,double t)=>a+(b-a)*t;

sealed record RunData(string Orientation,int TotalCells,int Inside,int Outside,int Cut,IReadOnlyDictionary<string,int> CompositionCounts,double EstimatedVolume,double ExactVolume,double RelativeVolumeError,
    double EstimatedArea,double ExactArea,double RelativeAreaError,int JudgmentCalls,double JudgmentPercentage,double JudgmentMilliseconds,double SetupMilliseconds,double ClassificationMilliseconds,
    double CandidateQueryMilliseconds,double CompositionAndIntegrationMilliseconds,int? FineTotalCells,double? FineBruteForceRelativeVolumeError,IReadOnlyList<BrepCirConsistencyProbe> ConsistencyProbes,IReadOnlyList<CellDiagnostic> Diagnostics)
{public IReadOnlyList<MaterialSideEvidence> MaterialSideEvidence=>Diagnostics.SelectMany(d=>d.MaterialSideEvidence).GroupBy(e=>e.FaceId).Select(g=>g.First()).OrderBy(e=>e.FaceId.Value).ToArray();}
sealed record CellDiagnostic(CellIndex CellIndex,CutCellCompositionKind CompositionKind,IReadOnlyList<string> CandidateFaceIds,IReadOnlyList<string> EdgeIds,IReadOnlyList<string> VertexIds,
    IReadOnlyList<MaterialSideEvidence> MaterialSideEvidence,IReadOnlyList<string> MapTypes,BoundaryCompositionJudgmentTrace? Judgment,string IntegrationMethod,double OccupancyEstimate,double BoundaryAreaContribution,IReadOnlyDictionary<string,double> BoundaryAreaByFace);
