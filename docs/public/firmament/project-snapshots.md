# In-memory Firmament project snapshots

`FirmamentProjectSnapshot` accepts one explicit `.firmasm` or `.firmament` root and a finite map of project-relative `.firmasm` and `.firmament` source documents. `AssemblyM1Pipeline.CompileProject(snapshot)` resolves `Include` declarations and `LoftFile`/`SectionChainFile` sources from that map. Paths use `/`, normalize `.` and `..`, and reject paths outside the project root. The local `CompileFile` path remains available for CLI use.

This compiler entry point can materialize the canonical Cartesian lamp assembly and its shade from two in-memory source strings. The browser SDK now accepts `projectDocuments` beside `sourceName` for Assembly and Scene compilation, routing to the existing retained compilation owners. Helios's local showcase uses that path for the canonical ATLAS, guitar and house projects. Browser language analysis also accepts `projectRoot` and `projectDocuments` for Assembly project context. This source-snapshot API does not introduce multi-file cloud persistence.

Assembly `Include` syntax currently accepts `.firmament` documents only, so nested `.firmasm` project documents are not supported. `ExternalStep` and `InlineStep` are rejected for snapshot compilation because the snapshot contains source documents, not binary STEP assets.

The assembly parser currently combines included declarations into the root source before binding. Dependency hashes identify included documents, but construct source ranges within those declarations do not yet retain their original document path. Multi-document source navigation requires that provenance work before an IDE can claim accurate cross-file selection.
