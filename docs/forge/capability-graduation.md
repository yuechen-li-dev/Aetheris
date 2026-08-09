# Forge capability graduation

The expected lifecycle is:

```text
private bounded C# helper
  -> explicit Forge extension
  -> reusable internal or public package
  -> optional shared-Kernel candidate
```

Working code is not automatically a Kernel feature. A capability should remain private or packaged through Forge when its semantics are customer-specific, proprietary, fast-moving, narrowly useful, or expensive for the shared project to maintain.

A candidate may graduate upstream only when evidence shows:

- broad usefulness across multiple independent consumers;
- generic semantics rather than customer policy;
- an honest exactness contract and admitted support geometry;
- deterministic behavior and stable identity;
- strong construction, BRep, export, provenance, and where relevant CIR tests;
- successful use across more than one extension or product scenario;
- a manageable long-term maintenance burden;
- a clear improvement to the small shared substrate rather than API duplication.

Graduation should preserve the existing Forge capability or provide a deterministic migration path. It must not force private callers to rewrite their Firmament Templates merely because an implementation becomes shared.

For the common report “the Kernel does not support my geometry,” first inspect existing Firmament Templates and Kernel primitives. If the missing operation is not demonstrably general, author and register a Forge capability. Upstream it only after the evidence above exists.
