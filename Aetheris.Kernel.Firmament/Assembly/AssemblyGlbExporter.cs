using System.Text.Json;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Explicit presentation appearance. UVs follow the display definition's vertices;
/// texture bytes are embedded, never linked. Color factors are linear RGB.</summary>
public sealed record AssemblyGlbAppearance(AssemblyUsdMaterial? Material = null,
    double[]? TextureCoordinates = null, byte[]? BaseColorTexture = null, string? TextureMimeType = null);

/// <summary>Static glTF 2.0 binary lowering of qualified display geometry.
/// BRep/STEP remains engineering authority. No tessellation, shading graph, or animation here.</summary>
public static class AssemblyGlbExporter
{
    private static Dictionary<string,object> Material(string name,object pbr,AssemblyUsdMaterial look)
    {
        var result=new Dictionary<string,object> { ["name"]=name,["pbrMetallicRoughness"]=pbr };
        if (look.Opacity < 1) { result["alphaMode"]="BLEND"; result["doubleSided"]=true; }
        if (look.EmissiveRed != 0 || look.EmissiveGreen != 0 || look.EmissiveBlue != 0)
            result["emissiveFactor"]=new[] {look.EmissiveRed,look.EmissiveGreen,look.EmissiveBlue};
        return result;
    }
    public static byte[] Export(AssemblyM1CompilationResult compilation,
        IReadOnlyDictionary<string, double>? state = null,
        IReadOnlyDictionary<string, AssemblyGlbAppearance>? appearances = null)
    {
        if (!compilation.IsSuccess || compilation.Ir is null)
            throw new InvalidOperationException("assembly-glb-invalid-compilation");
        var pose = AssemblyKinematics.Evaluate(compilation.Ir, state ?? new Dictionary<string, double>());
        return Serialize(AssemblyDisplayMeshExporter.Export(compilation, pose: pose), appearances,
            compilation.Ir.Instances.Where(instance => instance.Appearance is not null)
                .ToDictionary(instance => instance.StableId, instance => instance.Appearance!, StringComparer.Ordinal));
    }

