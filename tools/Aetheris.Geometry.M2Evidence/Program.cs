using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

var output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","artifacts", "local", "evidence", "geometry","reasoning-m2"));Directory.CreateDirectory(output);
var options=new JsonSerializerOptions{WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};options.Converters.Add(new JsonStringEnumConverter());var t=CurveExpression.T;var mm=CurveExpression.Length(1);var x=Direction3D.Create(new(1,0,0));var z=Direction3D.Create(new(0,0,1));
var line=BoundedParametricCurve3.LineSegment("line",Point3D.Origin,new(5,0,0),"evidence");
var circle=BoundedParametricCurve3.FromCurveGeometry("circle",CurveGeometry.FromCircle(new(Point3D.Origin,z,4,x)),0,2*double.Pi,"evidence");
var parabola=new BoundedParametricCurve3("parabola",new(-1,1),new(CurveExpression.Multiply(mm,t),CurveExpression.Multiply(mm,CurveExpression.Power(t,2)),CurveExpression.Length(0)),"evidence");
var surface=MathematicalSurfaces.EllipticParaboloid("bowl",2,3,1).Patch;
var minimumValue=CurveExpression.Multiply(mm,CurveExpression.Power(t,2)).Evaluate(0,0);var jetSnapshot=new{line=Curve(line,1),circle=Curve(circle,.4),parabola=Curve(parabola,0),patch=new{jet=surface.EvaluateJet2(0,0),curvature=CurvatureQuery.Patch(surface,0,0)},localMinimum=new{parameter=0,value=minimumValue.Value,firstDerivative=minimumValue.Du,secondDerivative=minimumValue.Duu,observation="The scalar value has zero first derivative and positive second derivative; no general contact-order classification is made."}};
Write("canonical-second-jets-and-curvature.json",jetSnapshot);

var scaled=Patch("scaled",SurfaceExpression.Multiply(SurfaceExpression.Length(3),SurfaceExpression.U),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.V),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(SurfaceExpression.Multiply(SurfaceExpression.Number(3),SurfaceExpression.U),2)));
var reversed=Patch("reversed",SurfaceExpression.Multiply(SurfaceExpression.Length(-3),SurfaceExpression.U),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.V),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(SurfaceExpression.Multiply(SurfaceExpression.Number(-3),SurfaceExpression.U),2)));
Write("parameterization-invariance.json",new{scaled=CurvatureQuery.Patch(scaled,0,0),reversed=CurvatureQuery.Patch(reversed,0,0),invariant="Gaussian curvature and principal-curvature magnitudes agree; signed mean/normal curvature follows orientation."});

var crease=Pair("crease",u=>SurfaceExpression.Length(0),u=>SurfaceExpression.Multiply(SurfaceExpression.Length(1),u));var curvatureBreak=Pair("curvature-break",u=>SurfaceExpression.Length(0),u=>SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(u,2)));var smooth=Pair("smooth",u=>SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(u,2)),u=>SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(u,2)));
var unavailablePatch=BoundedParametricPatch3.Procedural("first-only",smooth.Right.AuthoredPatch.Domain,(u,v)=>smooth.Right.AuthoredPatch.EvaluateJet1(u,v),"evidence");var unavailable=smooth.Right with{SurfaceConstruction=smooth.Right.SurfaceConstruction with{AuthoredPatch=unavailablePatch}};
var seams=new{g0PassG1Fail=Seam(crease,PanelContinuity.TangentG1),g1PassG2Fail=Seam(curvatureBreak,PanelContinuity.CurvatureG2),g2Pass=Seam(smooth,PanelContinuity.CurvatureG2),unknown=Seam((smooth.Left,unavailable),PanelContinuity.CurvatureG2)};Write("panel-continuity.json",seams);

const int iterations=100_000;var measures=new[]{Measure("curve-second-jet",iterations,_=>circle.EvaluateJet2(.4)),Measure("patch-second-jet",iterations,_=>surface.EvaluateJet2(0,0)),Measure("curve-curvature",iterations,_=>CurvatureQuery.Curve(circle,.4)),Measure("principal-curvature",iterations,_=>CurvatureQuery.Patch(surface,0,0)),Measure("panel-g1",5_000,_=>Seam(smooth,PanelContinuity.TangentG1)),Measure("panel-g2",5_000,_=>Seam(smooth,PanelContinuity.CurvatureG2))};Write("performance.json",measures);
var files=Directory.GetFiles(output,"*.json").Where(path=>Path.GetFileName(path) is not "deterministic-hashes.json" and not "performance.json").Order(StringComparer.Ordinal).Select(path=>new{file=Path.GetFileName(path),sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()}).ToArray();Write("deterministic-hashes.json",new{algorithm="SHA-256",files});Console.WriteLine(JsonSerializer.Serialize(new{output,measures,files},options));

object Curve(BoundedParametricCurve3 curve,double parameter)=>new{curve.StableId,curve.SupportsSecondJet,jet=curve.EvaluateJet2(parameter),curvature=CurvatureQuery.Curve(curve,parameter)};
BoundedParametricPatch3 Patch(string id,SurfaceScalarExpression px,SurfaceScalarExpression py,SurfaceScalarExpression pz)=>new(id,new(new(-1,1),new(-1,1)),new(px,py,pz),"evidence");
(PanelIr Left,PanelIr Right) Pair(string id,Func<SurfaceScalarExpression,SurfaceScalarExpression> leftZ,Func<SurfaceScalarExpression,SurfaceScalarExpression> rightZ){ParametricSurfaceIr Side(string suffix,double min,double max,Func<SurfaceScalarExpression,SurfaceScalarExpression> fn){var u=SurfaceExpression.U;return new(id+suffix,SurfaceConstructionKind.ParametricSurface,new(new(min,max),new(-1,1)),new(SurfaceExpression.Multiply(SurfaceExpression.Length(1),u),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.V),fn(u)),"evidence");}return(PanelFactory.FromParametric(Side("-left",-1,0,leftZ),controlCountU:9,controlCountV:3,tolerance:.01).Panel!,PanelFactory.FromParametric(Side("-right",0,1,rightZ),controlCountU:9,controlCountV:3,tolerance:.01).Panel!);}
PanelMateEvidence Seam((PanelIr Left,PanelIr Right) pair,PanelContinuity continuity)=>PanelNetworkValidator.Validate([pair.Left,pair.Right],[new("seam",pair.Left["East"],pair.Right["West"],continuity)]).Mates.Single();
void Write(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,options)+Environment.NewLine,new UTF8Encoding(false));
Measurement Measure(string name,int count,Action<int> operation){for(var i=0;i<100;i++)operation(i);var watch=Stopwatch.StartNew();for(var i=0;i<count;i++)operation(i);watch.Stop();return new(name,count,watch.Elapsed.TotalMilliseconds,watch.Elapsed.TotalNanoseconds/count);}
record Measurement(string Name,int Iterations,double ElapsedMilliseconds,double NanosecondsPerOperation);
