# FORGE-CS-A5 trusted external concept packs

Milestone: **FORGE-CS-A5**

Phase 2 doctrine remains:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

## Purpose

FORGE-CS-A5 adds **optional trusted local assembly loading** for external C# Forge concept packs during:

```bash
dotnet run --project Aetheris.CLI -- validate <file.firmament|file.firmfixture> --forge-pack <path-to-pack.dll> --json
```

Default behavior remains unchanged. Without `--forge-pack`, validation uses only the built-in:

```text
Aetheris.Standard
```

## Trust and security contract

Forge concept packs are trusted local code execution.
Aetheris does not sandbox external packs.
Do not load packs you do not trust.

Implemented hard policy:

- local file paths only
- existing `.dll` assemblies only
- no remote URLs
- no `file://` URIs
- no `.cs` source loading
- no Roslyn compilation
- no NuGet restore
- no package-id loading
- no implicit probing beyond explicit CLI paths
- no auto-discovery directories
- no Firmament syntax for importing or executing C#

Firmament remains data. External packs are loaded only because the user explicitly passes `--forge-pack` on the CLI.

## CLI shape

`aetheris validate --help` now advertises:

```text
--forge-pack <path>   Load a trusted local .NET assembly containing IForgeConceptPack implementations. This executes local code; Aetheris does not sandbox external packs. Do not load untrusted packs.
```

Multiple packs are supported by repeating the flag:

```bash
aetheris validate part.firmament --forge-pack ./PackA.dll --forge-pack ./PackB.dll --json
```

Scope remains validation-only. `build` and export paths were not widened.

## Assembly loader

Added loader:

```csharp
ForgeConceptPackAssemblyLoader
```

Behavior:

- rejects missing files
- rejects directory paths
- rejects URI-style input
- rejects non-`.dll` paths
- loads via `AssemblyLoadContext.Default.LoadFromAssemblyPath(...)`
- finds public non-abstract `IForgeConceptPack` types
- requires public parameterless constructors
- instantiates every discovered pack
- fails clearly when no pack types exist in the assembly

The loader does not claim isolation or unloading. Collectible contexts and plugin sandboxing remain out of scope.

## Registry merge and duplicate policy

Built-ins register first through `Aetheris.Standard`.

External packs then register into the same runtime registry and parser descriptor catalog.

Policy:

- duplicate `ConceptId` is an error
- built-ins cannot be silently replaced
- duplicate external concepts are errors
- error messages name the duplicate concept id and pack

Example failure:

```text
Forge concept pack 'Aetheris.TestForgePack.Duplicate (...dll)' attempted to register duplicate concept 'hole<Countersink>'.
```

Override and replacement policy remains deferred.

## Parser integration

The key A5 design issue was real: pre-A5 parser/binder validation rejected unknown concept ids before runtime validation could see them.

A5 implements parser integration by building a combined descriptor catalog from:

- built-in runtime concepts
- explicitly loaded external runtime concepts

That combined catalog is passed into the existing parser/binder path before concept field validation runs.

Result:

- built-ins still parse exactly as before by default
- external packs can add new concept ids such as `hole<BossTest>`
- parser/binder required-field, unknown-field, duplicate-field, and type checks remain active
- unknown concepts still fail deterministically when no matching trusted pack was loaded

Firmament syntax did not change.

## Report provenance

`firmamentV2Validation` now includes:

```json
{
  "forgeRuntime": {
    "builtInPack": "Aetheris.Standard",
    "externalPacks": [
      {
        "id": "Aetheris.TestForgePack",
        "version": "1.0.0",
        "assembly": "Aetheris.TestForgePack.dll"
      }
    ]
  }
}
```

Concept rows still expose `runtimeValidation.provider`, and external concepts report the external pack id there.

## Test-only external pack assemblies

Added test-only helper assemblies:

```text
tests/Aetheris.TestForgePack/
tests/Aetheris.TestForgePack.Duplicate/
```

`Aetheris.TestForgePack` provides:

- `TestForgePack`
- `hole<BossTest>`
- warning diagnostic `testforge.boss-hole.seen`

`Aetheris.TestForgePack.Duplicate` intentionally collides with built-in `hole<Countersink>` to prove duplicate rejection.

Fixture:

```text
fixtures/FirmamentV2/Language/invalid/concept-external-boss-hole.invalid.firmfixture
```

Without `--forge-pack`, it remains invalid with unknown-concept diagnostics.
With the trusted test pack loaded, the same source becomes valid and reports external provenance.

## Tests

Focused coverage added for:

- `aetheris validate --help` advertising trusted pack loading
- missing pack path failure
- assembly-without-pack failure
- external pack validating external concept ids
- duplicate built-in collision failure
- default built-in-only runtime metadata
- existing A4 PMI-obligation coverage remaining intact

## Explicit non-scope

FORGE-CS-A5 does not add:

- untrusted plugin loading
- sandboxing
- Roslyn/source compilation
- NuGet restore
- package-manager loading
- remote loading
- auto-discovery
- Firmament imports for C#
- template behavior
- document mutation
- PMI auto-generation
- AP242 export changes
