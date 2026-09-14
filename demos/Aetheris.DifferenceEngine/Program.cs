using System.Text.Json;
using System.Security.Cryptography;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.DifferenceEngine;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var destination = Path.GetFullPath(args.Length>0 ? args[0] : "artifacts/local/demos/difference-engine");
            var output = Path.Combine(destination,".generation");
            var fixtures = Path.GetFullPath(args.Length>1 ? args[1] : "fixtures/DifferenceEngine");
            var design = new StorageDesign();
            var source = Path.Combine(output,"source");
            design.WriteSources(source,fixtures);
            var compiled = new AssemblyM1Pipeline().CompileFile(Path.Combine(source,"StorageGate.firmament"));
            if (!compiled.IsSuccess) throw new InvalidOperationException(string.Join("\n",compiled.Diagnostics.Select(d=>d.Code+": "+d.Message)));
            var step = AssemblyIrAp242Exporter.Export(compiled);
            if (!step.IsSuccess) throw new InvalidOperationException(string.Join("\n",step.Diagnostics.Select(d=>d.Message)));
            File.WriteAllText(Path.Combine(output,"storage-gate.step"),step.Value);
            File.WriteAllText(Path.Combine(output,"storage-gate.mesh.json"),AssemblyDisplayMeshExporter.Serialize(AssemblyDisplayMeshExporter.Export(compiled)));
            File.WriteAllText(Path.Combine(output,"storage-design.json"),JsonSerializer.Serialize(design,new JsonSerializerOptions{WriteIndented=true}));
            var qualification=StorageQualification.Run(design,compiled,Path.GetFullPath(Path.Combine(fixtures,"../..")));
            File.WriteAllText(Path.Combine(output,"storage-qualification.json"),JsonSerializer.Serialize(qualification,new JsonSerializerOptions{WriteIndented=true}));
            var metadata=new {
                schema="aetheris/difference-engine-storage-gate/1",
                assembly=compiled.Ir!.Name,
                uniqueParts=compiled.Geometry!.DefinitionBodies.Count,
                physicalOccurrences=compiled.Geometry.InstanceBodies.Count,
                assemblyDepth=compiled.Ir.Instances.Max(i=>i.Path.Segments.Count),
                modules=compiled.Ir.AssemblyDefinitions!.Select(d=>d.DefinitionIdentity).Order(StringComparer.Ordinal),
                dependencies=compiled.Ir.SourceDependencies!.Select(d=>new {path=Path.GetRelativePath(output,d.Path).Replace('\\','/'),d.Sha256}),
                occurrenceTree=compiled.Ir.Instances.Select(i=>new {i.StableId,path=i.Path.ToString(),i.ParentStableId,i.DefinitionIdentity})
            };
            File.WriteAllText(Path.Combine(output,"storage-manifest.json"),JsonSerializer.Serialize(metadata,new JsonSerializerOptions{WriteIndented=true}));
            var files=Directory.GetFiles(output,"*",SearchOption.AllDirectories).Where(f=>Path.GetFileName(f)!="hashes.json").Order(StringComparer.Ordinal).ToArray();
            var hashes=files.ToDictionary(f=>Path.GetRelativePath(output,f).Replace('\\','/'),f=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
            File.WriteAllText(Path.Combine(output,"hashes.json"),JsonSerializer.Serialize(hashes,new JsonSerializerOptions{WriteIndented=true}));
            foreach(var file in files.Append(Path.Combine(output,"hashes.json"))) {
                var target=Path.Combine(destination,Path.GetRelativePath(output,file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file,target,true);
            }
            Console.WriteLine($"Storage gate: {compiled.Geometry!.DefinitionBodies.Count} unique parts; {compiled.Geometry.InstanceBodies.Count} physical occurrences.");
            Console.WriteLine("Gate B NOT ADMITTED: a permanent output gear mesh back-drives storage during reader return. No adder or complete machine is claimed.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
