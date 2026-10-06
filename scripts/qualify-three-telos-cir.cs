using System.Text.Json;
using Aetheris.Continuum.Backends.Sdf;
using Copeland.TS.Gpu;
using Copeland.TS.Gpu.VdMir;
using Copeland.TS.Gpu.Wgsl;

// Explicit external compiler checkout is a qualification input, not a runtime dependency.
var lowering = CirVisualTsLowerer.Lower(new SdfCylinderNode(0.8, 2));
if (!lowering.Success) throw new InvalidOperationException(lowering.FallbackReason);
string source = lowering.Program!.FieldSource + File.ReadAllText(args[1]);
var compiled = WgslGraphicsBackend.Compile(new GpuCompilationRequest([new GpuSourceFile("TelosCylinder.v.ts", source)]));
if (!compiled.Success) throw new InvalidOperationException(string.Join("\n", compiled.Diagnostics.Select(d => d.Code + ": " + d.Message)));
var program = compiled.Program!;
Directory.CreateDirectory(args[0]);
File.WriteAllText(Path.Combine(args[0], "Cylinder.v.ts"), source);
File.WriteAllText(Path.Combine(args[0], "Cylinder.wgsl"), program.Code);
File.WriteAllText(Path.Combine(args[0], "cylinder.json"), JsonSerializer.Serialize(new {
    shaderId = program.SemanticHash, wgsl = program.Code,
    vertexEntryPoint = program.VertexEntryPoint, fragmentEntryPoint = program.FragmentEntryPoint,
    bindings = new[] { new { group = 0, binding = 0, kind = "uniform", byteSize = program.Semantics.Material!.Size } },
    capabilities = new[] { "fragment-depth", "telos-field-rays/1", "rigid-occurrence" },
    sourceIdentity = lowering.Program.StructuralHash,
    compiler = WgslGraphicsBackend.CompatibilityVersion, program.SourceMappings,
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"CIR Cylinder -> VD-MIR -> direct WGSL: {program.SemanticHash}; uniform {program.Semantics.Material!.Size} bytes");
