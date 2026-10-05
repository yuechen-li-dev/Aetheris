using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.CLI;

internal static class SceneCommand
{
    internal const string Usage="aetheris scene <inspect|export-usd|export-glb> <scene.firmament> [out.usda|out.glb] [--repeat 1..16] [--hide-boundary room.boundary] [--json]";
    internal static int Run(string[] args,TextWriter stdout,TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h") { stdout.WriteLine(Usage); return args.Length == 0 ? 1 : 0; }
        try
        {
            if (args.Length < 2 || args[0] is not ("inspect" or "export-usd" or "export-glb")) throw new ArgumentException(Usage);
            var operation=args[0]; var path=Path.GetFullPath(args[1]); var json=false; var repeat=1; string? output=null;
            var hiddenBoundaries=new HashSet<string>(StringComparer.Ordinal);
            for(var i=2;i<args.Length;i++)
            {
                if(args[i] == "--json") json=true;
                else if(args[i] == "--hide-boundary" && i+1<args.Length && operation != "inspect") hiddenBoundaries.Add(args[++i]);
                else if(args[i] == "--repeat" && i+1<args.Length && int.TryParse(args[++i],out repeat) && repeat is >=1 and <=16) { }
                else if(!args[i].StartsWith('-') && output is null && operation != "inspect") output=args[i];
                else throw new ArgumentException("Unknown or incomplete Scene option: "+args[i]);
            }
            if(operation != "inspect")
            {
                var extension=operation == "export-usd" ? ".usda" : ".glb";
                output=Path.GetFullPath(output ?? Path.Combine("artifacts","local","scene",Path.GetFileNameWithoutExtension(path)+extension));
                if(!Path.GetExtension(output).Equals(extension,StringComparison.OrdinalIgnoreCase) || output.Equals(path,StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Scene output requires "+extension+" and must differ from input.");
            }
            using var session=new FirmamentSceneSession(); SceneCompilationResult? result=null; var builds=new List<object>();
            for(var i=0;i<repeat;i++)
            {
                var watch=Stopwatch.StartNew(); result=session.CompileFile(path);
                builds.Add(new {index=i,elapsedMilliseconds=watch.Elapsed.TotalMilliseconds,performance=result.Scene?.Performance,reuse=result.Scene?.Reuse});
                if(!result.IsSuccess) break;
            }
            if(!result!.IsSuccess)
            {
                if(json) stdout.WriteLine(JsonSerializer.Serialize(new {success=false,diagnostics=result.Diagnostics},CliRunner.JsonOptions));
                else foreach(var d in result.Diagnostics) stderr.WriteLine(d.Code+": "+d.Message);
                return 1;
            }
            var scene=result.Scene!; var export=Stopwatch.StartNew(); long bytes=0;
            if(output is not null)
            {
                // Protect every engineering input, including AssemblyFile dependencies.
                if(scene.Source.Occurrences.Any(o => o.Definition.Contains('"') && o.Definition.Contains(Path.GetFileName(output),StringComparison.Ordinal)))
                    throw new ArgumentException("Output must differ from engineering definition inputs.");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                if(operation == "export-usd") File.WriteAllText(output,SceneExport.Usd(scene,hiddenBoundaries),new UTF8Encoding(false));
                else File.WriteAllBytes(output,SceneExport.Glb(scene,hiddenBoundaries));
                bytes=new FileInfo(output).Length;
            }
            var report=new {success=true,scene=scene.Source.Name,authoringUnits=scene.Source.Units,internalUnits="mm",rooms=scene.Source.Rooms.Count,
                assemblies=scene.Source.Occurrences.Count(o => o.Kind == "Assembly"),parts=scene.Source.Occurrences.Count(o => o.Kind == "Part"),
                definitions=scene.Display.Definitions.Count,occurrences=scene.Display.Occurrences.Count,cameras=scene.Source.Cameras.Count,
                bounds=new {minimumMm=scene.MinimumMm,maximumMm=scene.MaximumMm},boundaries=scene.Boundaries,openings=scene.Source.Openings,
                nodes=scene.Nodes,layoutFrames=scene.Source.LayoutFrames,builds,output,bytes,hiddenBoundaries,exportMilliseconds=output is null ? 0 : export.Elapsed.TotalMilliseconds};
            stdout.WriteLine(json ? JsonSerializer.Serialize(report,CliRunner.JsonOptions) : $"Scene {scene.Source.Name}: {scene.Source.Rooms.Count} rooms, {scene.Source.Occurrences.Count} authored placements, {scene.Display.Definitions.Count} shared display definitions"+(output is null ? "." : $"; exported {output}."));
            return 0;
        }
        catch(Exception ex) when(ex is ArgumentException or IOException or InvalidOperationException or FormatException or OverflowException)
        { stderr.WriteLine("scene-command-failed: "+ex.Message); return 1; }
    }
}
