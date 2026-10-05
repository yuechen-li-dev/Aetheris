using System.Diagnostics;
using System.Text.Json;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aurelian.Shaders.Graphics;
using Copeland.TS.Gpu;
using Copeland.TS.Gpu.VdMir;

string output = args[0];
string naga = args[1];
var definitions = new Dictionary<string, SdfNode>
{
    ["Sphere"] = new SdfSphereNode(1),
    ["Cylinder"] = new SdfCylinderNode(0.8, 2),
    ["Cone"] = new SdfConeNode(1, 0.2, 2),
    ["Torus"] = new SdfTorusNode(0.8, 0.25),
    ["CSG"] = new SdfSubtractNode(new SdfCylinderNode(0.8, 2),
        new SdfTransformNode(new SdfSphereNode(0.7), Transform3D.CreateTranslation(new Vector3D(0, 0, -0.8)))),
};
var programs = new Dictionary<string, object>();
foreach (var (name, root) in definitions)
{
    // Rotate the authored field, not a renderer-specific substitute primitive.
    var field = new SdfTransformNode(root, Transform3D.CreateRotationX(0.6) * Transform3D.CreateRotationY(0.3));
    var timer = Stopwatch.StartNew();
    var lowering = CirVisualTsLowerer.Lower(field);
    if (!lowering.Success) throw new InvalidOperationException(lowering.FallbackReason);
    double generationMs = timer.Elapsed.TotalMilliseconds;
    string source = lowering.Program!.FieldSource + "\n" + """
        @space(clip.position)
        type ClipPosition = float4;
        stream Input { @location(0) position: float3; }
        stream Varyings {
            @builtin(position) position: ClipPosition;
            @location(0) local: float3;
        }
        stream Output {
            @target(0) color: float4;
            @builtin(frag_depth) depth: f32;
        }
        @vertex
        function VertexMain(input: Input): Varyings {
            return { position: float4(input.position.x, input.position.y, 0.0, 1.0), local: input.position };
        }
        @pixel
        function PixelMain(input: Varyings): Output {
            var t: f32 = 0.0;
            for (var i: u32 = 0; i < 256; i = i + 1) {
                const x: f32 = input.local.x * 1.5;
                const y: f32 = input.local.y * 1.5;
                const z: f32 = -1.5 + t;
                const d: f32 = Field(x, y, z);
                if (Abs(d) < 0.00001) {
                    const gx: f32 = Field(x + 0.0001, y, z) - Field(x - 0.0001, y, z);
                    const gy: f32 = Field(x, y + 0.0001, z) - Field(x, y - 0.0001, z);
                    const gz: f32 = Field(x, y, z + 0.0001) - Field(x, y, z - 0.0001);
                    const length: f32 = Max(Sqrt(gx * gx + gy * gy + gz * gz), 0.00000001);
                    const light: f32 = 0.2 + 0.8 * Max((gx * 0.3 + gy * 0.4 - gz * 0.866) / length, 0.0);
                    return { color: float4(light * 0.8, light * 0.6, light * 0.25, 1.0), depth: t / 3.0 };
                }
                t = t + Abs(d) * 0.9;
                if (t > 3.0) { break; }
            }
            Discard();
            return { color: float4(0.0, 0.0, 0.0, 1.0), depth: 1.0 };
        }
        """;
    var module = GpuGraphicsBinder.Compile(new GpuCompilationRequest([new GpuSourceFile(name + ".v.ts", source)]));
    if (!module.Success) throw new InvalidOperationException(string.Join("\n", module.Diagnostics.Select(item => item.Message)));
    var backend = VdMirGraphicsBackend.Compile(module, "vulkan1.1");
    var browser = WebGpuGraphicsBackend.Translate(module, backend, naga);
    File.WriteAllText(Path.Combine(output, name + ".v.ts"), source);
    File.WriteAllText(Path.Combine(output, name + ".hlsl"), backend.Hlsl);
    File.WriteAllText(Path.Combine(output, name + "-vertex.wgsl"), browser.VertexWgsl);
    File.WriteAllText(Path.Combine(output, name + "-fragment.wgsl"), browser.FragmentWgsl);
    programs[name] = new
    {
        lowering.Program.StructuralHash,
        Bounds = lowering.Program.Bounds,
        Vertex = browser.VertexWgsl,
        Fragment = browser.FragmentWgsl,
        generationMs,
        DxcMs = backend.Vertex.DxcMilliseconds + backend.Pixel.DxcMilliseconds,
        browser.TranslationMilliseconds,
        backend.DxcPath,
    };
    Console.WriteLine($"{name}: CIR -> Visual TS -> VD-MIR -> DXC -> validated WGSL");
}
File.WriteAllText(Path.Combine(output, "programs.json"), JsonSerializer.Serialize(programs, new JsonSerializerOptions { WriteIndented = true }));
