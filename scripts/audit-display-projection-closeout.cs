using System.Diagnostics;
using System.Text.Json;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Scene;

// Diagnostic only: compile through existing owners, inventory what they retain.
// This never constructs or injects a product display packet.
var root = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
string[] Properties(Type type) => type.GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
var results = new List<object>();
foreach (var relative in new[] { "fixtures/Canonical/Basics/box.firmament", "fixtures/Canonical/Basics/cylinder.firmament",
    "fixtures/Canonical/DisplayProjection/sphere.firmament", "fixtures/Canonical/DisplayProjection/cone.firmament",
    "fixtures/Canonical/DisplayProjection/torus.firmament", "fixtures/Canonical/DisplayProjection/locating-pin.firmament" })
{
    var path = Path.Combine(root, relative);
    var source = File.ReadAllText(path);
    var clock = Stopwatch.StartNew();
    var compiled = FirmamentBuildAndExport.CompileSource(source, Path.GetDirectoryName(path));
    clock.Stop();
    var shader = compiled.IsSuccess ? CirShaderArtifactProvider.Resolve(compiled.Value.Cir) : null;
    var repeatedShader = compiled.IsSuccess ? CirShaderArtifactProvider.Resolve(compiled.Value.Cir) : null;
    var legacy = new FirmamentCompiler().Compile(new(new(source, relative)));
    results.Add(new {
        kind = "normal-authored-part", source = relative, compiled.IsSuccess,
        compileMilliseconds = clock.Elapsed.TotalMilliseconds,
        shader = shader is null ? null : new { shader.Status, shader.Reason, shader.GenerationMilliseconds, shader.CacheHit,
            shader.Artifact?.ShaderId, shader.Artifact?.SourceIdentity, shader.Artifact?.CompilerVersion,
            repeatedCacheHit = repeatedShader!.CacheHit, repeatedMilliseconds = repeatedShader.GenerationMilliseconds },
        compiled.Diagnostics,
        retainedBody = compiled.IsSuccess && compiled.Value.RuntimeBody is not null,
        retainedCorrespondence = compiled.IsSuccess && compiled.Value.RuntimeCorrespondence is not null,
        retainedCir = compiled.IsSuccess ? compiled.Value.Cir is { } cir ? new { cir.DefinitionId, cir.Qualification,
            cir.FallbackReason, cir.StructuralIdentity, cir.Schema, runtimeRootRetained = cir.RuntimeRoot is not null,
            cir.MinimumMm, cir.MaximumMm } : null : null,
        exportedCategory = compiled.IsSuccess ? compiled.Value.ExportedBodyCategory : null,
        resultContract = Properties(typeof(FirmamentStepExportResult)),
        legacyCompatibilityCompilerSuccess = legacy.Compilation.IsSuccess,
        legacyCompatibilityDiagnostics = legacy.Compilation.Diagnostics,
        legacyPlanRetained = legacy.Compilation.IsSuccess && legacy.Compilation.Value.PrimitiveLoweringPlan is not null,
    });
}
// Control for the existing field lowering, explicitly not authored product data.
var field = CirVisualTsLowerer.Lower(new SdfCylinderNode(10, 30));
results.Add(new { kind = "standalone-cir-control", field.Success, field.FallbackReason,
    structuralIdentity = field.Program?.StructuralHash, bounds = field.Program?.Bounds });
var csgSource = File.ReadAllText(Path.Combine(root,"fixtures/Compatibility/LegacyV1/Examples/w2_cylinder_root_blind_bore_semantic.firmament"));
var csgCompatibility = new FirmamentCompiler().Compile(new(new(csgSource,"compatibility-csg.firmament")));
var csgRoot = csgCompatibility.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState.CirMirror.RuntimeRoot;
var csgLowering = CirVisualTsLowerer.Lower(csgRoot!);
var csgNormal = FirmamentBuildAndExport.CompileSource(csgSource);
results.Add(new { kind="preexisting-csg-route-boundary", compatibilitySuccess=csgCompatibility.Compilation.IsSuccess,
    retainedRoot=csgRoot is not null, loweringQualified=csgLowering.Success, csgLowering.FallbackReason,
    normalSourceAccepted=csgNormal.IsSuccess, csgNormal.Diagnostics });
