# Collaboration architecture

`Aetheris.Collaboration` owns the bounded engineering-review vocabulary and normalized `ReviewIr`. It depends only on common semantic infrastructure. Drawing consumes ReviewIR for projection; PowerPoint, Git, Microsoft Graph, email, and hosting are not review authority.

Review targets are stable semantic references with source provenance, current engineering display where available, and capabilities. A Drawing annotation can supply presentation provenance, but its underlying PMI or engineering semantic remains the target. Threads preserve authored source order and stable authored identities; compilation does not create timestamps or random IDs.

The authority chain is:

```text
Firmament engineering declarations -----> Product / PMI -----> DrawingIR
             |
             +---- authored Review declarations -----> ReviewIR
                                                        |
                                                        +---- Drawing review overlays
                                                        +---- DFM presentation deck
```

An accepted or resolved Proposal is still not product definition. A future speculative compiler may apply a typed proposal to an isolated product derivation, run assertions/analysis, and report impact. It must remain explicit and must not silently mutate source.
