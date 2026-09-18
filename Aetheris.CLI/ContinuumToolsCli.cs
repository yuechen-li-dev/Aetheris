using System.Text.Json;
using Aetheris.Continuum.Reconstruction;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.CLI;

internal static class ContinuumToolsCli
{
    private const string Usage = "Usage: aetheris continuum <pat-query|contour|make-fixtures> [options]";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help") { WriteHelp(stdout); return args.Length == 0 ? 1 : 0; }
        return args[0] switch
        {
            "pat-query" => RunPat(args[1..],stdout,stderr,jsonOptions),
            "contour" => RunContour(args[1..],stdout,stderr,jsonOptions),
            "make-fixtures" => MakeFixtures(args[1..],stdout,stderr,jsonOptions),
            _ => Fail($"Unknown continuum command '{args[0]}'.",stderr)
        };
    }

    private static int RunPat(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if(args.Length>0&&args[0] is "-h" or "--help"){stdout.WriteLine("Usage: aetheris continuum pat-query <field.json> <queries.json> [--out <results.json>]");return 0;}
        if(args.Length<2){stderr.WriteLine("Usage: aetheris continuum pat-query <field.json> <queries.json> [--out <results.json>]");return 1;}
        var output=Option(args,"--out");
        using var fieldStream=File.OpenRead(args[0]); var field=ContinuumInterchange.ReadPointTorusField(fieldStream);
        var arrays=JsonSerializer.Deserialize<double[][]>(File.ReadAllText(args[1]),ContinuumInterchange.JsonOptions)??throw new InvalidDataException("Query JSON is empty.");
        var queries=arrays.Select((q,i)=>q.Length==3?new Point3D(q[0],q[1],q[2]):throw new InvalidDataException($"Query {i} must have three coordinates.")).ToArray();
        var evaluations=field.EvaluateBatch(queries);
        var result=new { schema="aetheris.point-torus-query-results.v1", approximate=true, authority="reconstruction/query evidence only", field.Provenance,
            queries=queries.Select((q,i)=>new { point=new[]{q.X,q.Y,q.Z}, evaluations[i].ValueMm, evaluations[i].Qualification, evaluations[i].IsSigned,
                evaluations[i].NeighborsUsed,evaluations[i].NearestSourceDistanceMm,evaluations[i].Reason }) };
        var text=JsonSerializer.Serialize(result,jsonOptions); if(output is null)stdout.WriteLine(text);else{WriteFile(output,text);stdout.WriteLine(JsonSerializer.Serialize(new{output=Path.GetFullPath(output),queryCount=queries.Length},jsonOptions));}
        return 0;
    }

    private static int RunContour(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if(args.Length>0&&args[0] is "-h" or "--help"){stdout.WriteLine("Usage: aetheris continuum contour <grid.json> --out <mesh.obj> [--report <report.json>]");return 0;}
        if(args.Length<1){stderr.WriteLine("Usage: aetheris continuum contour <grid.json> --out <mesh.obj> [--report <report.json>]");return 1;}
        var output=Option(args,"--out"); if(output is null){stderr.WriteLine("contour requires --out <mesh.obj>.");return 1;}
        var reportPath=Option(args,"--report"); using var stream=File.OpenRead(args[0]); var grid=ContinuumInterchange.ReadSdfGrid(stream,out var source);
        var result=SharpDualContouring.Reconstruct(grid,source); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        using(var writer=File.CreateText(output))ApproximateMeshObj.Write(result.Mesh,writer);
        var report=new {schema="aetheris.sharp-dual-contour-result.v1",approximate=true,authority=result.Mesh.Provenance.Authority,input=Path.GetFullPath(args[0]),output=Path.GetFullPath(output),
            vertices=result.Mesh.Vertices.Count,triangles=result.Mesh.Triangles.Count,result.Diagnostics,result.Statistics,result.Mesh.Provenance};
        var text=JsonSerializer.Serialize(report,jsonOptions);if(reportPath is not null)WriteFile(reportPath,text);stdout.WriteLine(text);return 0;
    }

    private static int MakeFixtures(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if(args.Length>0&&args[0] is "-h" or "--help"){stdout.WriteLine("Usage: aetheris continuum make-fixtures --out-dir <directory>");return 0;}
        var outDir=Option(args,"--out-dir");if(outDir is null){stderr.WriteLine("Usage: aetheris continuum make-fixtures --out-dir <directory>");return 1;}
        Directory.CreateDirectory(outDir);
        var axes=new[]{new[]{1d,0,0},new[]{-1d,0,0},new[]{0d,1,0},new[]{0d,-1,0},new[]{0d,0,1},new[]{0d,0,-1}};
        var samples=axes.Select(p=>new PointTorusJsonSample(p.Select(v=>v*10d).ToArray(),p,p.Select(_=>0d).ToArray(),new[]{0d,0d,1d},0d,10d)).ToArray();
        var pat=new PointTorusJsonDocument(ContinuumInterchange.PointTorusSchema,"exact 10 mm sphere fixture","checked-in deterministic fixture generator",null,null,6,4d,
            PointTorusSignCapability.SignedClosedOriented,samples,new(){["truth"]="length(q)-10 mm"});
        WriteFile(Path.Combine(outDir,"sphere-point-tori.json"),JsonSerializer.Serialize(pat,jsonOptions));
        WriteFile(Path.Combine(outDir,"sphere-queries.json"),JsonSerializer.Serialize(new[]{new[]{0d,0d,0d},new[]{10d,0d,0d},new[]{12d,0d,0d},new[]{0d,9d,0d}},jsonOptions));
        const int n=25; const double spacing=1d; var origin=new[]{-12.25d,-12.25d,-12.25d};var values=new double[n*n*n];var cursor=0;
        for(var z=0;z<n;z++)for(var y=0;y<n;y++)for(var x=0;x<n;x++)
        {
            var px=origin[0]+x*spacing;var py=origin[1]+y*spacing;var pz=origin[2]+z*spacing;
            values[cursor++]=BoxSdf(px,py,pz,8d,6d,5d);
        }
        var grid=new SampledSdfGridJsonDocument(ContinuumInterchange.SdfGridSchema,n,n,n,origin,new[]{spacing,spacing,spacing},values,"exact axis-aligned box half extents 8,6,5 mm");
        WriteFile(Path.Combine(outDir,"sharp-box-grid.json"),JsonSerializer.Serialize(grid,jsonOptions));
        stdout.WriteLine(JsonSerializer.Serialize(new {outDir=Path.GetFullPath(outDir),files=new[]{"sphere-point-tori.json","sphere-queries.json","sharp-box-grid.json"}},jsonOptions));return 0;
    }

    private static double BoxSdf(double x,double y,double z,double hx,double hy,double hz)
    {
        var qx=Math.Abs(x)-hx;var qy=Math.Abs(y)-hy;var qz=Math.Abs(z)-hz;
        var outside=Math.Sqrt(Math.Max(qx,0)*Math.Max(qx,0)+Math.Max(qy,0)*Math.Max(qy,0)+Math.Max(qz,0)*Math.Max(qz,0));
        return outside+Math.Min(Math.Max(qx,Math.Max(qy,qz)),0d);
    }
    private static string? Option(string[] args,string name){var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
    private static void WriteFile(string path,string text){var full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllText(full,text);}
    private static int Fail(string message,TextWriter stderr){stderr.WriteLine(message);stderr.WriteLine(Usage);return 1;}
    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Bounded approximate utilities for geometry decompilation assistance; never canonical CAD authority.");output.WriteLine();output.WriteLine(Usage);
        output.WriteLine("  pat-query <field.json> <queries.json> [--out <results.json>]  Evaluate imported precomputed local tori.");
        output.WriteLine("  contour <grid.json> --out <mesh.obj> [--report <report.json>] Reconstruct an approximate explicit mesh.");
        output.WriteLine("  make-fixtures --out-dir <directory> Generate deterministic sphere and sharp-box evidence inputs.");
    }
}