var robotPath = Path.Combine(root, "fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament");
var robotClock = Stopwatch.StartNew();
var robot = new AssemblyM1Pipeline().CompileFile(robotPath);
results.Add(new { kind = "normal-assembly", robot.IsSuccess, robot.Diagnostics,
    compileMilliseconds = robotClock.Elapsed.TotalMilliseconds,
    geometryContract = Properties(typeof(AssemblyExecutedGeometry)),
    definitions = robot.Geometry?.DefinitionBodies.Count,
    retainedCirDefinitions = robot.Geometry?.DefinitionCir.Count,
    occurrences = robot.Ir?.Instances.Count,
    authoredAppearanceBindings = robot.Ir?.Instances.Count(i => i.Appearance is not null),
    resolvedAppearance = robot.Ir?.Instances.Where(i => i.Appearance is not null).Select(i => new { i.StableId, i.Appearance }).Take(3).ToArray(),
    displayDefinitionContract = Properties(typeof(AssemblyDisplayMeshDefinition)),
    displayOccurrenceContract = Properties(typeof(AssemblyDisplayMeshOccurrence)),
});
var guitar = new FirmamentAssemblyDocumentCompiler().CompileFile(Path.Combine(root,
    "fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm")).Compilation;
results.Add(new { kind = "material-authored-assembly", guitar.IsSuccess, guitar.Diagnostics,
    definitions = guitar.Geometry?.DefinitionBodies.Count,
    occurrences = guitar.Ir?.Instances.Count,
    authoredAppearanceBindings = guitar.Ir?.Instances.Count(i => i.Appearance is not null),
    resolvedAppearance = guitar.Ir?.Instances.Where(i => i.Appearance is not null).Select(i => new { i.StableId, i.Appearance }).Take(3).ToArray(),
});
foreach (var relative in new[] {
    "fixtures/Canonical/Scene/WarmModernHouse/house.firmament",
    "fixtures/Canonical/Scene/FactoryX0/factory.firmament" })
{
    using var session = new FirmamentSceneSession();
    var clock = Stopwatch.StartNew();
    var compiled = session.CompileFile(Path.Combine(root, relative));
    var elapsed = clock.Elapsed.TotalMilliseconds;
    var retained = compiled.IsSuccess ? session.CompileFile(Path.Combine(root, relative)) : null;
    var scene = compiled.Scene;
    var projected = scene is null ? null : DisplayProjection.Project(scene);
    results.Add(new { kind = "compiled-scene", source = relative, compiled.IsSuccess, compiled.Diagnostics,
        compileMilliseconds = elapsed, displaySchema = scene?.Display.Schema,
        definitions = scene?.Display.Definitions.Count, occurrences = scene?.Display.Occurrences.Count,
        environmentOccurrences = scene?.Nodes.Count(n => n.Kind is "EnvironmentPanel" or "WindowFinish"),
        appearances = scene?.Appearances.Count,
        transportedMaterials = projected?.Occurrences.Count(o => o.Material is not null),
        transportedBoundaries = projected?.Boundaries.Count,
        translucentAppearances = scene?.Appearances.Values.Count(a => a.Preview.Opacity < 1),
        cameras = scene?.Source.Cameras.Select(c => new { c.Name, c.PositionMm, c.LookAtMm, c.FovDegrees }).ToArray(),
        bounds = scene is null ? null : new { scene.MinimumMm, scene.MaximumMm },
        scene?.Performance, retainedPerformance = retained?.Scene?.Performance,
        compiledSceneContract = Properties(typeof(CompiledScene)),
    });
}
var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(output, "compiled-contracts.json"), json);
Console.WriteLine(json);