    public static byte[] Serialize(AssemblyDisplayMeshDocument display,
        IReadOnlyDictionary<string, AssemblyGlbAppearance>? appearances = null,
        IReadOnlyDictionary<string, AssemblyAppearanceBinding>? occurrenceLooks = null,
        IReadOnlyList<DisplayCamera>? cameras = null)
    {
        if (display.Units != "mm" || display.Definitions.Count == 0 || display.Occurrences.Count == 0)
            throw new InvalidOperationException("assembly-glb-invalid-document");
        var definitions = display.Definitions.OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();
        var occurrences = display.Occurrences.OrderBy(o => o.Path, StringComparer.Ordinal).ToArray();
        var definitionIds = definitions.Select((d, i) => (d.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        var occurrenceIds = occurrences.Select((o, i) => (o.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        if (appearances?.Keys.Any(key => !definitions.Any(d => d.Identity == key)) == true)
            throw new InvalidOperationException("assembly-glb-material-definition-unknown");
        // Validate the entire forest before allocating binary payloads; cycles and missing parents
        // must never silently drop geometry or create invalid glTF trees.
        foreach (var o in occurrences)
        {
            ValidateTransform(o.Transform);
            if (o.DefinitionId is not null && !definitionIds.ContainsKey(o.DefinitionId))
                throw new InvalidOperationException("assembly-glb-missing-definition");
            var visited = new HashSet<string>(StringComparer.Ordinal) { o.Id };
            var parent = o.ParentId;
            while (parent is not null)
            {
                if (!visited.Add(parent) || !occurrenceIds.TryGetValue(parent, out var p))
                    throw new InvalidOperationException("assembly-glb-invalid-hierarchy");
                parent = occurrences[p].ParentId;
            }
        }
        using var binary = new MemoryStream();
        using var writer = new BinaryWriter(binary);
        var views = new List<object>(); var accessors = new List<object>();
        var meshes = new List<object>(); var materials = new List<object>();
        var primitiveTemplates = new List<(Dictionary<string,object> Attributes,int Indices)>();
        var images = new List<object>(); var textures = new List<object>();
        int AddView(Action write, int? target = null)
        {
            while (binary.Position % 4 != 0) writer.Write((byte)0);
            var start = checked((int)binary.Position); write();
            var view = new Dictionary<string, object> { ["buffer"] = 0, ["byteOffset"] = start, ["byteLength"] = checked((int)binary.Position - start) };
            if (target is not null) view["target"] = target.Value;
            views.Add(view); return views.Count - 1;
        }
        int Floats(double[] values, int components, bool bounds = false)
        {
            var floats = values.Select(v => (float)v).ToArray();
            if (floats.Any(v => !float.IsFinite(v))) throw new InvalidOperationException("assembly-glb-float-overflow");
            var view = AddView(() => { foreach (var v in floats) writer.Write(v); }, 34962);
            var accessor = new Dictionary<string, object> { ["bufferView"] = view, ["componentType"] = 5126, ["count"] = values.Length / components, ["type"] = "VEC" + components };
            if (bounds)
            {
                accessor["min"] = Enumerable.Range(0, components).Select(c => floats.Where((_, i) => i % components == c).Min()).ToArray();
                accessor["max"] = Enumerable.Range(0, components).Select(c => floats.Where((_, i) => i % components == c).Max()).ToArray();
            }
            accessors.Add(accessor); return accessors.Count - 1;
        }
        int Indices(int[] values)
        {
            var view = AddView(() => { foreach (var v in values) writer.Write((uint)v); }, 34963);
            accessors.Add(new { bufferView = view, componentType = 5125, count = values.Length, type = "SCALAR" }); return accessors.Count - 1;
        }
        foreach (var d in definitions)
        {
            var count = d.Positions.Length / 3;
            if (count == 0 || d.Positions.Length % 3 != 0 || d.Normals.Length != d.Positions.Length
                || d.Indices.Length == 0 || d.Indices.Length % 3 != 0 || d.Indices.Any(i => i < 0 || i >= count)
                || d.Positions.Concat(d.Normals).Any(v => !double.IsFinite(v)))
                throw new InvalidOperationException("assembly-glb-invalid-mesh:" + d.Identity);
            for (var i = 0; i < d.Normals.Length; i += 3)
                if (Math.Abs(Math.Sqrt(d.Normals[i]*d.Normals[i] + d.Normals[i+1]*d.Normals[i+1] + d.Normals[i+2]*d.Normals[i+2]) - 1) > 1e-4)
                    throw new InvalidOperationException("assembly-glb-invalid-normal:" + d.Identity);
            var appearance = appearances?.GetValueOrDefault(d.Identity) ?? new();
            var m = appearance.Material ?? new();
            // PMI inspection look only; engineering geometry and authored looks are retained.
            if (display.Annotations.Count > 0 && appearance.Material is null) m = m with { Red = .74, Green = .79, Blue = .86, Metallic = .1, Roughness = .55, Opacity = .55 };
            if (new[] { m.Red, m.Green, m.Blue, m.Metallic, m.Roughness, m.Opacity, m.EmissiveRed, m.EmissiveGreen, m.EmissiveBlue }.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
                throw new InvalidOperationException("assembly-glb-invalid-material");
            var pbr = new Dictionary<string, object> { ["baseColorFactor"] = new[] { m.Red, m.Green, m.Blue, m.Opacity }, ["metallicFactor"] = m.Metallic, ["roughnessFactor"] = m.Roughness };
            var attributes = new Dictionary<string, object> { ["POSITION"] = Floats(d.Positions, 3, true), ["NORMAL"] = Floats(d.Normals, 3) };
            if (appearance.BaseColorTexture is not null)
            {
                var bytes = appearance.BaseColorTexture;
                var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
                var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
                if (!(png && appearance.TextureMimeType == "image/png" || jpeg && appearance.TextureMimeType == "image/jpeg")
                    || appearance.TextureCoordinates is null || appearance.TextureCoordinates.Length != count * 2
                    || appearance.TextureCoordinates.Any(v => !double.IsFinite(v)))
                    throw new InvalidOperationException("assembly-glb-invalid-texture");
                attributes["TEXCOORD_0"] = Floats(appearance.TextureCoordinates, 2);
                images.Add(new { bufferView = AddView(() => writer.Write(bytes)), mimeType = appearance.TextureMimeType });
                textures.Add(new { source = images.Count - 1 });
                pbr["baseColorTexture"] = new { index = textures.Count - 1 };
            }
            else if (appearance.TextureCoordinates is not null || appearance.TextureMimeType is not null)
                throw new InvalidOperationException("assembly-glb-texture-missing");
            materials.Add(Material(d.Identity,pbr,m));
            var indexView = AddView(() => { foreach (var index in d.Indices) writer.Write((uint)index); }, 34963);
            var indexAccessor = accessors.Count;
            accessors.Add(new { bufferView = indexView, componentType = 5125, count = d.Indices.Length, type = "SCALAR" });
            meshes.Add(new { name = d.Identity, primitives = new[] { new { attributes, indices = indexAccessor, material = materials.Count - 1 } }, extras = new { definitionIdentity = d.Identity, meshPipeline = d.MeshPipeline } });
            primitiveTemplates.Add((attributes,indexAccessor));
        }
        var variants = new Dictionary<(string, AssemblyUsdMaterial), int>();
        var nodes = new List<object>();
        foreach (var o in occurrences)
        {
            // Historical row-vector matrix storage is already the column-major array
            // of its transpose, exactly what glTF consumes. Do not transpose it again.
            var local = Transform3D.FromRowMajor(o.Transform);
            if (o.ParentId is not null) local *= Transform3D.FromRowMajor(occurrences[occurrenceIds[o.ParentId]].Transform).Inverse();
            var node = new Dictionary<string, object> { ["name"] = o.Path, ["matrix"] = local.ToRowMajor(), ["extras"] = new { occurrenceIdentity = o.Id } };
            if (o.DefinitionId is not null)
            {
                var meshIndex = definitionIds[o.DefinitionId];
                if (occurrenceLooks?.GetValueOrDefault(o.Id) is { } look)
                {
                    var m = look.Preview;
                    if (new[] {m.Red,m.Green,m.Blue,m.Metallic,m.Roughness,m.Opacity,m.EmissiveRed,m.EmissiveGreen,m.EmissiveBlue}.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
                        throw new InvalidOperationException("assembly-glb-invalid-material");
                    if (!variants.TryGetValue((o.DefinitionId,m),out var variant))
                    {
                        // Material variants share all accessors and buffer views.
                        var primitive = primitiveTemplates[meshIndex];
                        materials.Add(Material(look.Appearance,new {baseColorFactor=new[] {m.Red,m.Green,m.Blue,m.Opacity},metallicFactor=m.Metallic,roughnessFactor=m.Roughness},m));
                        meshes.Add(new {name=definitions[meshIndex].Identity,primitives=new[] {new {attributes=primitive.Attributes,indices=primitive.Indices,material=materials.Count-1}}});
                        variants[(o.DefinitionId,m)]=variant=meshes.Count-1;
                    }
                    meshIndex=variant;
                }
                node["mesh"] = meshIndex;
            }
            var children = occurrences.Where(c => c.ParentId == o.Id).Select(c => occurrenceIds[c.Id]).ToArray();
            if (children.Length > 0) node["children"] = children;
            nodes.Add(node);
        }
        // One explicit boundary converts Z-up millimetres to right-handed Y-up metres:
        // (x,y,z) -> .001 * (x,z,-y). All original local frames remain inspectable.
        var roots = occurrences.Where(o => o.ParentId is null).Select(o => occurrenceIds[o.Id]).ToList();
        var pmiJsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        foreach (var annotation in display.Annotations)
        {
            // Filled text needs no glyph outline in a GLB; line-only consumers keep them in Lines.
            var drawn = annotation.TextFill is not null && annotation.DraftingLines is { } drafting ? drafting : annotation.Lines;
            var positions = drawn.SelectMany(line => line.Zip(line.Skip(1), (a, b) => new[] { a, b })
                .SelectMany(pair => pair).SelectMany(point => point)).ToArray();
            if (positions.Length == 0) positions = annotation.Lines.SelectMany(line => line.Zip(line.Skip(1), (a, b) => new[] { a, b })
                .SelectMany(pair => pair).SelectMany(point => point)).ToArray();
            if (positions.Length == 0) throw new InvalidOperationException("glb-pmi-presentation-empty:" + annotation.Name);
            var ink = annotation.LeaderColor ?? [.05, .1, .18];
            materials.Add(new { name = "PMI " + annotation.Name, pbrMetallicRoughness = new { baseColorFactor = new[] { ink[0], ink[1], ink[2], 1 }, metallicFactor = 0, roughnessFactor = 1 }, emissiveFactor = ink });
            var primitives = new List<object> { new { attributes = new Dictionary<string, object> { ["POSITION"] = Floats(positions, 3, true) }, mode = 1, material = materials.Count - 1 } };
            if (annotation.TextFill is { Indices.Length: > 0 } fill)
            {
                materials.Add(new { name = "PMI text " + annotation.Name, doubleSided = true,
                    pbrMetallicRoughness = new { baseColorFactor = new[] { .04, .07, .13, 1 }, metallicFactor = 0, roughnessFactor = 1 },
                    emissiveFactor = new[] { .04, .07, .13 } });
                primitives.Add(new { attributes = new Dictionary<string, object> { ["POSITION"] = Floats(fill.Positions, 3, true) },
                    indices = Indices(fill.Indices), mode = 4, material = materials.Count - 1 });
            }
            if (annotation.Panel is { Indices.Length: > 0 } panel)
            {
                // Backing panels stay readable over a translucent part; emissive keeps them white under any lighting.
                materials.Add(new { name = "PMI panel " + annotation.Name, doubleSided = true,
                    pbrMetallicRoughness = new { baseColorFactor = new[] { 1d, 1d, 1d, .92 }, metallicFactor = 0, roughnessFactor = 1 },
                    emissiveFactor = new[] { .85, .87, .9 }, alphaMode = "BLEND" });
                primitives.Add(new { attributes = new Dictionary<string, object> { ["POSITION"] = Floats(panel.Positions, 3, true) },
                    indices = Indices(panel.Indices), mode = 4, material = materials.Count - 1 });
            }
            if (annotation.Target is { } target)
            {
                materials.Add(new { name = "PMI target " + target.Kind, doubleSided = true, alphaMode = "BLEND",
                    pbrMetallicRoughness = new { baseColorFactor = new[] { 1d, .7, .2, .38 }, metallicFactor = 0, roughnessFactor = 1 },
                    emissiveFactor = new[] { .25, .15, .02 } });
                primitives.Add(new { attributes = new Dictionary<string, object> {
                    ["POSITION"] = Floats(target.Indices.SelectMany(i => new[] { target.Positions[i*3], target.Positions[i*3+1], target.Positions[i*3+2] }).ToArray(), 3, true),
                    ["NORMAL"] = Floats(target.Indices.SelectMany(i => new[] { target.Normals[i*3], target.Normals[i*3+1], target.Normals[i*3+2] }).ToArray(), 3) }, mode = 4, material = materials.Count - 1 });
                var outline = target.Lines.SelectMany(line => line.Zip(line.Skip(1), (a,b) => a.Concat(b)).SelectMany(p => p)).ToArray();
                if (outline.Length > 0)
                    primitives.Add(new { attributes = new Dictionary<string, object> { ["POSITION"] = Floats(outline, 3, true) }, mode = 1, material = materials.Count - 1 });
            }
            meshes.Add(new { name = annotation.Id, primitives,
                extras = new { role = "PMI presentation", annotationId = annotation.Id } });
            roots.Add(nodes.Count);
            nodes.Add(new { name = annotation.Text, mesh = meshes.Count - 1, extras = new { pmi = JsonSerializer.SerializeToElement(Metadata(annotation), pmiJsonOptions) } });
        }
        if (display.EdgeLines.Count > 0)
        {
            // Model edges keep a translucent part readable: every B-rep edge as a dark line.
            var edgePositions = display.EdgeLines.SelectMany(line => line.Zip(line.Skip(1), (a, b) => a.Concat(b)).SelectMany(p => p)).ToArray();
            materials.Add(new { name = "Model edges", pbrMetallicRoughness = new { baseColorFactor = new[] { .16, .2, .26, 1 }, metallicFactor = 0, roughnessFactor = 1 },
                emissiveFactor = new[] { .16, .2, .26 } });
            meshes.Add(new { name = "edges", primitives = new[] { new { attributes = new Dictionary<string, object> { ["POSITION"] = Floats(edgePositions, 3, true) }, mode = 1, material = materials.Count - 1 } },
                extras = new { role = "B-rep edges" } });
            roots.Add(nodes.Count);
            nodes.Add(new { name = "Edges", mesh = meshes.Count - 1 });
        }
        foreach (var (camera,index) in (cameras ?? []).Select((c,i) => (c,i)))
        {
            ValidateTransform(camera.Transform);
            if (!double.IsFinite(camera.FovDegrees) || camera.FovDegrees <= 0 || camera.FovDegrees >= 180)
                throw new InvalidOperationException("display-glb-camera-invalid");
            roots.Add(nodes.Count);
            nodes.Add(new {name=camera.Name,matrix=camera.Transform,camera=index});
        }
        nodes.Add(new { name = "Aetheris mm Z-up to glTF m Y-up", matrix = new double[] {.001,0,0,0, 0,0,-.001,0, 0,.001,0,0, 0,0,0,1}, children = roots });
        var document = new Dictionary<string, object>
        {
            ["asset"] = new { version = "2.0", generator = "Aetheris PRESENTATION-3D-X0", extras = new { modelName = display.Name, authority = "BRep/STEP; GLB is a presentation asset", sourceProject = "Aetheris", pmiSchema = "aetheris/pmi-display/1", pmiUnits = "mm", pmi = JsonSerializer.SerializeToElement(display.Annotations.Select(Metadata), pmiJsonOptions) } },
            ["scene"] = 0, ["scenes"] = new[] { new { name = display.Name, nodes = new[] { nodes.Count - 1 } } },
            ["nodes"] = nodes, ["meshes"] = meshes, ["materials"] = materials,
            ["accessors"] = accessors, ["bufferViews"] = views, ["buffers"] = new[] { new { byteLength = checked((int)binary.Length) } }
        };
        if (cameras?.Count > 0) document["cameras"] = cameras.Select(c => new {name=c.Name,type="perspective",perspective=new {yfov=c.FovDegrees*Math.PI/180,znear=1d}}).ToArray();
        if (images.Count > 0) { document["images"] = images; document["textures"] = textures; }
        var json = JsonSerializer.SerializeToUtf8Bytes(document);
        using var result = new MemoryStream(); using var output = new BinaryWriter(result);
        var jsonLength = (json.Length + 3) & ~3; var binaryLength = checked(((int)binary.Length + 3) & ~3);
        output.Write(0x46546C67u); output.Write(2u); output.Write(checked((uint)(28L + jsonLength + binaryLength)));
        output.Write(jsonLength); output.Write(0x4E4F534Au); output.Write(json);
        for (var i = json.Length; i < jsonLength; i++) output.Write((byte)32);
        output.Write(binaryLength); output.Write(0x004E4942u); output.Write(binary.ToArray());
        for (var i = binary.Length; i < binaryLength; i++) output.Write((byte)0);
        return result.ToArray();
    }

    /// <summary>GLB metadata carries the semantic record and its anchors; geometry stays in the buffers.</summary>
    private static Visualization.PmiDisplayAnnotation Metadata(Visualization.PmiDisplayAnnotation annotation) =>
        annotation with { Lines = [], Target = annotation.Target is { } target ? target with { Positions = [], Normals = [], Indices = [], Lines = [] } : null };

    private static void ValidateTransform(double[] m)
    {
        var t = Transform3D.FromRowMajor(m);
        if (Math.Abs(m[3]) > 1e-12 || Math.Abs(m[7]) > 1e-12 || Math.Abs(m[11]) > 1e-12 || Math.Abs(m[15] - 1) > 1e-12
            || t.Apply(new Vector3D(1,0,0)).Cross(t.Apply(new Vector3D(0,1,0))).Dot(t.Apply(new Vector3D(0,0,1))) <= 0)
            throw new InvalidOperationException("assembly-glb-invalid-transform");
    }
}
