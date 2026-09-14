using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.DifferenceEngine.Showcase;
public static class Program
{
    public static int Main(string[] args)
    {
        try {
            var output=Path.GetFullPath(args.Length>0?args[0]:"artifacts/local/demos/difference-engine-showcase");
            var design=args.Length>1?JsonSerializer.Deserialize<ShowcaseDesign>(File.ReadAllText(args[1]))!:new();
            var fixtures=Path.GetFullPath(args.Length>2?args[2]:"fixtures/DifferenceEngine");
            var source=Path.Combine(output,"source"); design.WriteSources(source,fixtures);
            Console.WriteLine("Compiling full showcase assembly…");
            var compiled=new AssemblyM1Pipeline().CompileFile(Path.Combine(source,"DifferenceEngine.firmament"));
            File.WriteAllText(Path.Combine(output,"build-diagnostics.json"),JsonSerializer.Serialize(new{compiled.Diagnostics,compiled.Ir},new JsonSerializerOptions{WriteIndented=true}));
            if(compiled.Geometry is {} geometry) File.WriteAllText(Path.Combine(output,"body-bounds.json"),JsonSerializer.Serialize(geometry.DefinitionBodies.Select(p=>new{p.Key,points=p.Value.Topology.Vertices.Select(v=>{p.Value.TryGetVertexPoint(v.Id,out var point);return point;}).ToArray()})));
            if(!compiled.IsSuccess)throw new InvalidOperationException(string.Join("\n",compiled.Diagnostics.Select(d=>d.Code+": "+d.Message)));
            var step=AssemblyIrAp242Exporter.Export(compiled); if(!step.IsSuccess)throw new InvalidOperationException(string.Join("\n",step.Diagnostics.Select(d=>d.Message)));
            File.WriteAllText(Path.Combine(output,"difference-engine.step"),step.Value);
            Console.WriteLine($"CAD: {compiled.Geometry!.DefinitionBodies.Count} shared bodies; {compiled.Geometry.InstanceBodies.Count} occurrences. Meshing…");
            var mesh=AssemblyDisplayMeshExporter.Export(compiled);
            File.WriteAllText(Path.Combine(output,"machine.mesh.json"),AssemblyDisplayMeshExporter.Serialize(mesh));
            File.WriteAllText(Path.Combine(output,"design.json"),JsonSerializer.Serialize(design,new JsonSerializerOptions{WriteIndented=true}));
            File.WriteAllText(Path.Combine(output,"assembly-inspection.json"),JsonSerializer.Serialize(compiled.Ir,new JsonSerializerOptions{WriteIndented=true}));
            PresentationManifest.Write(output,design,compiled,mesh);
            Console.WriteLine($"Mesh: {mesh.Definitions.Sum(d=>d.Indices.Length/3)} unique triangles. Complete assembly exported.");
            return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
