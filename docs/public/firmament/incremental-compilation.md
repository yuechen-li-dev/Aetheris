# Incremental assembly compilation

Keep a `FirmamentCompilationSession` alive across builds to reuse successful exact
part materializations. This is a bounded in-memory cache, with no new Firmament
syntax, disk cache, background process or dependency graph solver.

```csharp
using var session = new FirmamentCompilationSession(maximumDefinitions: 256);
var first = session.CompileProject(project);
var next = session.CompileProject(editedProject);
Console.WriteLine(next.Reuse?.ReusedDefinitions);
Console.WriteLine(next.Reuse?.RebuiltDefinitions);
```

`CompileFile(path)` and `Compile(source, sourceIdentity)` are also available.
Reuse evidence lists every requested distinct definition, whether it was reused,
and its reason. `Clear()` drops cached definitions; disposal releases the cache.
Calls within a session are serialized. Capacity uses deterministic insertion-order
eviction, retaining one latest entry per authored definition identity.

## What is reused

Ordinary template parts reuse the exact producer STEP payload, scalar/datum
semantic evidence and definition metadata. SectionChainFile, LoftFile and external
STEP definitions use their own source contents as inputs. Each hit reimports a
fresh BRep body from the original payload, avoiding geometry construction while
isolating the cache from public mutable topology and geometry stores. Metadata
arrays and semantic provenance are copied at the cache boundary.

Parsing, port binding, assembly solving, placement, mate residual checks and solid
interference checks still run. Display tessellation and export also run normally.
Moving a part can therefore reuse geometry while still reporting a new overlap.
Camera rotation continues to be a display operation, separate from compilation.

## Conservative invalidation

Keys include source identity, authored specialization identity and exact input
contents. Ordinary parts use the parser's entire non-assembly declaration catalog.
A shared feature, specification, declaration or relevant source-offset change
therefore invalidates ordinary parts conservatively. File-backed geometry uses its
own contents and does not depend on unrelated declaration changes. Assembly edits
reuse ordinary parts when the parser's declaration catalog is unchanged; edits
that alter blanked spans before/between declarations can invalidate them too.
Exact text is retained to protect source-span provenance rather than guessing
semantic equivalence from formatting.

Invalid inputs still go through current parsing and validation. Failed part
materializations are not stored. InlineStep resource dependencies, domain-specific
materializer delegates, geometry-valued semantic ports and non-STEP-backed producer
branches are outside this first cache qualification and use the uncached route.
There is no cross-process reuse; ordinary one-shot CLI commands retain their
existing behavior.

Web Runtime model sessions now retain this cache for assembly rebuilds and expose
reuse evidence under snapshot `timings.reuse`. Standalone Web Runtime part builds
continue through their existing uncached path. The .NET session API is also the
multi-document snapshot entry point; this does not add a multi-file browser editor.

## Guitar benchmark

```powershell
dotnet run --project demos/GuitarSurfacingX0 -c Release -- fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm artifacts/local/incremental-guitar/timings.json
```

The harness measures cold and warm uncached compilation, then a populated-session
compile. Cache population is outside the measured incremental run. Every run
must produce byte-identical USD to the uncached baseline. Display preparation is
measured separately; process startup and dotnet build are excluded.

On the guitar snapshot before Concept-directed material seating was added,
measured compilation was 9.885 seconds
cold, 4.137 seconds warm uncached, and 0.244 seconds with the populated session
(about 17 times faster than warm uncached). All 53 definitions were reused; none
were rebuilt. Every run produced identical USD. Display preparation remained
separate: 0.568, 0.404 and 0.292 seconds respectively.

The guitar snapshot regression changes the neck's section geometry and proves
one definition rebuilt while 52 were reused, with a changed neck STEP hash.
Other regressions cover exact export parity, placement-only edits, fresh overlap
validation, specification/shared-recipe changes, failed inputs, missing documents,
caller mutation, bounded eviction and explicit Clear/Dispose.

Qualification passed the Release solution build (including Web Runtime), all
eight focused incremental tests, the fast core lane (1,004 tests), and the full
serial solution lane (4,050 tests). Fresh CLI assembly inspection also succeeded.
Web Runtime wiring is build-qualified here; this run did not perform a browser
interaction qualification.

Recorded evidence is under ignored `artifacts/local/pickup-features/`:
`incremental-final-timings.json`, `incremental-final-focused.log`,
`incremental-final-build.log`, `incremental-final-fast.log` and
`incremental-final-full.log`.
